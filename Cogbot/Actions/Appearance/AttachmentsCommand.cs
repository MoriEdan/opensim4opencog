using System;
using System.Collections.Generic;
using OpenMetaverse;

namespace cogbot.Actions
{
    public class AttachmentsCommand : Command
    {
        public AttachmentsCommand(BotClient testClient)
        {
            TheBotClient = testClient;
            Name = "attachments";
            Description = "Prints a list of the currently known agent attachments";
            Category = CommandCategory.Appearance;
        }

        public override string Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            List<Primitive> attachments = Client.Network.CurrentSim.ObjectsPrimitives.FindAll(
                delegate(Primitive prim) { return prim.ParentID == Client.Self.LocalID; }
            );

            for (int i = 0; i < attachments.Count; i++)
            {
                Primitive prim = attachments[i];
                AttachmentPoint point = prim.PrimData.AttachmentPoint;

                // TODO: Fetch properties for the objects with missing property sets so we can show names
                Logger.Log(String.Format("[Attachment @ {0}] LocalID: {1} UUID: {2} Offset: {3}",
                    point, prim.LocalID, prim.ID, prim.Position), Helpers.LogLevel.Info, Client);
            }

            return "Found " + attachments.Count + " attachments";
        }
    }
}
