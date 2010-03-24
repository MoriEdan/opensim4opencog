using System;
using System.Threading;
using System.Xml;
using System.Text;

namespace RTParser.AIMLTagHandlers
{
    /// <summary>
    /// The srai element instructs the AIML interpreter to pass the result of processing the contents 
    /// of the srai element to the AIML matching loop, as if the input had been produced by the user 
    /// (this includes stepping through the entire input normalization process). The srai element does 
    /// not have any attributes. It may contain any AIML template elements. 
    /// 
    /// As with all AIML elements, nested forms should be parsed from inside out, so embedded srais are 
    /// perfectly acceptable. 
    /// </summary>
    public class srai : RTParser.Utils.AIMLTagHandler
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
        public srai(RTParser.RTPBot bot,
                        RTParser.User user,
                        RTParser.Utils.SubQuery query,
                        RTParser.Request request,
                        RTParser.Result result,
                        XmlNode templateNode)
            : base(bot, user, query, request, result, templateNode)
        {
        }

        protected override Unifiable ProcessChange()
        {
            int d = request.GetCurrentDepth();
            if (d > 30)
            {
                Console.WriteLine("WARNING Depth pretty deep " + templateNode + " returning empty");
                return Unifiable.Empty;
            }
            if (this.templateNode.Name.ToLower() == "srai")
            {
                if (!templateNodeInnerText.IsEmpty)
                {
                    AIMLbot.Request subRequest = new AIMLbot.Request(templateNodeInnerText, this.user, this.Proc);
                    subRequest.StartedOn = this.request.StartedOn; // make sure we don't keep adding time to the request
                    AIMLbot.Result subQuery = this.Proc.Chat(subRequest);
                    this.request.hasTimedOut = subRequest.hasTimedOut;
                    return subQuery.Output;
                }
            }
            return Unifiable.Empty;
        }
    }
}
