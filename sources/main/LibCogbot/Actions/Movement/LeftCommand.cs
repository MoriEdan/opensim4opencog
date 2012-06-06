using System;
using System.Threading;
using OpenMetaverse;
// older LibOMV
//using AgentFlags = OpenMetaverse.AgentManager.AgentFlags;
//using AgentState = OpenMetaverse.AgentManager.AgentState;

using MushDLR223.ScriptEngines;

namespace Cogbot.Actions.Movement
{
    class LeftCommand : Command, BotPersonalCommand
    {
        public LeftCommand(BotClient client)
        {
            Name = "left";
            Description = "Sends the move left command to the server for a single packet or a given number of seconds.";
            Parameters =
                CreateParams(Optional("seconds", typeof(TimeSpan), "timespan to move for"));
            Details = AddUsage(Parameters, Description);
            Category = CommandCategory.Movement;
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            if (args.Length > 1)
                return ShowUsage();// " left [seconds]";

            if (args.Length == 0)
            {
                Client.Self.Movement.SendManualUpdate(AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS, Client.Self.Movement.Camera.Position,
                    Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis, Client.Self.Movement.Camera.UpAxis,
                    Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation, Client.Self.Movement.Camera.Far, AgentFlags.None,
                    AgentState.None, true);
            }
            else
            {
                // Parse the number of seconds
                int duration;
                if (!Int32.TryParse(args[0], out duration))
                    return ShowUsage();// " left [seconds]";
                // Convert to milliseconds
                duration *= 1000;

                DateTime start = DateTime.Now;

                Client.Self.Movement.LeftPos = true;

                while (DateTime.Now.Subtract(start).TotalMilliseconds < duration)
                {
                    // The movement timer will do this automatically, but we do it here as an example
                    // and to make sure updates are being sent out fast enough
                    Client.Self.Movement.SendUpdate(false);
                    Thread.Sleep(100);
                }

                Client.Self.Movement.LeftPos = false;
            }

            return Success("Moved left");
        }
    }
}
