using System;
using System.Collections.Generic;
using System.Text;
using cogbot.TheOpenSims;
using OpenMetaverse;
using System.Threading;

namespace cogbot.Listeners
{


    public class SimEventTextSubscriber : SimEventSubscriber
    {
        readonly BotClient From;
        readonly OutputDelegate textForm;
        public SimEventTextSubscriber(OutputDelegate _text, BotClient from)
        {
            From = from;
            textForm = _text;
        }

        #region SimEventSubscriber Members

        void SimEventSubscriber.OnEvent(SimObjectEvent evt)
        {
            if (evt.EventType == SimEventType.DATA_UPDATE) return;
            String eventName = evt.GetVerb();
            object[] args = evt.GetArgs();

            String msg = "["+ From.GetName() + ": " + eventName.ToLower()+"]";
            int start = 0;
            if (args.Length > 1)
            {
                if (args[0] is Simulator)
                {
                   // start = 1;
                }
            }
            for (int i = start; i < args.Length; i++)
            {
                msg += " ";
                msg += From.argString(args[i]);
            }

            msg += "";
            
            textForm(msg);
        }

        void SimEventSubscriber.ShuttingDown()
        {
            textForm("SimEventTextSubscriber shutdown for " + From);
        }

        #endregion
    }
    public interface SimEventSubscriber
    {
        // fired when SendEvent is invoke and this subscriber is downstream in the pipeline
        void OnEvent(SimObjectEvent evt);
        void ShuttingDown();
    }

    public interface SimEventPublisher
    {
        // this publisher will SendEvent to some SimEventPipeline after the Event params have been casted to the correct types
        SimObjectEvent CreateEvent(SimEventType type,SimEventClass clazz, string eventName, params object[] args);
        // this object will propogate the event AS-IS 
        void SendEvent(SimObjectEvent evt);
        void AddSubscriber(SimEventSubscriber sub);
    }

    public class SimEventMulticastPipeline : SimEventSubscriber, SimEventPublisher
    {
        #region SimEventMulticastPipeline Members

        List<SimEventSubscriber> subscribers = new List<SimEventSubscriber>();

        public SimEventMulticastPipeline()
        {
        }

        #endregion

        #region SimEventSubscriber Members

        public void ShuttingDown()
        {
            foreach (SimEventSubscriber subscriber in GetSubscribers())
            {
                subscriber.ShuttingDown();
            }
        }

        private IEnumerable<SimEventSubscriber> GetSubscribers()
        {
            lock (subscribers)
            {
                return new List<SimEventSubscriber>(subscribers);
            }
        }

        #endregion

        #region SimEventPublisher Members

        public SimObjectEvent CreateEvent(SimEventType type, SimEventClass clazz, string eventName, params object[] args)
        {
            return new SimObjectEvent(type,clazz, eventName, args);
        }

        static private bool UseQueue = false;
        SimObjectEvent LastEvent = null;
        // this pipelike will fire OnEvent to the subscriber list 
        public void SendEvent(SimObjectEvent simObjectEvent)
        {
            if (LastEvent != null && simObjectEvent.SameAs(LastEvent))
            {
                return;
            }
            LastEvent = simObjectEvent;
            foreach (SimEventSubscriber subscriber in GetSubscribers())
            {
                SimEventSubscriber sub = subscriber;
                ThreadStart start =()=>
                               {
                                   try
                                   {
                                       simObjectEvent.SendTo(sub);
                                   }
                                   catch (Exception e)
                                   {
                                       Console.WriteLine(e);
                                   }
                               };
                if (UseQueue)
                {
                    new Thread(start).Start();
                }
                else start.Invoke();
                
                
                
            }
        }

        #endregion

        #region SimEventSubscriber Members

        public void OnEvent(SimObjectEvent simObjectEvent)
        {
            foreach (SimEventSubscriber subscriber in GetSubscribers())
            {
                simObjectEvent.SendTo(subscriber);
            }
        }

        #endregion

        #region SimEventPublisher Members


        public void AddSubscriber(SimEventSubscriber sub)
        {
            if (sub == null) throw new NullReferenceException();
            lock (subscribers) if (!subscribers.Contains(sub))
                    subscribers.Add(sub);
        }

        #endregion
    }
}
