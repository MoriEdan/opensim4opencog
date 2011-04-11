using System;
using System.Xml;
using System.Text;

namespace RTParser.AIMLTagHandlers
{
    /// <summary>
    /// The think element instructs the AIML interpreter to perform all usual processing of its 
    /// contents, but to not return any value, regardless of whether the contents produce output.
    /// 
    /// The think element has no attributes. It may contain any AIML template elements.
    /// </summary>
    public class think : RTParser.Utils.AIMLFormatingTagHandler
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
        public think(RTParser.RTPBot bot,
                        RTParser.User user,
                        RTParser.Utils.SubQuery query,
                        RTParser.Request request,
                        RTParser.Result result,
                        XmlNode templateNode)
            : base(bot, user, query, request, result, templateNode)
        {
            IsStarAtomically = false;
        }

        /// <summary>
        /// The method that does the actual processing of the text.
        /// </summary>
        /// <returns>The resulting processed text</returns>
        protected override Unifiable Format(Unifiable templateNodeInnerText)
        {
            CheckNode("think");
            writeToLog("THOUGHT: '" + Unifiable.DescribeUnifiable(templateNodeInnerText) + "'");
            if (IsNull(templateNodeInnerText))
            {
                return FAIL;
            }
            var vv = GetAttribValue<Unifiable>(templateNode, "retval", null);
            if (vv != null) return vv;
            if (true) return THINKYTAG;
            return Succeed(" THOUGHT: '" + templateNodeInnerText + "'");
        }

        static public Unifiable THINKYTAG
        {
            get { return "TAG-THINK"; }
        }

        protected override Unifiable templateNodeInnerText
        {
            get
            {
                return base.templateNode.InnerText;
            }
            set
            {
                base.templateNode.InnerText = value;
            }
        }
    }
}
