using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace cogbot.Actions
{
    public class TreeCommand: Command
    {
        public TreeCommand(BotClient testClient)
		{
			Name = "tree";
			Description = "Rez a tree.";
            Category = CommandCategory.Objects;
		}

        public override string Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
		{
		    if (args.Length == 1)
		    {
		        try
		        {
		            string treeName = args[0].Trim(new char[] { ' ' });
		            Tree tree = (Tree)Enum.Parse(typeof(Tree), treeName);

		            Vector3 treePosition = GetSimPosition();
		            treePosition.Z += 3.0f;

		            Client.Objects.AddTree(Client.Network.CurrentSim, new Vector3(0.5f, 0.5f, 0.5f),
		                Quaternion.Identity, treePosition, tree, TheBotClient.GroupID, false);

		            return "Attempted to rez a " + treeName + " tree";
		        }
		        catch (Exception)
		        {
		         //   return "Type !tree for usage";
		        }
		    }

		    string usage = "Usage: !tree [";
		    foreach (string value in Enum.GetNames(typeof(Tree)))
		    {
		        usage += value + ",";
		    }
		    usage = usage.TrimEnd(new char[] { ',' });
		    usage += "]";
		    return usage;
		}
    }
}
