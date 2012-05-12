using System;
using System.Collections.Generic;
using System.Threading;
using cogbot.Listeners;
using cogbot.Utilities;
using MushDLR223.ScriptEngines;
using OpenMetaverse;
using PathSystem3D.Navigation;

namespace cogbot.TheOpenSims
{
    /// <summary>
    /// An Afforance in Secondlife
    /// </summary>
    abstract public class SimUsage : BotMentalAspect
    {
        public abstract FirstOrderTerm GetTerm();
        public virtual ICollection<NamedParam> GetInfoMap()
        {
            return WorldObjects.GetMemberValues("", this);
        }
        public UUID ID
        {
            get { throw new NotImplementedException("BotMentalAspect.ID " + this); }
        }
        public String UsageName;

        public SimUsage(string name)
           // : base(name)
        {
            UsageName = name;
        }

        public static bool operator ==(SimUsage use1, SimUsage use2)
        {
            if (Object.ReferenceEquals(use1, null) && Object.ReferenceEquals(use2, null)) return true;
            if (Object.ReferenceEquals(use1, null) || Object.ReferenceEquals(use2, null)) return false;
            return use1.UsageName == use2.UsageName;
        }

        public static bool operator !=(SimUsage use1, SimUsage use2)
        {
            if (use1 == null && use2 == null) return false;
            if (use1 == null || use2 == null) return true;
            return use1.UsageName != use2.UsageName;
        }
        public override bool Equals(object obj)
        {
            if (!(obj is SimUsage)) return false;
            return this == (SimUsage)obj;
        }

        public override int GetHashCode()
        {
            return UsageName.GetHashCode();
        }

        public override string ToString()
        {
            return String.Format("{0}::{1}", GetType().Name, UsageName);
        }

        public abstract float RateIt(BotNeeds current);
    }
    // find the subclasses in the Usage subdirectory
}
