﻿using System.Xml;
using AltAIMLbot.Utils;

namespace AltAIMLbot.AIMLTagHandlersU
{
    /// <summary>
    /// IMPLEMENTED FOR COMPLETENESS REASONS
    /// </summary>
    public class template : AIMLTagHandlerU
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="bot">The bot involved in this request</param>
        /// <param name="user">The user making the request</param>
        /// <param name="query">The query that originated this node</param>
        /// <param name="request">The request inputted into the system</param>
        /// <param name="result">The result to be passed to the user</param>
        /// <param name="templateNode">The node to be Processed</param>
        public template(AltBot bot,
                        User user,
                        SubQuery query,
                        Request request,
                        Result result,
                        XmlNode templateNode)
            : base(bot, user, query, request, result, templateNode)
        {
        }

        protected override Unifiable ProcessChangeU()
        {
            if (!IsStarted && QueryHasFailed)
            {
                QueryHasFailed = false;
            }
            Unifiable templateResult = RecurseReal(templateNode, true);
            if (QueryHasFailed)
            {
                return FAIL;
            }
            RecurseResult = templateResult;//.ToString();
            return templateResult;
        }

        public override Unifiable CompleteProcessU()
        {
            if (RecurseResultValid) return RecurseResult;
            TemplateInfo queryTemplate = query.CurrentTemplate;
            if (queryTemplate != null)
            {
                if (queryTemplate.IsDisabledOutput)
                {
                    return Unifiable.INCOMPLETE;
                }
                Succeed();
                request.MarkTemplate(queryTemplate);
            }
            Unifiable templateResult = RecurseReal(templateNode, false);
            Unifiable test = templateResult;
            if (Unifiable.IsEMPTY(test))
            {
                if (QueryHasFailed)
                {
                    return FAIL;
                }
                if (IsSilentTag(templateNode))
                {
                    return templateResult;
                }
                ResetValues(true);
                templateResult = RecurseReal(templateNode, false);
            }
            if (templateResult == null)
            {
                return null;
            }
            return templateResult;
            string THINKYTAG = think.THINKYTAG;
            string tr = templateResult;
            string tr2 = ReplaceAll(tr.Replace(THINKYTAG + ".", " "), THINKYTAG, " ").Replace("  ", " ").Trim();
            if (tr != tr2)
            {
                if (tr2 == "")
                {
                    return THINKYTAG;
                }
                return tr2;
            }
            return templateResult;
            //return templateResult;
        }
    }
}
