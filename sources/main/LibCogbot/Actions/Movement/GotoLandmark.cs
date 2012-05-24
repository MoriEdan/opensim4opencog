using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;
using OpenMetaverse.Packets;
using PathSystem3D.Navigation;

using MushDLR223.ScriptEngines;

namespace cogbot.Actions.Movement
{
    public class GotoLandmarkCommand : Command, BotPersonalCommand
    {
        public GotoLandmarkCommand(BotClient testClient)
        {
            Name = "goto_landmark";
            Description = "Teleports to a Landmark. Usage: goto_landmark [UUID]";
            Category = CommandCategory.Movement;
            Parameters = new[] { new NamedParam(typeof(InventoryLandmark), typeof(UUID)) };
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            if (args.Length < 1)
            {
                return ShowUsage();// " goto_landmark [UUID]";
            }

            UUID landmark = UUID.Zero;
            int argsUsed;
            if (!UUIDTryParse(args, 0, out landmark, out argsUsed))
            {
                return Failure("Invalid LLUID");
            }
            else
            {
                WriteLine("Teleporting to " + landmark.ToString());
            }
            if (Client.Self.Teleport(landmark))
            {
                return Success("Teleport Succesful");
            }
            else
            {
                return Failure("Teleport Failed");
            }
        }
    }
}
