using System;
using System.Threading;
using OpenMetaverse;
// older LibOMV
//using AgentFlags = OpenMetaverse.AgentManager.AgentFlags;
//using AgentState = OpenMetaverse.AgentManager.AgentState;

using MushDLR223.ScriptEngines;

namespace cogbot.Actions.Movement
{
    class RightCommand : Command, BotPersonalCommand
    {
        public RightCommand(BotClient client)
        {
            Name = "right";
            Description = "Sends the move right command to the server for a single packet or a given number of seconds.";
            Parameters =
                CreateParams(Optional("seconds", typeof(TimeSpan), "timespan to move for"));
            Details = AddUsage(Parameters, Description);
            Category = CommandCategory.Movement;
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            if (args.Length > 1)
                return ShowUsage();// " right [seconds]";

            if (args.Length == 0)
            {
                Client.Self.Movement.SendManualUpdate(AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG, Client.Self.Movement.Camera.Position,
                    Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis, Client.Self.Movement.Camera.UpAxis,
                    Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation, Client.Self.Movement.Camera.Far, AgentFlags.None,
                    AgentState.None, true);
            }
            else
            {
                // Parse the number of seconds
                int duration;
                if (!Int32.TryParse(args[0], out duration))
                    return ShowUsage();// " right [seconds]";
                // Convert to milliseconds
                duration *= 1000;

                DateTime start = DateTime.Now;

                Client.Self.Movement.LeftNeg = true;

                while (DateTime.Now.Subtract(start).TotalMilliseconds < duration)
                {
                    // The movement timer will do this automatically, but we do it here as an example
                    // and to make sure updates are being sent out fast enough
                    Client.Self.Movement.SendUpdate(false);
                    Thread.Sleep(100);
                }

                Client.Self.Movement.LeftNeg = false;
            }

            return Success("Moved right");
        }
    }
}
