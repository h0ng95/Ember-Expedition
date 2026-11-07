using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Expedition.Core
{
    [Serializable] public sealed class RankedRunTicket
    {
        public string Id, OwnerId, Formation, RequestId;
        public uint Seed;
        public int Rules;
        public long CreatedUtcTicks;
    }
    [Serializable] public sealed class RankedCommand
    {
        public string Kind, A, B, C;
        public bool Flag;
        public float X, Y;
        public RankedCommand Copy() { return (RankedCommand)MemberwiseClone(); }
    }
    [Serializable] public sealed class RankedRunProof
    {
        public const int CommandLimit = 2048;
        public int Version = 1;
        public string TicketId, OwnerId;
        public bool Overflow;
        public RankedCommand[] Commands = new RankedCommand[0];
        public RankedRunProof Copy()
        {
            if (Commands == null || Commands.Length > CommandLimit) return null;
            var copy = (RankedRunProof)MemberwiseClone();
            copy.Commands = new RankedCommand[Commands.Length];
            for (int i=0;i<Commands.Length;i++) copy.Commands[i] = Commands[i] == null ? null : Commands[i].Copy();
            return copy;
        }
    }
    public sealed partial class ExpeditionRun
    {
        RankedRunProof rankedProof;
        public RankedRunProof RankedProof { get { return rankedProof == null ? null : rankedProof.Copy(); } }
        public void AttachRankedTicket(RankedRunTicket ticket)
        {
            Guid parsed;
            if (ticket == null || ticket.Rules != RulesVersion || ticket.Seed != InitialSeed ||
                ticket.Formation != StartFormationId || Wave != 1 || Phase != ExpeditionPhase.Preparation ||
                Gold != 40 || Victories != 0 || rankedProof != null || string.IsNullOrEmpty(ticket.OwnerId) ||
                !Guid.TryParseExact(ticket.Id, "N", out parsed)) throw new ArgumentException("Invalid ranked start.");
            RunId = ticket.Id;
            rankedProof = new RankedRunProof { TicketId = ticket.Id, OwnerId = ticket.OwnerId };
        }
        void Trace(string kind, string a=null, string b=null, string c=null, bool flag=false, float x=0, float y=0)
        {
            if (rankedProof == null || rankedProof.Overflow) return;
            if (rankedProof.Commands.Length >= RankedRunProof.CommandLimit) { rankedProof.Overflow = true; return; }
            var list = new List<RankedCommand>(rankedProof.Commands);
            list.Add(new RankedCommand { Kind=kind,A=a,B=b,C=c,Flag=flag,X=x,Y=y });
            rankedProof.Commands = list.ToArray();
        }
    }
    public static class RankedReplay
    {
        // Same fixed 60 Hz simulation and 75-second timeout as the shipped battle presentation.
        public static bool Fight(ExpeditionRun run)
        {
            var battle = run.BeginBattle();
            if (battle == null) throw new ArgumentException("Battle unavailable.");
            while (!battle.Ended && battle.Time < 75f) battle.Step(1f/60f);
            bool victory = battle.Ended && battle.Winner == CatalogTeam.Ally;
            run.ResolveBattle(victory);
            return victory;
        }
        public static ExpeditionRun Verify(CatalogDatabase catalog, RankedRunTicket ticket, RankedRunProof proof)
        {
            if (ticket == null || ticket.Rules != CatalogBalance.CurrentRules || ticket.Seed == 0 ||
                !ExpeditionRun.IsStartingFormation(ticket.Formation) || proof == null || proof.Version != 1 || proof.Overflow ||
                proof.TicketId != ticket.Id || proof.OwnerId != ticket.OwnerId || proof.Commands == null ||
                proof.Commands.Length == 0 || proof.Commands.Length > RankedRunProof.CommandLimit)
                throw new ArgumentException("Invalid ranked proof.");
            var run = ExpeditionRun.New(catalog, ticket.Seed, startFormationId:ticket.Formation);
            var budget = Stopwatch.StartNew();
            int battles = 0;
            foreach (var command in proof.Commands)
            {
                if (budget.Elapsed.TotalSeconds > 6) throw new ArgumentException("Replay budget exceeded.");
                if (command == null || TooLong(command.A) || TooLong(command.B) || TooLong(command.C)) throw new ArgumentException("Invalid command.");
                bool ok = false;
                switch (command.Kind)
                {
                    case "summon": ok=run.Summon()!=null; break;
                    case "research": ok=run.PurchaseTactic(command.A); break;
                    case "deploy": ok=run.SetDeployed(command.A,command.Flag); break;
                    case "swap": ok=run.SwapDeployment(command.A,command.B); break;
                    case "move":
                        if(command.X>=-6.2001f && command.X<=-.1999f && command.Y>=-2.5001f && command.Y<=2.2001f)
                            ok=run.Move(command.A,command.X,command.Y);
                        break;
                    case "lock": ok=run.SetMaterialLocked(command.A,command.Flag); break;
                    case "transfer": ok=run.Transfer(command.A,command.B,command.Flag); break;
                    case "evolve": ok=run.Evolve(command.A,command.B); break;
                    case "combine": ok=run.Combine(command.A,command.B,command.C); break;
                    case "undo": ok=run.Undo(); break;
                    case "fight":
                        if (++battles>32) throw new ArgumentException("Too many battles.");
                        ok=Fight(run)==command.Flag; break;
                    case "continue": ok=run.Continue(); break;
                }
                if (!ok) throw new ArgumentException("Rejected action or battle result.");
            }
            if (!run.HasEnded) throw new ArgumentException("Only finished expeditions can rank.");
            return run;
        }
        static bool TooLong(string value) { return value != null && value.Length > 64; }
    }
}
