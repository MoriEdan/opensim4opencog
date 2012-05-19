﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using cogbot;
using cogbot.Actions;
using OpenMetaverse;

// the Namespace used for all BotClient commands
using MushDLR223.ScriptEngines;

namespace cogbot.Actions.Search
{
    class SearchPlacesCommand : Command, GridMasterCommand
    {
        AutoResetEvent waitQuery = new AutoResetEvent(false);
        int resultCount;

        public SearchPlacesCommand(BotClient testClient)
        {
            Name = "searchplaces";
            Description = "Searches Places. Usage: searchplaces [search text]";
            Category = CommandCategory.Other;
            Parameters =
                NamedParam.CreateParams("searchText", typeof (string), "what you are searching for");
            ResultMap = NamedParam.CreateParams(
                "result", typeof (List<string>), "search results",
                "message", typeof (string), "if success was false, the reason why",
                "success", typeof (bool), "true if command was successful");
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            if (args.Length < 1)
                return ShowUsage();// " searchplaces [search text]";

            string searchText = string.Empty;
            for (int i = 0; i < args.Length; i++)
                searchText += args[i] + " ";
            searchText = searchText.TrimEnd();
            waitQuery.Reset();

            StringBuilder result = new StringBuilder();

            EventHandler<PlacesReplyEventArgs> callback = delegate(object sender, PlacesReplyEventArgs e)
            {
                result.AppendFormat("Your search string '{0}' returned {1} results" + Environment.NewLine,
                    searchText, e.MatchedPlaces.Count);
                foreach (DirectoryManager.PlacesSearchData place in e.MatchedPlaces)
                {
                    result.AppendLine(place.ToString());
                }

                waitQuery.Set();
            };

            Client.Directory.PlacesReply += callback;
            Client.Directory.StartPlacesSearch(searchText);            

            if (!waitQuery.WaitOne(20000, false) && Client.Network.Connected)
            {
                result.AppendLine("Timeout waiting for simulator to respond to query.");
            }

            Client.Directory.PlacesReply -= callback;

            return Success(result.ToString());;
        }
    }
}
