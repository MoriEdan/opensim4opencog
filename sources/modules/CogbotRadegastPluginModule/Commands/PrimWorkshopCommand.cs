using System.Collections.Generic;
using Cogbot;
using Cogbot.World;
using OpenMetaverse;
using Radegast;

using MushDLR223.ScriptEngines;
using Radegast.Rendering;

namespace Cogbot.Actions
{
    public class PrimWorkshopCommand : Cogbot.Actions.Command, RegionMasterCommand, GUICommand
    {
        public PrimWorkshopCommand(BotClient client)
        {
            Name = "Prim Workshop";
        }

        public override void MakeInfo()
        {
            Description = "Runs PrimWorkshop on a prim. Usage: PrimWorkshop [prim]";
            Category = Cogbot.Actions.CommandCategory.Objects;
            Parameters = CreateParams("targets", typeof(PrimSpec), "The targets of " + Name);
        }

        public override CmdResult ExecuteRequest(CmdRequest args0)
        {
            var args = args0.GetProperty("targets");
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