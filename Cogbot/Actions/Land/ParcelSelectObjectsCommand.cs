using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using OpenMetaverse;

namespace cogbot.Actions
{
    public class ParcelSelectObjectsCommand : Command, RegionMasterCommand
    {
        public ParcelSelectObjectsCommand(BotClient testClient)
        {
            Name = "selectobjects";
            Description = "Displays a list of prim localIDs on a given parcel with a specific owner. Usage: selectobjects parcelID OwnerUUID";
            Category = CommandCategory.Parcel;
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            if (args.Length < 2)
                return ShowUsage();// " selectobjects parcelID OwnerUUID (use parcelinfo to get ID, use parcelprimowners to get ownerUUID)";
            int argsUsed;
            Simulator CurSim = TryGetSim(args, out argsUsed) ?? Client.Network.CurrentSim;
            int parcelID;
            UUID ownerUUID;

            int counter = 0;
            StringBuilder result = new StringBuilder();
            // test argument that is is a valid integer, then verify we have that parcel data stored in the dictionary
            if (Int32.TryParse(args[0], out parcelID) 
                && UUIDTryParse(args,1, out ownerUUID, out argsUsed))
            {
                AutoResetEvent wait = new AutoResetEvent(false);
                ParcelManager.ForceSelectObjects callback = delegate(Simulator simulator, List<uint> objectIDs, bool resetList)
                {
                    //result.AppendLine("New List: " + resetList.ToString());
                    for(int i = 0; i < objectIDs.Count; i++)
                    {
                        result.Append(objectIDs[i].ToString() + " ");
                        counter++;
                    }
                    //result.AppendLine("Got " + objectIDs.Count.ToString() + " Objects in packet");
                    if(objectIDs.Count < 251)
                        wait.Set();
                };

                Client.Parcels.OnParcelSelectedObjects += callback;
                Client.Parcels.SelectObjects(parcelID, (ObjectReturnType)16, ownerUUID);
                

                Client.Parcels.ObjectOwnersRequest(CurSim, parcelID);
                if (!wait.WaitOne(30000, false))
                {
                    result.AppendLine("Timed out waiting for packet.");
                }
                
                Client.Parcels.OnParcelSelectedObjects -= callback;
                result.AppendLine("Found a total of " + counter + " Objects");
                return Success(result.ToString());;
            }
            else
            {
                return Failure(string.Format("Unable to find Parcel {0} in Parcels Dictionary, Did you run parcelinfo to populate the dictionary first?", args[0]));
            }
        }
    }
}
