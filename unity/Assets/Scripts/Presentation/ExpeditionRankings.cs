using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Expedition.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using UnityEngine;

namespace Expedition.Presentation
{
    [Serializable] public sealed class RankingRow { public int Rank, Score; public string Name; public bool Mine; }
    [Serializable] public sealed class RankingPage { public RankingRow[] Rows; public RankingRow Mine; public int Total; public string Board; }

    public sealed partial class ExpeditionScreen
    {
        public static readonly Rect RankingWindow = new Rect(125, 40, 1030, 640);
        const string RankingModule = "IgnisRankings";
        bool rankingBusy;
        string rankingStatus = "help", rankingOwner, rankedStartRequest, rankedStartFormation;
        RankingPage rankingPage;
        int rankingPageIndex;
        float rankingRefreshAfter;
        static string RankText(string key) { return CatalogText.Get("rank." + key); }
        bool RankingsReady { get { return cloudConfig != null && cloudConfig.IsReady && cloudConfig.RankingsEnabled; } }
        string RankedStartHelp { get { return RankText(rankingBusy ? "loading" :
            RankingsReady && UgsAccountBackend.Linked ? "start_help" : "local_help"); } }
        bool CanSubmitRankedRun
        {
            get { var proof = run == null ? null : run.RankedProof;
                return UgsAccountBackend.Linked && run != null && run.HasEnded && proof != null && !proof.Overflow &&
                    proof.OwnerId == AuthenticationService.Instance.PlayerId && run.RulesVersion == CatalogBalance.CurrentRules; }
        }
        async Task<T> RankingCall<T>(string endpoint, Dictionary<string, object> arguments = null)
        {
            if (!RankingsReady || !UgsAccountBackend.Linked) throw new InvalidOperationException();
            string owner = AuthenticationService.Instance.PlayerId;
            var request = CloudCodeService.Instance.CallModuleEndpointAsync<T>(RankingModule, endpoint,
                arguments ?? new Dictionary<string, object>());
            if (await Task.WhenAny(request, Task.Delay(25000)) != request)
            {
                // A late mutating reply may still succeed. Start and submit are idempotent for retry.
                _ = request.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException();
            }
            var value = await request;
            if (!UgsAccountBackend.Linked || owner != AuthenticationService.Instance.PlayerId) throw new InvalidOperationException();
            return value;
        }
        async void RunRankingOperation(Func<Task> operation)
        {
            if (rankingBusy || cloudBusy) return;
            rankingBusy = true; rankingStatus = "loading";
            try { await operation(); }
            catch (Exception) { rankingStatus = "failed"; Notice(RankText("failed")); }
            finally { rankingBusy = false; }
        }
        void OpenRanking()
        {
            modal = Modal.Rankings; rankingPageIndex = 0;
            if (!UgsAccountBackend.Linked) { rankingPage = null; rankingStatus = "guest"; return; }
            string owner = AuthenticationService.Instance.PlayerId;
            if (owner != rankingOwner) { rankingPage = null; rankingRefreshAfter = 0; rankingOwner = owner; }
            if (Time.realtimeSinceStartup >= rankingRefreshAfter) RunRankingOperation(RefreshRanking);
        }
        async Task RefreshRanking()
        {
            rankingRefreshAfter = Time.realtimeSinceStartup + 30;
            rankingPage = await RankingCall<RankingPage>("GetRanking");
            rankingStatus = "help";
        }
        async Task SubmitRanking()
        {
            if (!CanSubmitRankedRun) { rankingStatus = "ineligible"; return; }
            await RankingCall<int>("SubmitRun", new Dictionary<string, object> { { "proofJson", JsonUtility.ToJson(run.RankedProof) } });
            await RefreshRanking(); rankingStatus = "submitted";
        }
        void NewRun()
        {
            if (!RankingsReady || !UgsAccountBackend.Linked) { StartNewRun(); return; }
            if (cloudBusy || cloudSync != null && cloudSync.Pending != null)
            { Notice(RankText("resolve_cloud")); return; }
            RunRankingOperation(async () => {
                if (CanSubmitRankedRun) await SubmitRanking();
                if (rankedStartRequest == null || rankedStartFormation != selectedFormationId)
                { rankedStartRequest = Guid.NewGuid().ToString("N"); rankedStartFormation = selectedFormationId; }
                var ticket = await RankingCall<RankedRunTicket>("StartRun", new Dictionary<string, object> {
                    { "formation", selectedFormationId }, { "requestId", rankedStartRequest } });
                StartNewRun(ticket);
                rankedStartRequest = null; rankingStatus = "help";
            });
        }
        void DrawRankedStartButtons(Rect panel, Rect content)
        {
            bool online = RankingsReady && UgsAccountBackend.Linked;
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            GUI.enabled = !rankingBusy && !cloudBusy;
            if (online)
            {
                if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 0, 3), RankText("start"), primary)) NewRun();
                if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 1, 3), RankText("local_start"), button)) StartNewRun();
                if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 2, 3), CloudText("back"), button)) modal = Modal.None;
            }
            else ConfirmationButtons(footer, CatalogText.Get("text.0742"), NewRun, () => modal = Modal.None);
            GUI.enabled = true;
        }
        void DrawRankingPanel()
        {
            Fill(new Rect(0, 0, Width, Height), new Color(.005f,.008f,.015f,.84f));
            ExpeditionUiTheme.Window(RankingWindow);
            Rect c = ExpeditionUiTheme.ContentRect(RankingWindow), footer = ExpeditionUiTheme.FooterRect(RankingWindow);
            LocalizedGui.Label(new Rect(c.x,c.y,c.width,44), RankText("title") + "  ·  NORMAL", heading);
            LocalizedGui.Label(new Rect(c.x,126,c.width,54), RankText("description"), small);
            bool linked = UgsAccountBackend.Linked;
            // Clear cached rows if the account is signed out or replaced while this panel is open.
            if (!linked || rankingOwner != AuthenticationService.Instance.PlayerId) rankingPage = null;
            ExpeditionUiTheme.PanelBox(new Rect(c.x,190,c.width,48),true);
            string mine = !linked ? RankText("guest") : rankingPage?.Mine == null ? RankText("no_score") :
                CatalogText.Format("rank.mine", rankingPage.Mine.Rank, rankingPage.Mine.Score.ToString("N0"), rankingPage.Mine.Name);
            LocalizedGui.Label(new Rect(c.x+16,196,c.width-32,36),mine,hint);
            LocalizedGui.Label(new Rect(c.x+16,246,96,30),RankText("position"),small);
            LocalizedGui.Label(new Rect(c.x+128,246,560,30),RankText("name"),small);
            LocalizedGui.Label(new Rect(c.xMax-200,246,184,30),RankText("score"),small);
            var rows = rankingPage == null ? null : rankingPage.Rows;
            if (rows == null || rows.Length == 0)
                LocalizedGui.Label(new Rect(c.x+24,320,c.width-48,112),
                    !linked ? RankText("guest_help") : rankingBusy ? RankText("loading") :
                    rankingPage == null ? RankText("failed") : RankText("empty"), centered);
            else
            {
                int begin = rankingPageIndex*5;
                for(int i=begin;i<Mathf.Min(rows.Length,begin+5);i++)
                {
                    var row=rows[i]; float y=284+(i-begin)*46;
                    ExpeditionUiTheme.PanelBox(new Rect(c.x,y,c.width,40),row.Mine);
                    LocalizedGui.Label(new Rect(c.x+16,y+2,96,36),row.Rank.ToString(),hint);
                    LocalizedGui.Label(new Rect(c.x+128,y+2,560,36),row.Name+(row.Mine ? "  ·  "+RankText("you") : ""),body);
                    LocalizedGui.Label(new Rect(c.xMax-200,y+2,184,36),row.Score.ToString("N0"),body);
                }
            }
            LocalizedGui.Label(new Rect(c.x,524,c.width,44),
                !linked ? RankText("guest_help") : RankText(rankingStatus),small);
            ExpeditionUiTheme.FooterRule(RankingWindow);
            GUI.enabled=!rankingBusy && !cloudBusy;
            if(!linked)
            {
                if(LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer,0,2),CloudText("google"),primary)) modal=Modal.Account;
                if(LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer,1,2),CloudText("back"),button)) modal=Modal.None;
            }
            else
            {
                bool ready=GUI.enabled;
                GUI.enabled=ready && CanSubmitRankedRun;
                if(LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer,0,4),RankText("submit"),primary)) RunRankingOperation(SubmitRanking);
                GUI.enabled=ready && Time.realtimeSinceStartup>=rankingRefreshAfter;
                if(LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer,1,4),RankText("refresh"),button)) RunRankingOperation(RefreshRanking);
                GUI.enabled=ready && rows != null && rows.Length>5;
                if(LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer,2,4),RankText("next"),button))
                    rankingPageIndex=(rankingPageIndex+1)%((rows.Length+4)/5);
                GUI.enabled=ready;
                if(LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer,3,4),CloudText("back"),button)) modal=Modal.None;
            }
            GUI.enabled=true;
        }
    }
}
