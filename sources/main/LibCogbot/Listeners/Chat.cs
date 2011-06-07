using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse; //using libsecondlife;

namespace cogbot.Listeners
{
    public class Chat : Listener
    {
        public List<string> muteList;
		public bool muted = false;

        public Chat(BotClient parent) : base(parent) {
            muteList = new List<string>();

            client.Self.OnChat += new AgentManager.ChatCallback(Self_OnChat);
        }

        void Self_OnChat(string message, ChatAudibleLevel audible, ChatType type, 
            ChatSourceType sourceType, string fromName, UUID id, UUID ownerid, 
            Vector3 position)
        {
            BotClient parent = client;
            if (message.Length > 0 && sourceType == ChatSourceType.Agent && !muteList.Contains(fromName))
            {
                parent.WriteLine(fromName + " says, \"" + message + "\".");
                //parent.enqueueLispTask("(thisClient.msgClient \"(heard (" + fromName + ") '" + message + "' )\" )");
                parent.enqueueLispTask("(on-chat (@\"" + fromName + "\") (@\"" + message + "\") )");
            }
        }
    }
}
