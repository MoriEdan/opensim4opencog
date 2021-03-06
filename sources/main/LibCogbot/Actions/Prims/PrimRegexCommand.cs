using System;
using System.Text.RegularExpressions;
using OpenMetaverse;
using MushDLR223.ScriptEngines;

namespace Cogbot.Actions.Objects
{
    public class PrimRegexCommand : Command, RegionMasterCommand, AsynchronousCommand
    {
        public PrimRegexCommand(BotClient testClient)
        {
            Name = "primregex";
        }

        public override void MakeInfo()
        {
            Description = "Find prim by text predicat. " +
                          "Usage: primregex [text predicat] (eg findprim .away.)";
            Category = CommandCategory.Objects;
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            if (args.Length < 1)
                return ShowUsage(); // " primregex [text predicat]";

            try
            {
                // Build the predicat from the args list
                string predicatPrim = string.Empty;
                for (int i = 0; i < args.Length; i++)
                    predicatPrim += args[i] + " ";
                predicatPrim = predicatPrim.TrimEnd();

                // Build Regex
                Regex regexPrimName = new Regex(predicatPrim.ToLower());
                int argsUsed;
                Simulator CurSim = TryGetSim(args, out argsUsed) ?? Client.Network.CurrentSim;
                // Print result
                Logger.Log(string.Format("Searching prim for [{0}] ({1} prims loaded in simulator)\n", predicatPrim,
                                         CurSim.ObjectsPrimitives.Count), Helpers.LogLevel.Info, Client);

                CurSim.ObjectsPrimitives.ForEach(
                    delegate(Primitive prim)
                        {
                            if (prim.Text != null && regexPrimName.IsMatch(prim.Text.ToLower()))
                            {
                                Logger.Log(
                                    string.Format("\nNAME={0}\nID = {1}\nFLAGS = {2}\nTEXT = '{3}'\nDESC='{4}",
                                                  prim.Properties.Name,
                                                  prim.ID, prim.Flags.ToString(), prim.Text, prim.Properties.Description),
                                    Helpers.LogLevel.Info, Client);
                            }
                            else if (prim.Properties.Name != null &&
                                     regexPrimName.IsMatch(prim.Properties.Name.ToLower()))
                            {
                                Logger.Log(
                                    string.Format("\nNAME={0}\nID = {1}\nFLAGS = {2}\nTEXT = '{3}'\nDESC='{4}",
                                                  prim.Properties.Name,
                                                  prim.ID, prim.Flags.ToString(), prim.Text,
                                                  prim.Properties.Description), Helpers.LogLevel.Info, Client);
                            }
                            else if (prim.Properties.Description != null &&
                                     regexPrimName.IsMatch(prim.Properties.Description.ToLower()))
                            {
                                Logger.Log(
                                    string.Format("\nNAME={0}\nID = {1}\nFLAGS = {2}\nTEXT = '{3}'\nDESC='{4}",
                                                  prim.Properties.Name,
                                                  prim.ID, prim.Flags.ToString(), prim.Text,
                                                  prim.Properties.Description), Helpers.LogLevel.Info, Client);
                            }
                        }
                    );
            }
            catch (Exception e)
            {
                Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                return Failure("Error searching");
            }

            return Success("Done searching");
        }
    }
}