using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cogbot;
using Cogbot.Actions;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using PathSystem3D.Navigation;
using MushDLR223.ScriptEngines;

namespace Cogbot.Actions.SimExport
{
    public class XferCommand : Command, GridMasterCommand
    {
        private const int FETCH_ASSET_TIMEOUT = 1000*10;

        public XferCommand(BotClient testClient)
        {
            Name = "xfer";
        }

        public override void MakeInfo()
        {
            Description = "Downloads the specified asset using the Xfer system. Usage: xfer [uuid]";
            Category = CommandCategory.Inventory;
            Parameters = CreateParams("position", typeof (SimPosition), "the location you wish to " + Name);
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            UUID assetID = UUID.Zero;

            if (args.Length != 1 || !UUID.TryParse(args[0], out assetID))
                return ShowUsage(); // " xfer [uuid]";

            string filename;
            byte[] assetData = RequestXfer(assetID, AssetType.Object, out filename);

            if (assetData != null)
            {
                try
                {
                    File.WriteAllBytes(filename, assetData);
                    return Success("Saved asset " + filename);
                }
                catch (Exception ex)
                {
                    return Failure("failed to save asset " + filename + ": " + ex.Message);
                }
            }
            else
            {
                return Failure("failed to xfer asset " + assetID);
            }
        }

        private byte[] RequestXfer(UUID assetID, AssetType type, out string filename)
        {
            AutoResetEvent xferEvent = new AutoResetEvent(false);
            ulong xferID = 0;
            byte[] data = null;

            EventHandler<XferReceivedEventArgs> xferCallback =
                delegate(object sender, XferReceivedEventArgs e)
                    {
                        if (e.Xfer.XferID == xferID)
                        {
                            if (e.Xfer.Success)
                                data = e.Xfer.AssetData;
                            xferEvent.Set();
                        }
                    };

            Client.Assets.XferReceived += xferCallback;

            filename = assetID + ".asset";
            xferID = Client.Assets.RequestAssetXfer(filename, false, true, assetID, type, false);

            xferEvent.WaitOne(FETCH_ASSET_TIMEOUT, false);

            Client.Assets.XferReceived -= xferCallback;

            return data;
        }
    }
}