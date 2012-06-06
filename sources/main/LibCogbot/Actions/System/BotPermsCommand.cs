using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Packets;
using Cogbot.World;

using MushDLR223.ScriptEngines;

namespace Cogbot.Actions.System
{
    public class BotPermsCommand : Command, BotSystemCommand
    {
        public BotPermsCommand(BotClient testClient)
        {
            Name = "botperms";
            Description = "Sets the bot use permissions. Usage: botperms name [Base] [Owner] [Group] [Ignore]";
            Category = CommandCategory.Security;
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            if (args.Length < 1)
            {
                int nfound = 0;
                foreach (var sl in TheBotClient.SecurityLevels)
                {
                    nfound++;
                    Success(string.Format("{0}={1}", sl.Key, sl.Value));
                }
                foreach (var sl in TheBotClient.SecurityLevelsByName)
                {
                    nfound++;
                    Success(string.Format("{0}={1}", sl.Key, sl.Value));
                }
                return Success(nfound + " entries found");
            }
            int argsUsed;
            List<SimObject> worldSystemGetPrimitives = WorldSystem.GetPrimitives(args, out argsUsed);
            if (IsEmpty(worldSystemGetPrimitives))
            {
                return Failure("Cannot find objects from " + args.str);
            }
            BotPermissions who = BotPermissions.Stranger;

            object value;
            if (TryEnumParse(typeof (BotPermissions), args, argsUsed, out argsUsed, out value))
            {
                who = (BotPermissions) value;
            }

            foreach (var p in worldSystemGetPrimitives)
            {

                BotPermissions perms = TheBotClient.GetSecurityLevel(p.ID, null);
                if (argsUsed==0)
                {
                    Success("Perms for " + p + " was " + perms);
                    continue;    
                }
                Success("Perms for " + p + " was " + perms + " now setting to " + who);
                TheBotClient.SetSecurityLevel(p.ID, null, who);
            }
            return SuccessOrFailure();
        }
    }
}
