using System;
using System.Collections.Generic;
using Cogbot.World;
using OpenMetaverse;

using MushDLR223.ScriptEngines;

namespace Cogbot.Actions.Objects
{
    public class PrimInfoCommand : Command, RegionMasterCommand
    {
        public PrimInfoCommand(BotClient testClient)
        {
            Name = "priminfo";
            Description = "Dumps information about a specified prim. " + "Usage: priminfo [prim-uuid]";
            Category = CommandCategory.Objects;
            Parameters = new[] { new NamedParam(typeof(SimObject), typeof(UUID)) };
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            UUID primID = UUID.Zero;

            if (args.Length < 1)
            {
                foreach (SimObject O in WorldSystem.TheSimAvatar.GetNearByObjects(10, true))
                {
                    WriteLine("\n {0}", WorldSystem.describePrim(O.Prim, false));
                }
                return Success("Done.");
            }

            int argsUsed;
            List<SimObject> PS = WorldSystem.GetPrimitives(args, out argsUsed);
            if (IsEmpty(PS)) return Failure("Cannot find objects from " + args.str);
            foreach (var target in PS)
            {
                WriteLine("\n {0}", WorldSystem.describePrim(target.Prim, true));
                AddSuccess("Done.");
            }
            return SuccessOrFailure();
        }
    }
}
