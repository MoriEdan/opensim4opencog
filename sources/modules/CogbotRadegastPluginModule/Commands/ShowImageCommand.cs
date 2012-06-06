﻿using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Cogbot;
using OpenMetaverse;
using OpenMetaverse.Assets;
using Radegast;

using MushDLR223.ScriptEngines;

namespace Cogbot.Actions.SimExport
{
    public class ShowImageCommand : Command, GridMasterCommand
    {

        public ShowImageCommand(BotClient testClient)
        {
            Name = "ShowImage";
            Description = "Shows the specified image. Usage: ShowImage [uuid]";
            Category = CommandCategory.Inventory;
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            if (args.Length < 1) return ShowUsage();
            UUID AssetID = UUID.Zero;
            int argsUsed;

            if (!UUIDTryParse(args, 0, out AssetID, out argsUsed))
            {
                return Failure("Cant get asset from " + AssetID);
            }

            TheBotClient.InvokeGUI(() =>
                                    {
                                        var v = new SLImageHandler(TheBotClient.TheRadegastInstance, AssetID,
                                                                   AssetID.ToString()) {Dock = DockStyle.Fill};
                                        var f = new RadegastForm();
                                        f.Controls.Add(v);
                                        f.Show();
                                    });
            
            return Success("Done RequestAsset " + AssetID);
        }
    }
}
