using System;
using System.Xml;
using System.Text;
using RTParser.Database;
using RTParser.Utils;
using RTParser.Variables;

namespace RTParser.AIMLTagHandlers
{
    /// <summary>
    /// The get element tells the AIML interpreter that it should substitute the contents of a 
    /// predicate, if that predicate has a value defined. If the predicate has no value defined, 
    /// the AIML interpreter should substitute the empty Unifiable "". 
    /// 
    /// The AIML interpreter implementation may optionally provide a mechanism that allows the 
    /// AIML author to designate default values for certain predicates (see [9.3.]). 
    /// 
    /// The get element must not perform any text formatting or other "normalization" on the predicate
    /// contents when returning them. 
    /// 
    /// The get element has a required name attribute that identifies the predicate with an AIML 
    /// predicate name. 
    /// 
    /// The get element does not have any content.
    /// </summary>
    public class get : RTParser.Utils.AIMLTagHandler
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
        public get(RTParser.RTPBot bot,
                        RTParser.User user,
                        RTParser.Utils.SubQuery query,
                        RTParser.Request request,
                        RTParser.Result result,
                        XmlNode templateNode)
            : base(bot, user, query, request, result, templateNode)
        {
        }
        protected override bool ExpandingSearchWillYieldNoExtras { get { return true; } }

        public override Unifiable CompleteProcess()
        {
            Unifiable pc = ProcessChange();
            string s = (string) pc;
            if (pc != null && pc == "name")
            {
                return pc;
            }
            return pc;
        }

        protected override Unifiable ProcessChange()
        {
            if (RecurseResultValid) return RecurseResult;
            Unifiable u = ProcessChange0();
            if (IsIncomplete(u))
            {
                Unifiable defaultVal = GetAttribValue("default", null);
                if (defaultVal == null)
                {
                    QueryHasFailed = true;
                    return FAIL;
                }
                return defaultVal;
            }
            string s = u.ToValue(query);
            if (s.Contains(" ")) return s;
            if (s.ToLower().StartsWith("unknown"))
            {
                s = s.Substring(7);
                writeToLog("SETTINGS UNKNOWN " + s);
                return "unknown " + s;
            }
            return u;
        }

        /// <summary>
        /// Helper method that turns the passed Unifiable into an XML node
        /// </summary>
        /// <param name="outerXML">the Unifiable to XMLize</param>
        /// <returns>The XML node</returns>
        public override float CanUnify(Unifiable with)
        {
            if (CheckNode("get,bot"))
            {
                string name = GetAttribValue(templateNode, "name,var", () => templateNodeInnerText, ReduceStarAttribute);
                bool succeed;
                Unifiable v = GetActualValue(name, typeof (bot) == GetType() ? "bot" : "get", out succeed);
                if (IsNull(v))
                {
                    if (!succeed)
                    {
                        QueryHasFailed = true;
                        return 1.0f;
                    }
                    // trace the next line to see why
                    Proc.TraceTest("NULL from success?!",
                                   () => GetActualValue(name, typeof (bot) == GetType() ? "bot" : "get", out succeed));
                    return 1.0f;
                }
                if (succeed)
                {          
                    if (IsPredMatch(v, with, query))
                    {
                        Succeed();
                        return 0.0f;
                    }
                }
                return 1.0f;
            }
            return 1.0f;
        }

        protected Unifiable ProcessChange0()
        {
            if (CheckNode("get,bot"))
            {
                string name = GetAttribValue(templateNode, "name,var", () => templateNodeInnerText, ReduceStarAttribute);
                bool succeed;
                Unifiable v = GetActualValue(name, typeof(bot) == GetType() ? "bot" : "get", out succeed);
                if (!IsValue(v))
                {
                    if (!succeed)
                    {
                        QueryHasFailed = true;
                        return FAIL;
                    }
                    // trace the next line to see why
                    Proc.TraceTest("NULL from success?!", () => GetActualValue(name, typeof (bot) == GetType() ? "bot" : "get", out succeed));
                    return Unifiable.Empty;
                }
                if (succeed)
                {
                    Succeed();
                    return v;
                }
                if (request.IsToplevelRequest) return v;
                return FAIL;
            }
            return Unifiable.Empty;
        }
    }
}
