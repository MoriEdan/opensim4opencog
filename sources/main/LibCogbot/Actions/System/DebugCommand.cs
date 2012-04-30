using System;
using System.Collections.Generic;
using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.Packets;

using MushDLR223.ScriptEngines;

namespace cogbot.Actions.System
{
    public class DebugCommand : Command, SystemApplicationCommand
    {
        public DebugCommand(BotClient testClient)
        {
            Name = "debug";
            Description = "Turn debug messages on or off. Usage: debug [level] where level is one of None, Debug, Error, Info, Warn";
            Category = CommandCategory.BotClient;
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            if (args.Length == 0)
                return Success("Logging is " + Settings.LOG_LEVEL);

            args[0] = args[0].ToLower();
            int level = -1;
            foreach (var s in typeof(Helpers.LogLevel).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                level++;
                if (s.Name.ToLower().StartsWith(args[0]))
                {
                    Settings.LOG_LEVEL = (Helpers.LogLevel) s.GetValue(null);
                    MushDLR223.Utilities.TaskQueueHandler.DebugLevel = level;
                    return Success("Logging is set to " + Settings.LOG_LEVEL);
                }
            }
            return ShowUsage();// " debug [level] where level is one of None, Debug, Error, Info, Warn";
        }
    }
}
