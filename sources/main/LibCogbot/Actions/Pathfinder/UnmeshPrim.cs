using System.Collections.Generic;
using cogbot.TheOpenSims;
using OpenMetaverse;

using MushDLR223.ScriptEngines;

namespace cogbot.Actions.Pathfinder
{
    public class UnmeshPrim : cogbot.Actions.Command, SystemApplicationCommand
    {
        public UnmeshPrim(BotClient client)
        {
            Name = GetType().Name;
            Description = "Unmeshes all prims and removes collision planes. Usage: UnmeshPrim [prims] ";
            Category = cogbot.Actions.CommandCategory.Movement;
            Parameters = new[] {  new NamedParam(typeof(SimObject), typeof(UUID)) };
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            int argsUsed;
            ICollection<SimObject> objs = WorldSystem.GetPrimitives(args, out argsUsed);
            bool rightNow = true;
            if (argsUsed == 0)
            {
                objs = (ICollection<SimObject>) WorldSystem.GetAllSimObjects();
                rightNow = false;
            }
            WriteLine("Unmeshing " + objs.Count);
            foreach (SimObject o2 in objs)
            {
                SimObjectPathFinding o = o2.PathFinding;

                o.IsWorthMeshing = true;
                if (rightNow)
                {
                    o.RemoveCollisions();
                }
                else
                {
                    o.RemoveCollisions();
                }
            }
            if (rightNow)
            {
                TheSimAvatar.GetSimRegion().GetPathStore(TheSimAvatar.SimPosition).RemoveAllCollisionPlanes();
            }
            else
            {
                TheSimAvatar.GetSimRegion().GetPathStore(TheSimAvatar.SimPosition).RemoveAllCollisionPlanes();
            }

            return TheBotClient.ExecuteCommand("meshinfo", fromAgentID, WriteLine);
        }
    }
}