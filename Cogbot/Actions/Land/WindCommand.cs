using System;
using OpenMetaverse;

namespace cogbot.Actions
{
    public class WindCommand : Command
    {
        public WindCommand(BotClient testClient)
        {
            Name = "wind";
            Description = "Displays current wind data";
            Category = CommandCategory.Simulator;
            Parameters = new [] {  new NamedParam(typeof(GridClient), null) };
        }

        public override string Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            // Get the agent's current "patch" position, where each patch of
            // wind data is a 16x16m square
            Vector3 agentPos = GetSimPosition();
            int xPos = (int)Utils.Clamp(agentPos.X, 0.0f, 255.0f) / 16;
            int yPos = (int)Utils.Clamp(agentPos.Y, 0.0f, 255.0f) / 16;

            Vector2 windSpeed = Client.Terrain.WindSpeeds[yPos * 16 + xPos];

            return "Local wind speed is " + windSpeed.ToString();
        }
    }
}
