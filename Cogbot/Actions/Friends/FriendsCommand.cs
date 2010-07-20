using System;
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Packets;
using System.Text;

// the Namespace used for all BotClient commands
using MushDLR223.ScriptEngines;

namespace cogbot.Actions.Friends
{
    /// <summary>
    /// Shows a list of friends
    /// </summary>
    public class FriendsCommand : Command, BotPersonalCommand
    {        
        /// <summary>
        /// Constructor for FriendsCommand class
        /// </summary>
        /// <param name="testClient">A reference to the BotClient object</param>
        public FriendsCommand(BotClient testClient)
        {
            // The name of the command
            Name = "Friends List";
            // A short description of the command with usage instructions
            Description = "List avatar friends. Usage: friends";
            Category = CommandCategory.Friends;
            Parameters = new[] { new NamedParam(typeof(Avatar), typeof(UUID)) };
        }

        /// <summary>
        /// Get a list of current friends
        /// </summary>
        /// <param name="args">optional testClient command arguments</param>
        /// <param name="fromAgentID">The <seealso cref="OpenMetaverse.UUID"/> 
        /// of the agent making the request</param>
        /// <returns></returns>
        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            // initialize a StringBuilder object used to return the results
            return Success(ListFriends(Client));
        }

        static public String ListFriends(GridClient Client)
        {
            StringBuilder sb = new StringBuilder();

            // Only iterate the Friends dictionary if we actually have friends!
            if (Client.Friends.FriendList.Count > 0)
            {
                // iterate over the InternalDictionary using a delegate to populate
                // our StringBuilder WriteLine string
                Client.Friends.FriendList.ForEach(delegate(FriendInfo friend)
                                                      {
                                                          // append the name of the friend to our WriteLine
                                                          sb.AppendLine(friend.Name);
                                                      });
            }
            else
            {
                // we have no friends :(
                sb.AppendLine("No Friends");   
            }

            // return the result
            return sb.ToString();
        }
    }
}
