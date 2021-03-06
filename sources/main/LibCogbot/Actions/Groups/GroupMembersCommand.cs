using System;
using System.Collections.Generic;
using System.Threading;
using MushDLR223.Utilities;
using OpenMetaverse;
using OpenMetaverse.Packets;
using System.Text;
using MushDLR223.ScriptEngines;

namespace Cogbot.Actions.Groups
{
    /// <summary>
    /// dumps group members to console
    /// </summary>
    public class GroupMembersCommand : Command, GridMasterCommand
    {
        private ManualResetEvent GroupsEvent = new ManualResetEvent(false);
        private string GroupName;
        private UUID GroupUUID = UUID.Zero;
        private UUID GroupRequestID = UUID.Zero;

        public GroupMembersCommand(BotClient testClient)
        {
            Name = "groupmembers";
            TheBotClient = testClient;
        }

        public override void MakeInfo()
        {
            Description = "Dump group members to console.";
            Category = CommandCategory.Groups;
            Details = AddUsage(Name + " group", Description);
            Parameters = CreateParams("group", typeof (Group), "group you are going to see " + Name);
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            if (args.Length < 1)
                return ShowUsage();

            GroupName = String.Empty;
            for (int i = 0; i < args.Length; i++)
                GroupName += args[i] + " ";
            GroupName = GroupName.Trim();

            GroupUUID = Client.GroupName2UUID(GroupName);
            if (UUID.Zero != GroupUUID)
            {
                Client.Groups.GroupMembersReply += GroupMembersHandler;
                GroupRequestID = Client.Groups.RequestGroupMembers(GroupUUID);
                GroupsEvent.WaitOne(30000, false);
                GroupsEvent.Reset();
                Client.Groups.GroupMembersReply -= GroupMembersHandler;
                return Success(Client.ToString() + " got group members");
            }
            return Failure(Client.ToString() + " doesn't seem to be member of the group " + GroupName);
        }

        private void GroupMembersHandler(object sender, GroupMembersReplyEventArgs e)
        {
            if (e.RequestID == GroupRequestID)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendFormat("GroupMembers: RequestID {0}", e.RequestID).AppendLine();
                sb.AppendFormat("GroupMembers: GroupUUID {0}", GroupUUID).AppendLine();
                sb.AppendFormat("GroupMembers: GroupName {0}", GroupName).AppendLine();
                if (e.Members.Count > 0)
                    foreach (KeyValuePair<UUID, GroupMember> member in e.Members)
                        sb.AppendFormat("GroupMembers: MemberUUID {0}", member.Key.ToString()).AppendLine();
                sb.AppendFormat("GroupMembers: MemberCount {0}", e.Members.Count).AppendLine();
                WriteLine(sb.ToString());
                GroupsEvent.Set();
            }
        }
    }
}