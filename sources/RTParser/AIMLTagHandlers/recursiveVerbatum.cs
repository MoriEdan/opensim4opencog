using System;
using System.Xml;
using System.Text;

namespace RTParser.AIMLTagHandlers
{
    /// <summary>
    /// The version element tells the AIML interpreter that it should substitute the version number
    /// of the AIML interpreter.
    /// 
    /// The version element does not have any content. 
    /// </summary>
    public class recursiveVerbatum : RTParser.Utils.AIMLTagHandler
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="bot">The bot involved in this request</param>
        /// <param name="user">The user making the request</param>
        /// <param name="query">The query that originated this node</param>
        /// <param name="request">The request inputted into the system</param>
        /// <param name="result">The result to be passed to the user</param>
        /// <param name="templateNode">The node to be processed</param>
        public recursiveVerbatum(XmlNode show, RTParser.RTPBot bot,
                        RTParser.User user,
                        RTParser.Utils.SubQuery query,
                        RTParser.Request request,
                        RTParser.Result result,
                        XmlNode templateNode, bool isRecurse)
            : base(bot, user, query, request, result, templateNode)
        {
            data = show;
            //RecurseResult = data;
            isRecursive = isRecurse;
        }

        readonly XmlNode data;
        protected override Unifiable ProcessChange()
        {
            return data.OuterXml;
        }
        public override float CanUnify(Unifiable with)
        {
            writeToLogWarn("CANUNIFY: " + with);
            return base.CanUnify(with);
        }
    }
}
