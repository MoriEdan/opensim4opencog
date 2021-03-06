using System;
using System.Threading;
using MushDLR223.ScriptEngines;
using PrologScriptEngine;
using SbsSW.SwiPlCs;
using Swicli.Library;

namespace Cogbot.Actions.System
{
    public class SwipCommand : Command, SystemApplicationCommand, AsynchronousCommand
    {
        private PrologScriptEngine.PrologScriptInterpreter pse = null;
        public SwipCommand(BotClient testClient)
        {
            Name = "swip";
        }

        public override void MakeInfo()
        {
            Description = "runs swi-prolog commands on current sim.";
            Category = CommandCategory.Simulator;
            Parameters = CreateParams(new NamedParam("prologCode", typeof (string), null));
        }

        public override CmdResult ExecuteRequest(CmdRequest pargs)
        {
            string verb = pargs.CmdName;
            int argsUsed;
            string text = pargs.str;
            try
            {
                pse = pse ?? new PrologScriptInterpreter(this);
                Nullable<PlTerm> cmd = null;
                object o = null;
                ManualResetEvent mre = new ManualResetEvent(false);
                Client.InvokeGUI(true, () =>
                                     {
                                         o = PrologCLR.InvokeFromC(() =>
                                                                      {
                                                                          cmd = pse.prologClient.Read(text, null) as Nullable<PlTerm>;
                                                                          o = pse.prologClient.Eval(cmd);
                                                                          mre.Set();
                                                                          return o;
                                                                      }, false);
                                     });
                PrologCLR.RegisterCurrentThread();
                mre.WaitOne();
                if (o == null) return Success("swip: " + cmd.Value);
                return Success("swip: " + cmd.Value + " " + o);
            }
            catch (Exception e)
            {
                string f = e.Message + " " + e.StackTrace;
                Client.WriteLine(f);
                return Failure(f);
            }
        }
    }
}