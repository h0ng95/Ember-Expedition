using System;
using System.IO;
using System.Linq;
using Expedition.Core;

internal static class StartFormationProbe
{
    static string Branch(string form)
    {
        switch (CatalogRoster.FamilyCode(form))
        {
            case "KN": return "E-KN-G"; case "AR": return "E-AR-P";
            case "PR": return "E-PR-H"; case "FI": return "E-FI-B";
            case "IC": return "E-IC-F"; default: return "E-BA-G";
        }
    }
    static void Prepare(ExpeditionRun run, bool grow)
    {
        while (run.CanSummon && (grow || run.DeployedIds.Count < 5))
        {
            string donor = run.Summon();
            if (donor == null) throw new Exception("Affordable summon failed.");
            if (run.DeployedIds.Count < 5)
            { if (!run.SetDeployed(donor, true)) throw new Exception("Deploy failed."); continue; }
            CatalogMember recipient = run.Roster.Find("ally-01");
            if (recipient.FormId.StartsWith("F-"))
                recipient = run.DeployedIds.Select(run.Roster.Find).FirstOrDefault(m =>
                    m.FormId.StartsWith("B-") || m.FormId.StartsWith("E-") && m.Growth < 6);
            if (recipient == null || !run.Transfer(donor, recipient.Id, false))
                throw new Exception("Growth transfer failed.");
            if (recipient.FormId.StartsWith("B-") && recipient.Growth >= 2)
            { if (!run.Evolve(recipient.Id, Branch(recipient.FormId))) throw new Exception("Branch failed."); }
            else if (recipient.FormId.StartsWith("E-") && recipient.Growth >= 6)
            { if (!run.Evolve(recipient.Id, "F-" + CatalogRoster.FamilyCode(recipient.FormId)))
                throw new Exception("Final failed."); }
        }
        int front = 0, back = 0;
        float[] lanes = { 1.8f, -1.8f, .6f, -.6f, 0f };
        foreach (string id in run.DeployedIds.ToArray())
        {
            bool tank = CatalogRoster.FamilyCode(run.Roster.Find(id).FormId) == "KN";
            float x = tank ? -1.3f : -3.7f;
            float y = tank ? (front++ == 0 ? 0f : -1.7f) : lanes[back++];
            CatalogMember member = run.Roster.Find(id);
            if ((member.X != x || member.Y != y) && !run.Move(id, x, y))
                throw new Exception("Place failed.");
        }
    }
    static void Main(string[] args)
    {
        var db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
        string[] forms = { "standard", "tactical", "ember", "frost" };
        foreach (bool grow in new[] { false, true }) foreach (string form in forms)
        {
            int[] wins = new int[5], reach = new int[5];
            double[] hp = new double[5];
            for (int seed = 1; seed <= 20; seed++)
            {
                string[] unlock = form == "standard" ? new string[0] : new[] { form + "-start" };
                var run = ExpeditionRun.New(db, (uint)seed, ExpeditionDifficulty.Normal, unlock, form);
                bool unbroken = true;
                for (int w = 0; w < 5; w++)
                {
                    Prepare(run, grow);
                    var battle = run.BeginBattle();
                    while (!battle.Ended && battle.Time < 75f) battle.Step(.25f);
                    bool win = battle.Ended && battle.Winner == CatalogTeam.Ally;
                    if (win) wins[w]++; else unbroken = false;
                    if (unbroken) reach[w]++;
                    var allies = battle.Units.Where(u => u.Team == CatalogTeam.Ally).ToArray();
                    hp[w] += allies.Average(u => Math.Max(0f, u.Hp) / u.MaxHp);
                    if (!run.ResolveBattle(true) || !run.Continue()) throw new Exception("Advance failed.");
                }
            }
            Console.WriteLine((grow ? "grown" : "basic") + "/" + form + " w3=" + wins[2] +
                " hp=" + (hp[2] / 20).ToString("0.00") + " w4=" + wins[3] + " hp=" +
                (hp[3] / 20).ToString("0.00") + " w5=" + wins[4] + " reach=" + reach[4]);
        }
    }
}
