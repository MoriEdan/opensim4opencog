using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace cogbot.Actions
{
    public class ThreadCommand : Command, BotSystemCommand
    {
        public ThreadCommand(BotClient testClient)
        {
            Name = "thread";
            Description = "executes a command in its own thread. Type \"thread\" for usage.";
            Category = CommandCategory.Other;
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            //BotClient Client = TheBotClient;
            if (args.Length < 1)
            {
                return ShowUsage();// " thread anim 30 crouch";
            }
            if (args.Length == 1 && args[0]=="list")
            {
                int n = 0;
                String s="";
                lock (Client.botCommandThreads)  foreach (Thread t in Client.botCommandThreads)
                {
                    n++;
                    //System.Threading.ThreadStateException: Thread is dead; state cannot be accessed.
                    //  at System.Threading.Thread.IsBackgroundNative()
                    s+=""+ n + ": " + t.Name + " alive=" + t.IsAlive;
                }
                return Success(s);
            }
            String cmd = String.Join(" ", args);
            Thread thread = new Thread(new ThreadStart(delegate()
            {
                try
                {
                    WriteLine(Client.ExecuteCommand(cmd, WriteLine));
                }
                catch (OutOfMemoryException) { }
                catch (StackOverflowException) { }
                catch (Exception) { }
                try
                {
                    WriteLine("done with " + cmd);
                }
                catch (OutOfMemoryException) { }
                catch (StackOverflowException) { }
                catch (Exception) { }
                try
                {
                    lock (Client.botCommandThreads)
                        TheBotClient.botCommandThreads.Remove(Thread.CurrentThread);
                }
                catch (OutOfMemoryException) { }
                catch (StackOverflowException) { }
                catch (Exception) { }
            }));
            thread.Name = "ThreadCommnand for " + cmd;
            thread.Start();
            lock (Client.botCommandThreads) Client.botCommandThreads.Add(thread);
            return Success(thread.Name);
        }
    }
}
