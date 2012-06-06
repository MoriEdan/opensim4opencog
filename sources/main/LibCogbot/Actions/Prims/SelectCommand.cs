using System.Collections.Generic;
using Cogbot;
using Cogbot.World;
using Cogbot.Utilities;
using MushDLR223.Utilities;
using OpenMetaverse;
using PathSystem3D.Navigation;

using MushDLR223.ScriptEngines;

namespace Cogbot.Actions.Objects
{
    public class SelectCommand : Cogbot.Actions.Command, BotPersonalCommand
    {
        public SelectCommand(BotClient client)
        {
            Name = "select";
            Description = "selects one or more object in world.";
            Details = AddUsage("select +/-[prim0] +/-[prim1] +/-[prim2]", "Selects or deslects prims") +
                      AddUsage("select none", "Clears the select buffer") +
                      AddUsage("select", "Shows the select buffer");
            Category = Cogbot.Actions.CommandCategory.Objects;
            Parameters = new[] { new NamedParam(typeof(SimObject), typeof(UUID)) };
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            ListAsSet<SimPosition> objs = TheSimAvatar.GetSelectedObjects();

            if (args.Length == 0)
            {
                foreach (var o in objs)
                {
                    WriteLine(" " + o);
                }
            }
            if (args.Length == 1 && args[0].ToLower() == "none")
            {
                objs.Clear();
                bool was = TheSimAvatar.SelectedBeam;
                TheSimAvatar.SelectedBeam = !was;
                TheSimAvatar.SelectedBeam = was;
            }
            else
            {
                int used = 0;
                bool remove = false;
                while (used < args.Length)
                {
                    args = Parser.SplitOff(args, used);
                    string s = args[0];
                    if (s.StartsWith("-"))
                    {
                        remove = true;
                        s = s.Substring(1);
                    }
                    if (s.StartsWith("+"))
                    {
                        remove = false;
                        s = s.Substring(1);
                    }
                    if (s.Length < 0)
                    {
                        used = 1;
                        continue;
                    }
                    args[0] = s;
                    List<SimObject> PS = WorldSystem.GetPrimitives(args, out used);
                    foreach (var P in PS)
                    {
                        if (P == null)
                        {
                            WriteLine("Cannot find " + s);
                            used = 1;
                            continue;
                        }
                        if (remove)
                        {
                            WriteLine("Removing " + P);
                            TheSimAvatar.SelectedRemove(P);
                        }
                        else
                        {
                            WriteLine("Adding " + P);
                            TheSimAvatar.SelectedAdd(P);
                        }
                    }
                    if (used == 0) break;
                }
            }
            return Success("selected objects count=" + objs.Count);
        }
    }
}