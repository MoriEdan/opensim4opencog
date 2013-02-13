using System;
using System.Xml;
using RTParser.Utils;

namespace RTParser.AIMLTagHandlers
{
    public class format : AIMLTagHandler
    {
        private Func<Unifiable, Unifiable> UFormatter;
        private Func<string, string> SFormatter;
        private Func<Unifiable, Unifiable> UFormatterE;
        private Func<string, string> SFormatterE;

        #region Overrides of AIMLFormatingTagHandler

        /// <summary>
        /// The subclass only needs to process the non atomic inner text
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        protected Unifiable Format(Unifiable text)
        {
            if (UFormatter != null) return UFormatter(text);
            if (SFormatter != null) return SFormatter(text);
            return text;
        }

        #endregion


        /// <summary>
        /// The method that does the actual processing of the text.
        /// </summary>
        /// <returns>The resulting processed text</returns>
        sealed protected override Unifiable ProcessChange()
        {
            return Format(TransformAtomically(OnEach, isRecursive && !ReadOnly));
        }

        private Unifiable OnEach(Unifiable text)
        {
            if (UFormatterE != null) return UFormatterE(text);
            if (SFormatterE != null) return SFormatterE(text);
            return text;
        }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="bot">The bot involved in this request</param>
        /// <param name="user">The user making the request</param>
        /// <param name="query">The query that originated this node</param>
        /// <param name="request">The request inputted into the system</param>
        /// <param name="result">The result to be passed to the user</param>
        /// <param name="templateNode">The node to be processed</param>
        public format(RTParser.RTPBot bot,
                      RTParser.User user,
                      RTParser.Utils.SubQuery query,
                      RTParser.Request request,
                      RTParser.Result result,
                      XmlNode templateNode, Func<Unifiable, Unifiable> formatter, Func<Unifiable, Unifiable> formattereach)
            : base(bot, user, query, request, result, templateNode)
        {
            this.UFormatter = formatter;
            this.UFormatterE = formattereach;
        }

        public format(RTParser.RTPBot bot,
                      RTParser.User user,
                      RTParser.Utils.SubQuery query,
                      RTParser.Request request,
                      RTParser.Result result,
                      XmlNode templateNode, Func<string, string> formatter, Func<string, string> formattereach)
            : base(bot, user, query, request, result, templateNode)
        {
            this.SFormatter = formatter;
            this.SFormatterE = formattereach;
        }
    }
}