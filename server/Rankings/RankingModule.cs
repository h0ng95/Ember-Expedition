using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Expedition.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;
using Unity.Services.Leaderboards.Model;

namespace Ignis.Server;

public sealed class ModuleSetup : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config) => config.AddGameApiClient();
}

// This module is the only writer to the board. Deploy access-policy.json with it.
public sealed class RankingModule
{
    public const string Board = "normal_r6_v1", TicketKey = "ignis_rank_ticket_r6_v1";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };
    static readonly Lazy<CatalogDatabase> Catalog = new(() => {
        using var stream = typeof(RankingModule).Assembly.GetManifestResourceStream("catalog.json");
        using var reader = new StreamReader(stream!);
        return CatalogDatabase.FromJson(reader.ReadToEnd());
    });
    static readonly JsonSerializerSettings Json = new() { MaxDepth = 16 };

    public sealed class TicketState
    {
        public RankedRunTicket Ticket;
        public bool Completed;
        public string ProofHash;
        public int Score;
    }

    static async Task RequireGoogle(IExecutionContext context)
    {
        if (string.IsNullOrEmpty(context.PlayerId) || string.IsNullOrEmpty(context.AccessToken))
            throw new InvalidOperationException("Google sign-in required.");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://player-auth.services.api.unity.com/v1/users/" + Uri.EscapeDataString(context.PlayerId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        request.Headers.Add("ProjectId", context.ProjectId);
        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var player = JObject.Parse(await response.Content.ReadAsStringAsync());
        if (!HasGoogleIdentity(player))
            throw new InvalidOperationException("Google sign-in required.");
    }
    // Matches Authentication SDK IdProviderKeys.Google. Do not trust a client-supplied provider name.
    public static bool HasGoogleIdentity(JObject player) =>
        (player["externalIds"] as JArray)?.Any(id => (string)id["providerId"] == "google.com" &&
            !string.IsNullOrEmpty((string)id["externalId"])) == true;

    static async Task<(TicketState State, string Lock)> ReadTicket(IExecutionContext c, IGameApiClient api)
    {
        var response = await api.CloudSaveData.GetProtectedItemsAsync(c, c.ServiceToken,
            c.ProjectId, c.PlayerId, new List<string> { TicketKey });
        var item = response.Data.Results.SingleOrDefault(x => x.Key == TicketKey);
        if (item == null) return (null, null);
        return (JsonConvert.DeserializeObject<TicketState>(item.Value.ToString(), Json), item.WriteLock);
    }
    static Task WriteTicket(IExecutionContext c, IGameApiClient api, TicketState state, string writeLock) =>
        api.CloudSaveData.SetProtectedItemAsync(c, c.ServiceToken, c.ProjectId, c.PlayerId,
            new SetItemBody(TicketKey, JsonConvert.SerializeObject(state), writeLock));

    [CloudCodeFunction("StartRun")]
    public async Task<RankedRunTicket> StartRun(IExecutionContext context, IGameApiClient api,
        string formation, string requestId)
    {
        await RequireGoogle(context);
        if (!Guid.TryParseExact(requestId, "N", out _) || !ExpeditionRun.IsStartingFormation(formation))
            throw new ArgumentException("Invalid start.");
        var old = await ReadTicket(context, api);
        if (old.State?.Ticket?.RequestId == requestId) return old.State.Ticket;
        uint seed;
        do { seed = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4)); } while (seed == 0);
        var ticket = new RankedRunTicket { Id = Guid.NewGuid().ToString("N"), OwnerId = context.PlayerId,
            RequestId = requestId, Formation = formation, Seed = seed, Rules = CatalogBalance.CurrentRules,
            CreatedUtcTicks = DateTime.UtcNow.Ticks };
        await WriteTicket(context, api, new TicketState { Ticket = ticket }, old.Lock);
        return ticket;
    }

    [CloudCodeFunction("SubmitRun")]
    public async Task<int> SubmitRun(IExecutionContext context, IGameApiClient api, string proofJson)
    {
        await RequireGoogle(context);
        if (string.IsNullOrEmpty(proofJson) || Encoding.UTF8.GetByteCount(proofJson) > 262144)
            throw new ArgumentException("Invalid proof size.");
        var proof = JsonConvert.DeserializeObject<RankedRunProof>(proofJson, Json);
        var saved = await ReadTicket(context, api);
        var state = saved.State;
        if (state?.Ticket == null || state.Ticket.OwnerId != context.PlayerId ||
            proof == null || proof.OwnerId != context.PlayerId || proof.TicketId != state.Ticket.Id)
            throw new ArgumentException("This run is not the current ranked expedition.");
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(proofJson)));
        if (state.Completed)
        {
            if (state.ProofHash != hash) throw new ArgumentException("Run already submitted.");
        }
        else
        {
            var replay = RankedReplay.Verify(Catalog.Value, state.Ticket, proof);
            state.Score = replay.Score;
            state.Completed = true;
            state.ProofHash = hash;
            // Compare-and-swap the accepted result before publication. A lost reply is safely retryable.
            await WriteTicket(context, api, state, saved.Lock);
        }
        await api.Leaderboards.AddLeaderboardPlayerScoreAsync(context, context.ServiceToken,
            Guid.Parse(context.ProjectId), Board, context.PlayerId, new AddLeaderboardScore(state.Score));
        return state.Score;
    }

    public sealed class Row { public int Rank, Score; public string Name; public bool Mine; }
    public sealed class Page { public Row[] Rows; public Row Mine; public int Total; public string Board; }
    public static string Alias(string playerId) => "Wanderer-" + Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(playerId))).Substring(0, 8);
    static Row ToRow(string id, int rank, double score, string owner) => new() {
        Rank = rank + 1, Score = (int)score, Name = Alias(id), Mine = id == owner };

    [CloudCodeFunction("GetRanking")]
    public async Task<Page> GetRanking(IExecutionContext context, IGameApiClient api)
    {
        await RequireGoogle(context);
        var top = await api.Leaderboards.GetLeaderboardScoresAsync(context, context.ServiceToken,
            Guid.Parse(context.ProjectId), Board, includeMetadata: false, offset: 0, limit: 20);
        Row mine = null;
        try
        {
            var entry = await api.Leaderboards.GetLeaderboardPlayerScoreAsync(context, context.ServiceToken,
                Guid.Parse(context.ProjectId), Board, context.PlayerId, includeMetadata: false);
            mine = ToRow(entry.Data.PlayerId, entry.Data.Rank, entry.Data.Score, context.PlayerId);
        }
        catch (ApiException e) when ((int)e.Response.StatusCode == 404) { }
        return new Page { Board = Board, Total = top.Data.Total, Mine = mine,
            Rows = top.Data.Results.Select(e => ToRow(e.PlayerId, e.Rank, e.Score, context.PlayerId)).ToArray() };
    }
}
