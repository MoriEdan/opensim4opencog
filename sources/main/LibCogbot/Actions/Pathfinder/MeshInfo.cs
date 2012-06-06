using System;
using System.Collections;
using System.Collections.Generic;
using Cogbot;
using Cogbot.World;
using OpenMetaverse;

using MushDLR223.ScriptEngines;

namespace Cogbot.Actions.Pathfinder
{
    class meshinfo : Cogbot.Actions.Command, SystemApplicationCommand, BotStatefullCommand
    {
        public meshinfo(BotClient client)
        {
            Name = GetType().Name;
            Description = "Shows meshinfo";
            Category = Cogbot.Actions.CommandCategory.Movement;
            Parameters = new[] {  new NamedParam(typeof(SimObject), typeof(UUID)) };
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            int argsUsed;
            if (!WorldPathSystem.MaintainMeshes) return Success("WorldObjects.MaintainMeshes = false for " + Name);
            IEnumerable<SimObject> objs = WorldSystem.GetPrimitives(args, out argsUsed);
            if (argsUsed == 0)
            {

                objs = WorldSystem.GetAllSimObjects();
                int meshed = 0;
                int unmeshed = 0;
                int notNeedBeMEshed = 0;
                foreach (var o2 in objs)
                {
                    SimObjectPathFinding o = o2.PathFinding;
                    if (o.IsMeshed)
                    {
                        meshed++;
                        continue;
                    }
                    if (o.IsWorthMeshing)
                    {
                        unmeshed++;
                        continue;
                    }
                    notNeedBeMEshed++;
                }
                float total = meshed + unmeshed;
                float totalAll = meshed + unmeshed + notNeedBeMEshed;

                return
                    Success(
                        string.Format("IsMeshed/UnMeshed/UnNeeded = {0}/{1}/{2} {3:0%} complete for {4:0%} of Sim {5}",
                                      meshed, unmeshed, notNeedBeMEshed,
                                      meshed/total,
                                      total/totalAll,
                                      Name));
            }
            foreach (SimObject o in objs)
            {
                WriteLine("MeshInfo: " + o);              
                WriteLine(o.PathFinding.Mesh.DebugString());
            }
            return Success("Ran " + Name);
        }

        #region Implementation of IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}