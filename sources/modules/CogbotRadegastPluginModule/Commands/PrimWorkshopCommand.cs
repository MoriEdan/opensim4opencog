using System.Collections.Generic;
using cogbot.Listeners;
using cogbot.TheOpenSims;
using OpenMetaverse;
using Radegast;

using MushDLR223.ScriptEngines;
using Radegast.Rendering;

namespace cogbot.Actions
{
    public class PrimWorkshopCommand : cogbot.Actions.Command, RegionMasterCommand
    {
        public PrimWorkshopCommand(BotClient client)
        {
            Name = "Prim Workshop";
            Description = "Runs PrimWorkshop on a prim. Usage: PrimWorkshop [prim]";
            Category = cogbot.Actions.CommandCategory.Objects;
            Parameters = new[] { new NamedParam(typeof(SimObject), typeof(UUID)) };
        }

        public override CmdResult ExecuteRequest(CmdRequest args0)
        {
            var args = args0.tokens;
            if (args.Length == 0)
            {
                return ShowUsage();
            }

            int argsUsed;
            List<string> searchArgs = new List<string> {"family"};
            searchArgs.AddRange(args);
            List<SimObject> PSO = WorldSystem.GetPrimitives(searchArgs.ToArray(), out argsUsed);
            List<Primitive> PS = new List<Primitive>();
            WorldSystem.AsPrimitives(PS,PSO);
            if (IsEmpty(PS)) return Failure("Cannot find objects from " + string.Join(" ", args));
            Primitive rootPim = PS[0];
            foreach (Primitive ps in PS)
            {
                if (ps.ParentID == 0)
                {
                    rootPim = ps;
                }
            }
            TheBotClient.InvokeGUI(() =>
                                    {
                                        frmPrimWorkshop pw = new frmPrimWorkshop(TheBotClient.TheRadegastInstance,
                                                                                 rootPim.LocalID);
                                       // pw.LoadPrims(PS);
                                       // pw.
                                        pw.Show();
                                    });
            return Success(Name + " on " + PS.Count);
        }
    }
}