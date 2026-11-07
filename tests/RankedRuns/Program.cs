using Expedition.Core;
using Ignis.Server;
using Newtonsoft.Json;
using System.Diagnostics;

int checks=0;
void Check(bool ok,string name) { checks++; if(!ok)throw new Exception(name); }
void Reject(Action action,string name) { try{action();} catch(ArgumentException){checks++;return;} throw new Exception("Accepted "+name); }
var db=CatalogDatabase.FromJson(File.ReadAllText("unity/Assets/Resources/Content/unit-catalog-v010.json"));
Check(RankingModule.HasGoogleIdentity(Newtonsoft.Json.Linq.JObject.Parse("{\"externalIds\":[{\"providerId\":\"google.com\",\"externalId\":\"test\"}]}")),"Google SDK provider key");
Check(!RankingModule.HasGoogleIdentity(Newtonsoft.Json.Linq.JObject.Parse("{\"externalIds\":[]}")),"anonymous rejected");
Check(!RankingModule.HasGoogleIdentity(Newtonsoft.Json.Linq.JObject.Parse("{\"externalIds\":[{\"providerId\":\"google\",\"externalId\":\"test\"}]}")),"wrong provider key rejected");
if(args.Length==2 && args[0]=="--ticket")
{
    var liveTicket=JsonConvert.DeserializeObject<RankedRunTicket>(File.ReadAllText(args[1]));
    var live=ExpeditionRun.New(db,liveTicket.Seed,startFormationId:liveTicket.Formation);live.AttachRankedTicket(liveTicket);
    while(!live.HasEnded){RankedReplay.Fight(live);if(!live.HasEnded)live.Continue();}
    File.WriteAllText(".local-tools/v073/live-proof.json",JsonConvert.SerializeObject(live.RankedProof));
    Console.WriteLine("LIVE_PROOF_READY score="+live.Score+" battles="+live.RankedProof.Commands.Count(c=>c.Kind=="fight"));return;
}
var clock=Stopwatch.StartNew();
RankedRunTicket ticket=null; RankedRunProof proof=null;
foreach(var formation in new[]{"standard","tactical","ember","frost"})
for(uint seed=73001;seed<73013;seed++)
{
    ticket=new RankedRunTicket {Id=Guid.NewGuid().ToString("N"),OwnerId="test-player",Rules=CatalogBalance.CurrentRules,Seed=seed,Formation=formation};
    var run=ExpeditionRun.New(db,seed,startFormationId:formation);
    run.AttachRankedTicket(ticket);
    // Exercise growth and Undo through the public run API, with real consumed materials.
    var starting=run.Roster.Members.Select(m=>m.Id).ToArray();
    Check(run.Transfer(starting[1],starting[0],true),"first transfer");
    Check(run.Transfer(starting[2],starting[0],false),"second transfer");
    string family=CatalogRoster.FamilyCode(run.Roster.Find(starting[0]).FormId);
    string branch=family=="KN"?"E-KN-G":family=="FI"?"E-FI-B":family=="IC"?"E-IC-F":"E-AR-P";
    Check(run.Evolve(starting[0],branch),"evolution");
    Check(run.Undo() && run.Undo() && run.Undo(),"undo evolution and materials");
    string member=run.Roster.Members[0].Id;
    Check(run.Move(member,-4.5f,0),"move");
    Check(run.Undo(),"undo");
    Check(run.SetMaterialLocked(member,true),"lock");
    Check(run.SetMaterialLocked(member,false),"unlock");
    while(!run.HasEnded)
    {
        if(run.Gold>=ExpeditionRun.SummonCost && run.Roster.Members.Count<7) run.Summon();
        foreach(var unit in run.Roster.Members) run.SetDeployed(unit.Id,true);
        foreach(var research in CatalogResearch.Nodes)if(run.CanPurchaseTactic(research.Id))run.PurchaseTactic(research.Id);
        RankedReplay.Fight(run);
        if(!run.HasEnded)run.Continue();
        // Same proof survives a Unity-compatible field JSON round trip between battles.
        run=ExpeditionRun.Restore(db,JsonConvert.DeserializeObject<ExpeditionRunSave>(JsonConvert.SerializeObject(run.Capture())));
    }
    proof=run.RankedProof;
    var replay=RankedReplay.Verify(db,ticket,proof);
    Check(replay.Score==run.Score && replay.Victories==run.Victories && replay.Gold==run.Gold,"identical score and economy");
    Check(replay.Roster.Members.Count==run.Roster.Members.Count,"identical roster");
    var changed=proof.Copy();changed.Commands.First(c=>c.Kind=="fight").Flag^=true;
    Reject(()=>RankedReplay.Verify(db,ticket,changed),"forged battle result");
}
{
    var hybridTicket=new RankedRunTicket {Id=Guid.NewGuid().ToString("N"),OwnerId="test-player",Rules=CatalogBalance.CurrentRules,Seed=73100,Formation="standard"};
    var hybrid=ExpeditionRun.New(db,hybridTicket.Seed);hybrid.AttachRankedTicket(hybridTicket);
    Check(RankedReplay.Fight(hybrid),"earn actual first battle reward");hybrid.Continue();
    while(hybrid.Summon()!=null){}
    var knight=hybrid.Roster.Members.First(m=>m.FormId=="B-KN");
    var priest=hybrid.Roster.Members.First(m=>m.FormId=="B-PR");
    var material=hybrid.Roster.Members.Where(m=>m.Id!=knight.Id && m.Id!=priest.Id).Select(m=>m.Id).Take(4).ToArray();
    Check(material.Length==4,"paid materials available");
    Check(hybrid.SwapDeployment(knight.Id,material[3]) && hybrid.Undo(),"swap and undo");
    for(int i=0;i<4;i++)Check(hybrid.Transfer(material[i],i<2?knight.Id:priest.Id,false),"hybrid material");
    Check(hybrid.Evolve(knight.Id,"E-KN-G") && hybrid.Evolve(priest.Id,"E-PR-H"),"two evolved parents");
    var option=hybrid.GetCombinationOptions().First();
    Check(hybrid.Combine(option.RecipientId,option.DonorId,option.ResultId),"legal paid combination");
    // Save/restore while a battle is in progress must retain the exact preparation transcript.
    hybrid.BeginBattle();hybrid=ExpeditionRun.Restore(db,JsonConvert.DeserializeObject<ExpeditionRunSave>(JsonConvert.SerializeObject(hybrid.Capture())));
    var resumed=hybrid.ResumeBattle();while(!resumed.Ended && resumed.Time<75f)resumed.Step(1f/60f);
    hybrid.ResolveBattle(resumed.Ended && resumed.Winner==CatalogTeam.Ally);if(!hybrid.HasEnded)hybrid.Continue();
    while(!hybrid.HasEnded){RankedReplay.Fight(hybrid);if(!hybrid.HasEnded)hybrid.Continue();}
    Check(RankedReplay.Verify(db,hybridTicket,hybrid.RankedProof).Score==hybrid.Score,"hybrid and battle resume replay");
}
foreach(var malicious in new[]{
    new RankedCommand{Kind="move",A="u1",X=float.NaN},
    new RankedCommand{Kind="move",A="u1",X=30},
    new RankedCommand{Kind="research",A="invalid"},
    new RankedCommand{Kind="combine",A="u1",B="u2",C="invalid"},
    new RankedCommand{Kind="evolve",A="u1",B="invalid"},
    new RankedCommand{Kind="set_score"},
    new RankedCommand{Kind="fight",A=new string('x',65)}
})
{
    var altered=proof.Copy();altered.Commands=new[]{malicious}.Concat(altered.Commands).ToArray();
    Reject(()=>RankedReplay.Verify(db,ticket,altered),"illegal command");
}
var p=proof.Copy();p.OwnerId="other";Reject(()=>RankedReplay.Verify(db,ticket,p),"wrong owner");
p=proof.Copy();p.TicketId=Guid.NewGuid().ToString("N");Reject(()=>RankedReplay.Verify(db,ticket,p),"wrong ticket");
p=proof.Copy();p.Version=2;Reject(()=>RankedReplay.Verify(db,ticket,p),"future proof");
p=proof.Copy();p.Overflow=true;Reject(()=>RankedReplay.Verify(db,ticket,p),"overflow");
p=proof.Copy();p.Commands=new RankedCommand[2049];Reject(()=>RankedReplay.Verify(db,ticket,p),"oversize");
p=proof.Copy();p.Commands=p.Commands.Take(2).ToArray();Reject(()=>RankedReplay.Verify(db,ticket,p),"unfinished");
p=proof.Copy();p.Commands[0]=null;Reject(()=>RankedReplay.Verify(db,ticket,p),"null command");
Check(!RankingModule.Alias("private-player").Contains("private-player"),"public alias privacy");
Check(RankingModule.Alias("x")==RankingModule.Alias("x"),"stable alias");
Directory.CreateDirectory(".local-tools/v073");
File.WriteAllText(".local-tools/v073/replay-fixture.json",JsonConvert.SerializeObject(new{ticket,proof}));
Console.WriteLine($"RANKED_RUNS_PASS {checks} checks, 49 finished runs + replay + tampering; {clock.Elapsed.TotalSeconds:F1}s");
