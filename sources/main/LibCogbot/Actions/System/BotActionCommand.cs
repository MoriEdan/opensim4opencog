﻿using System;
using cogbot.TheOpenSims;
using MushDLR223.ScriptEngines;
using OpenMetaverse;

namespace cogbot.Actions.System
{
    public class BotActionCommand : Command, BotPersonalCommand
    {
        public BotActionCommand(BotClient testClient)
        {
            Name = "botact";
            Description = "Invoke a command a bot interuptable action (interupts previous foreground action).  Usage: botact anim KISS";
            Category = CommandCategory.BotClient;
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            if (args.Length < 1) return ShowUsage();
            string botcmd = String.Join(" ", args, 0, args.Length).Trim();
            TheSimAvatar.CurrentAction = new CommandAction(TheSimAvatar, botcmd);
            return Success(string.Format("{0} CurrentAction = {1}", TheSimAvatar, botcmd));
        }
    }
}