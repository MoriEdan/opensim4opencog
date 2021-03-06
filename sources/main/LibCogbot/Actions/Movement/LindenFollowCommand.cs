using System;
using System.Collections.Generic;
using System.Text;
#if (COGBOT_LIBOMV || USE_STHREADS)
using ThreadPoolUtil;
using Thread = ThreadPoolUtil.Thread;
using ThreadPool = ThreadPoolUtil.ThreadPool;
using Monitor = ThreadPoolUtil.Monitor;
#endif
using System.Threading;


using MushDLR223.Utilities;
using OpenMetaverse;
using OpenMetaverse.Packets;
using PathSystem3D.Navigation;
using MushDLR223.ScriptEngines;

namespace Cogbot.Actions.Movement
{
    public class FollowCommand : Command, BotPersonalCommand, BotStatefullCommand
    {
        private const float DISTANCE_BUFFER = 3.0f;
        private uint targetLocalID = 0;

        public FollowCommand(BotClient testClient)
        {
            Name = "Linden follow";
        }

        public override void MakeInfo()
        {
            Description = "Follow another avatar. Usage: follow [FirstName LastName]/off.";
            Category = CommandCategory.Movement;
            Parameters = CreateParams("position", typeof (SimPosition), "the location you wish to " + Name);
        }

        public override CmdResult ExecuteRequest(CmdRequest argsI)
        {
            string[] args = argsI;
            Client.Network.RegisterCallback(PacketType.AlertMessage, AlertMessageHandler);
            // Construct the target name from the passed arguments
            string target = String.Empty;
            for (int ct = 0; ct < args.Length; ct++)
                target = target + args[ct] + " ";
            target = target.TrimEnd();

            if (target.Length == 0 || target == "off")
            {
                Active = false;
                targetLocalID = 0;
                Client.Self.AutoPilotCancel();
                return Success("Following is off");
            }
            else
            {
                if (Follow(target))
                    return Success("Following " + target);
                else
                    return Failure("Unable to follow " + target + ".  Client may not be able to see that avatar.");
            }
        }

        private bool Active;

        private bool Follow(string name)
        {
            foreach (Simulator sim in LockInfo.CopyOf(Client.Network.Simulators))
            {
                Avatar target = sim.ObjectsAvatars.Find(
                    delegate(Avatar avatar) { return avatar.Name == name; }
                    );

                if (target != null)
                {
                    targetLocalID = target.LocalID;
                    Active = true;
                    EnsureRunning();
                    return true;
                }
            }


            if (Active)
            {
                Client.Self.AutoPilotCancel();
                Active = false;
            }

            return false;
        }

        private bool isDisposing = false;
        private AAbortable ThinkThread;

        private void EnsureRunning()
        {
            if (isDisposing || ThinkThread != null) return;
            ThinkThread = new AAbortable(new Thread(SecondLoop) {Name = "LindenFollow"}, OnDeath);
            TheBotClient.AddThread(ThinkThread);
        }

        private void OnDeath(Abortable obj)
        {
            ThinkThread = null;
            EnsureRunning();
        }

        private void SecondLoop()
        {
            while (true)
            {
                Think();
            }
        }

        public void Think()
        {
            if (Active)
            {
                // Find the target position
                {
                    foreach (Simulator sim in LockInfo.CopyOf(Client.Network.Simulators))
                    {
                        Avatar targetAv;

                        if (sim.ObjectsAvatars.TryGetValue(targetLocalID, out targetAv))
                        {
                            float distance = 0.0f;

                            if (sim == Client.Network.CurrentSim)
                            {
                                distance = Vector3.Distance(targetAv.Position, Client.Self.SimPosition);
                            }
                            else
                            {
                                // FIXME: Calculate global distances
                            }

                            if (distance > DISTANCE_BUFFER)
                            {
                                uint regionX, regionY;
                                Utils.LongToUInts(sim.Handle, out regionX, out regionY);

                                double xTarget = (double) targetAv.Position.X + (double) regionX;
                                double yTarget = (double) targetAv.Position.Y + (double) regionY;
                                double zTarget = targetAv.Position.Z - 2f;

                                Logger.DebugLog(
                                    String.Format(
                                        "[Autopilot] {0} meters away from the target, starting autopilot to <{1},{2},{3}>",
                                        distance, xTarget, yTarget, zTarget), Client);

                                Client.Self.AutoPilot(xTarget, yTarget, zTarget);
                            }
                            else
                            {
                                // We are in range of the target and moving, stop moving
                                Client.Self.AutoPilotCancel();
                            }
                        }
                    }
                }
            }
        }

        private void AlertMessageHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;

            AlertMessagePacket alert = (AlertMessagePacket) packet;
            string message = Utils.BytesToString(alert.AlertData.Message);

            if (message.Contains("Autopilot cancel"))
            {
                Logger.Log("FollowCommand: " + message, Helpers.LogLevel.Info, Client);
            }
        }

        #region Implementation of IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            isDisposing = true;
            Client.Network.UnregisterCallback(PacketType.AlertMessage, AlertMessageHandler);
            if (ThinkThread != null)
            {
                ThinkThread.Abort();
            }
        }

        #endregion
    }
}