using OpenMetaverse;
using PathSystem3D.Navigation;

namespace Cogbot.World
{
    abstract public class BotAction : SimUsage
    {
        public BotAction(string s)
            : base(s)
        {
        }

        public abstract SimPosition Target { get; set; }

        public override float RateIt(BotNeeds current)
        {
            return ProposedChange().TotalSideEffect(current);
        }

        // the actor
        public SimAvatar TheBot;
        public SimControllableAvatar TheCBot
        {
            get
            {
                return TheBot as SimControllableAvatar;
            }
        }


        public GridClient GetGridClient()
        {
            return TheCBot.GetGridClient();
        }
        public BotClient GetBotClient()
        {
            return TheCBot.GetBotClient();
        }

        // Returns how much the needs should be changed;
        public abstract BotNeeds ProposedChange();
        // the needs are really changed;
        public abstract void InvokeReal();
        // use assumptions
        //public virtual float RateIt()
        //{
        //    BotNeeds bn = TheBot.CurrentNeeds.Copy();
        //    BotNeeds pc = ProposedChange();
        //    bn.AddFrom(pc);
        //    bn.SetRange(0f, 100f);
        //    return bn.Total() - (Vector3.Distance(TheBot.GetSimPosition(),GetLocation()));
        //}

        public abstract Vector3 GetUsePostion();


        public abstract void Abort();
    }
}