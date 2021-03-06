using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using MushDLR223.Utilities;
using OpenMetaverse;
using OpenMetaverse.Packets;
#if USE_SAFETHREADS
using Thread = MushDLR223.Utilities.SafeThread;
#endif
using MushDLR223.ScriptEngines;

namespace Cogbot.Actions
{
    public class ThreadCommand : Command, BotSystemCommand
    {
        public ThreadCommand(BotClient testClient)
        {
            Name = "thread";
            TheBotClient = testClient;
        }

        public override void MakeInfo()
        {
            Description =
                "Manipulates and Shows the list of threads and task queue statuses. SL/Opensim is a streaming system." +
                " Many things happen asynchronously. Each asynch activity is represented by a 'taskid'. These tasks are" +
                " processed from task queues. This command displays the status of the queues. It is mostly useful for debugging" +
                " cogbot itself, but can also be useful for understanding bot performance.";

            AddExample("thread create anim 30 crouch",
                       "returns immediately, but the bot continues to crouch for 30 seconds (Executes a command in its own thread)");
            AddExample("thread movement moveto FluffyBunny Resident",
                       "returns immediately but enqueues the movement into the movement queue");
            AddExample("thread movement --kill moveto FluffyBunny Resident",
                       "detroys all previous 'movement' and adds the command the movement queue");
            AddVersion(CreateParams(
                           "taskid", typeof (string), "the task queue this thread will use or 'create' or 'list'",
                           OptionalFlag("--all", "stop all and queue and thread tasks"),
                           OptionalFlag("--queue", "queue tasks"),
                           OptionalFlag("--thread", "thread tasks"),
                           Optional("--wait", typeof (TimeSpan), "blocks until the thread completes"),
                           Optional("--kill", typeof (bool), "whether to kill or append to previous taskid"),
                           Optional("--debug", typeof (bool), "turn task debugging on"),
                           Optional("--nodebug", typeof (bool), "turn task debugging off"),
                           Rest("command", typeof (string[]), "The command to execute asynchronously")),
                       Description);
            ResultMap = CreateParams(
                "message", typeof (string), "if the inner command failed, the reason why",
                "threadid", typeof (int), "returns a unique thread Id for killing later on",
                "success", typeof (bool), "true if the inner command succeeded");
            Category = CommandCategory.BotClient;
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            return ExecuteRequestProc(args, this);
        }

        public static CmdResult ExecuteRequestProc(CmdRequest args, Command thizcmd)
        {
            var TheBotClient = thizcmd.TheBotClient;
            OutputDelegate WriteLine = thizcmd.WriteLine;
            bool thread = args.IsTrue("--thread");
            bool queue = args.IsTrue("--queue");
            bool all = args.IsTrue("--all");
            bool kill = args.IsTrue("--kill");
            bool asyc = args.IsTrue("--async") || thread;
            TimeSpan wait;
            ManualResetEvent mre = null;
            if (args.TryGetValue("--wait", out wait))
            {
                mre = new ManualResetEvent(false);
            }
            bool newDebug = args.IsTrue("--debug");
            bool changeDebug = newDebug || args.IsTrue("--nodebug");

            bool createFresh = false;
            string id = args.Length == 0 ? "list" : GetTaskID(args, out createFresh);
            id = (id == "list") ? "" : id;
            int n = 0;
            int found = 0;
            if (id == "" || kill || changeDebug)
            {
                List<string> list = new List<string>();
                lock (TaskQueueHandler.TaskQueueHandlers)
                {
                    var atq = TheBotClient != null
                                  ? TheBotClient.AllTaskQueues()
                                  : ClientManager.SingleInstance.AllTaskQueues();
                    foreach (var queueHandler in atq)
                    {
                        if (!queueHandler.MatchesId(id)) continue;
                        bool isQueue = queueHandler.Impl == queueHandler;
                        if (isQueue) found++;
                        else n++;
                        if (changeDebug) queueHandler.DebugQueue = newDebug;
                        if (queueHandler.IsAlive)
                        {
                            string str = queueHandler.ToDebugString(true);
                            if (kill)
                            {
                                if (!all)
                                {
                                    if (!isQueue && !thread || isQueue && !queue)
                                    {
                                        WriteLine("Not killing " + str);
                                        continue;
                                    }
                                }
                                queueHandler.Abort();
                                str = "Killing " + str;
                                thizcmd.IncrResult("killed", 1);
                            }

                            WriteLine(str);
                        }
                        else
                        {
                            list.Add(queueHandler.ToDebugString(true));
                        }
                    }
                }
                foreach (var s in list)
                {
                    WriteLine(s);
                }
            }

            if (kill && createFresh)
            {
                return thizcmd.Failure("Cannot create and kill in the same operation");
            }
            string[] cmdS;
            args.TryGetValue("command", out cmdS);
            thizcmd.IncrResult("taskqueues", found);
            thizcmd.IncrResult("threads", n);
            if (cmdS == null || cmdS.Length == 0)
            {
                return thizcmd.Success("TaskQueueHandlers: " + found + ", threads: " + n);
            }

            /// task is killed if request.. now making a new one
            string cmd = Parser.Rejoin(cmdS, 0);
            bool needResult = mre != null;
            CmdResult[] result = null;
            if (createFresh) needResult = false;
            if (needResult)
            {
                result = new CmdResult[1];
            }
            CMDFLAGS flags = needResult ? CMDFLAGS.ForceResult : CMDFLAGS.Inherit;
            if (asyc) flags |= CMDFLAGS.ForceAsync;

            ThreadStart task = () =>
                                   {
                                       try
                                       {
                                           var res = TheBotClient.ExecuteCommand(cmd, args.CallerAgent, args.Output,
                                                                                 flags);
                                           if (result != null) result[0] = res;
                                       }
                                       catch (Exception)
                                       {
                                           throw;
                                       }
                                   };
            string message = TheBotClient.CreateTask(id, task, cmd, createFresh, false, mre, WriteLine);
            thizcmd.SetResult("taskid", id);
            if (mre != null)
            {
                if (!mre.WaitOne(wait)) return thizcmd.Failure("Timeout: " + message);
                if (result == null) return thizcmd.Success(message);
                return result[0] ?? thizcmd.Success(message);
            }
            return thizcmd.Success(message);
        }
    }
}