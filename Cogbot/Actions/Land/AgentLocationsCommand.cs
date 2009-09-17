using System;
using System.Collections.Generic;
using System.Text;
using cogbot.TheOpenSims;
using OpenMetaverse;

namespace cogbot.Actions
{
    /// <summary>
    /// Display a list of all agent locations in a specified region
    /// </summary>
    public class AgentLocationsCommand : Command, GridMasterCommand
    {
        public AgentLocationsCommand(BotClient testClient)
        {
            Name = "agentlocations";
            Description = "Downloads all of the agent locations in a specified region. Usage: agentlocations [regionhandle]";
            Category = CommandCategory.Simulator;
            Parameters = new [] { new NamedParam(typeof(SimRegion), typeof(ulong)) };

        }

        public override string Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            ulong regionHandle;

            if (args.Length == 0)
                regionHandle = Client.Network.CurrentSim.Handle;
            else if (!(args.Length == 1 && UInt64.TryParse(args[0], out regionHandle)))
                return "Usage: agentlocations [regionhandle]";

            List<GridItem> items = Client.Grid.MapItems(regionHandle, GridItemType.AgentLocations, 
                GridLayerType.Objects, 1000 * 20);

            if (items != null)
            {
                StringBuilder ret = new StringBuilder();
                ret.AppendLine("Agent locations:");

                for (int i = 0; i < items.Count; i++)
                {
                    GridAgentLocation location = (GridAgentLocation)items[i];

                    ret.AppendLine(String.Format("{0} avatar(s) at {1},{2}", location.AvatarCount, location.LocalX,
                        location.LocalY));
                }

                return ret.ToString();
            }
            else
            {
                return "Failed to fetch agent locations";
            }
        }
    }
}
