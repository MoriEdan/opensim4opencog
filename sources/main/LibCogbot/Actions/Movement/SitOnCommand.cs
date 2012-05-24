using System;
using System.Collections.Generic;
using System.Text;
using cogbot.TheOpenSims;
using OpenMetaverse;
using OpenMetaverse.Packets;

using MushDLR223.ScriptEngines;

namespace cogbot.Actions.Movement
{
    public class SitOnCommand : Command, BotPersonalCommand
    {
        public SitOnCommand(BotClient testClient)
        {
            Name = "Sit On";
            Description = "Attempt to sit on a particular prim, with specified UUID";
            Category = CommandCategory.Movement;
            Parameters = new[] {  new NamedParam(typeof(SimObject), typeof(UUID)) };  
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            if (args.Length < 1)
                return ShowUsage();// " siton UUID";

            int argsUsed;
            List<SimObject> PS = WorldSystem.GetPrimitives(args, out argsUsed);
            if (IsEmpty(PS)) return Failure("Cannot find objects from " + args.str);
            foreach (var targetPrim in PS)
            {
                WorldSystem.TheSimAvatar.SitOn(targetPrim);
                Success("Requested to sit on prim " + targetPrim.ID.ToString() +
                       " (" + targetPrim.LocalID + ")");
            }
            return SuccessOrFailure();
        }


    }
}
