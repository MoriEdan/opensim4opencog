using System.Xml;
using AltAIMLbot.Utils;

namespace AltAIMLbot.AIMLTagHandlersU
{
    /// <summary>
    /// An element called bot, which may be considered a restricted version of get, is used to 
    /// tell the AIML interpreter that it should substitute the contents of a "bot predicate". The 
    /// value of a bot predicate is set at load-time, and cannot be changed at run-time. The AIML 
    /// interpreter may decide how to set the values of bot predicate at load-time. If the bot 
    /// predicate has no value defined, the AIML interpreter should substitute an empty Unifiable.
    /// 
    /// The bot element has a required name attribute that identifies the bot predicate. 
    /// 
    /// The bot element does not have any content. 
    /// </summary>
    public class bot : get
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="Proc">The Proc involved in this request</param>
        /// <param name="user">The user making the request</param>
        /// <param name="query">The query that originated this node</param>
        /// <param name="request">The request inputted into the system</param>
        /// <param name="result">The result to be passed to the user</param>
        /// <param name="templateNode">The node to be Processed</param>
        public bot(AltBot bot,
                        User user,
                        SubQuery query,
                        Request request,
                        Result result,
                        XmlNode templateNode)
            : base(bot, user, query, request, result, templateNode)
        {
        }
        /*
        //public StreamWriter chatTrace;

        protected override Unifiable ProcessChangeU()
        {
            if (RecurseResultValid) return RecurseResult;
            Unifiable defaultVal = GetAttribValue("default", Unifiable.Empty);
            if (CheckNode("bot"))
            {
                string name = GetAttribValue(templateNode, "name,var", () => templateNodeInnerText, ReduceStarAttribute);
                bool succeed;
                var value = GetActualValue(name, templateNode.Name, out succeed); // true == "bot";
                if (succeed && name != "name") Succeed();
                if (!Unifiable.IsNullOrEmpty(value))
                {
                    RecurseResult = value;
                }
                return value;
            }
            return defaultVal;
        }*/
    }
}
