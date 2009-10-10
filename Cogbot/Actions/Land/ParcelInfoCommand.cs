using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using OpenMetaverse;

namespace cogbot.Actions
{
    public class ParcelInfoCommand : Command, RegionMasterCommand
    {
        private AutoResetEvent ParcelsDownloaded = new AutoResetEvent(false);

        NetworkManager.DisconnectedCallback callback;

        public ParcelInfoCommand(BotClient testClient)
        {
            Name = "parcelinfo";
            Description = "Prints out info about all the parcels in this simulator";
            Category = CommandCategory.Parcel;

            callback = new NetworkManager.DisconnectedCallback(Network_OnDisconnected);
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            StringBuilder sb = new StringBuilder();
            string result;
            Client.Network.OnDisconnected += callback;
            ParcelManager.SimParcelsDownloaded del =
                delegate(Simulator simulator, InternalDictionary<int, Parcel> simParcels, int[,] parcelMap)
                {
                    ParcelsDownloaded.Set();
                };

            ParcelsDownloaded.Reset();
            Client.Parcels.OnSimParcelsDownloaded += del;
            try
            {

                Client.Parcels.RequestAllSimParcels(Client.Network.CurrentSim);

                if (Client.Network.CurrentSim.IsParcelMapFull())
                    ParcelsDownloaded.Set();

                if (ParcelsDownloaded.WaitOne(30000, false) && Client.Network.Connected)
                {
                    sb.AppendFormat("Downloaded {0} Parcels in {1} " + System.Environment.NewLine,
                                    Client.Network.CurrentSim.Parcels.Count, Client.Network.CurrentSim.Name);

                    Client.Network.CurrentSim.Parcels.ForEach(delegate(Parcel parcel)
                                                                  {
                                                                      sb.AppendFormat(
                                                                          "Parcel[{0}]: Name: \"{1}\", Description: \"{2}\" ACLBlacklist Count: {3}, ACLWhiteList Count: {5} Traffic: {4}" +
                                                                          System.Environment.NewLine,
                                                                          parcel.LocalID, parcel.Name, parcel.Desc,
                                                                          parcel.AccessBlackList.Count, parcel.Dwell,
                                                                          parcel.AccessWhiteList.Count);
                                                                      //foreach (ParcelManager.ParcelAccessEntry white in parcel.AccessWhiteList)
                                                                      //{
                                                                      //    if(white.AgentID != UUID.Zero)
                                                                      //        sb.AppendFormat("\tAllowed Avatar {0}" + System.Environment.NewLine, white.AgentID);
                                                                      //}
                                                                      //foreach (ParcelManager.ParcelAccessEntry black in parcel.AccessBlackList)
                                                                      //{
                                                                      //    if(black.AgentID != UUID.Zero)
                                                                      //        sb.AppendFormat("\t Banned Avatar {0}" + System.Environment.NewLine, black.AgentID);
                                                                      //}
                                                                  });

                    return Success(sb.ToString());
                }
                else
                    return Failure("Failed to retrieve information on all the simulator parcels");
            }
            finally
            {
                Client.Parcels.OnSimParcelsDownloaded -= del;
                Client.Network.OnDisconnected -= callback;
            }

        }

        void Network_OnDisconnected(NetworkManager.DisconnectType reason, string message)
        {
            ParcelsDownloaded.Set();
        }
    }
}
