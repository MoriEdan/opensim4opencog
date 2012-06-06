using System;
using System.Threading;
using OpenMetaverse;
using PathSystem3D.Navigation;

using MushDLR223.ScriptEngines;

namespace Cogbot.Actions.Movement
{

    public class WaitUntilPosition : Cogbot.Actions.Command, BotPersonalCommand
    {
        public WaitUntilPosition(BotClient client)
        {
            Name = "waitpos";
            Description = "Block until the robot gets to a certain position for a certain maxwait";
            Category = Cogbot.Actions.CommandCategory.Movement;
            Details = "waitpos seconds <x,y,z>";
            Parameters = new[]
                             {
                                 new NamedParam("seconds", typeof (TimeSpan), typeof (float)),
                                 new NamedParam("position", typeof (SimPosition), typeof (Vector3d))
                             };

        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            if (args.Length < 2)
                return ShowUsage();
            string str = Parser.Rejoin(args, 0);
            int argcount;
            float maxSeconds;
            if (!float.TryParse(args[0], out maxSeconds))
            {
                maxSeconds = 60000;
            }
            else
            {
                args = args.AdvanceArgs(1);
            }
            SimPosition pos = WorldSystem.GetVector(args, out argcount);
            if (pos == null)
            {
                return Failure(String.Format("Cannot {0} to {1}", Name, String.Join(" ", args)));
            }

            DateTime waitUntil = DateTime.Now.Add(TimeSpan.FromSeconds(maxSeconds));
            double maxDistance = pos.GetSizeDistance();
            if (maxDistance < 1) maxDistance = 1;

            bool MadIt = false;
            while (waitUntil > DateTime.Now)
            {
                var gp1 = pos.GlobalPosition;
                var gp2 = TheSimAvatar.GlobalPosition;
                if (Math.Abs(gp1.Z - gp2.Z) < 2) gp1.Z = gp2.Z;
                // do it antyways
                gp1.Z = gp2.Z;
                double cdist = Vector3d.Distance(gp1, gp2);
                if ( cdist <= maxDistance)
                {
                    MadIt = true;
                    break;
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
            if (MadIt)
            {
                return Success(string.Format("SUCCESS {0}", str));

            }
            else
            {
                return Failure("FAILED " + str);
            }
        }
    }
}