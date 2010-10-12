using System;
using System.Xml;
using System.Text;
using System.IO;
using MushDLR223.Utilities;
using RTParser.Utils;

namespace RTParser.AIMLTagHandlers
{
    /// <summary>
    /// The learn element instructs the AIML interpreter to retrieve a resource specified by a URI, 
    /// and to process its AIML object contents.
    /// supports network HTTP and web service based AIML learning (as well as local filesystem)
    /// </summary>
    public class learn : RTParser.Utils.LoadingTagHandler
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
        public learn(RTParser.RTPBot bot,
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
            IsStarted = true;
            isRecursive = true;
            var recursiveResult = Unifiable.CreateAppendable();

            if (templateNode.HasChildNodes)
            {

                XmlNode attach = StaticXMLUtils.CopyNode("aiml", templateNode, false);
                attach.RemoveAll();
                // recursively check
                foreach (XmlNode childNode in templateNode.ChildNodes)
                {
                    XmlNode evalChild = EvalChild(childNode);
                    attach.AppendChild(evalChild);
                }
                templateNode = attach;
            }
            return recursiveResult;
        }

        private XmlNode EvalChild(XmlNode templateNode)
        {
            XmlNode attach = templateNode.CloneNode(false);// //AIMLLoader.CopyNode(templateNode, false);
            LineInfoElementImpl.unsetReadonly(attach);
            if (templateNode.HasChildNodes)
            {
                // recursively check
                foreach (XmlNode childNode in templateNode.ChildNodes)
                {
                    if (childNode.LocalName == "eval")
                    {
                        AppendEvalation(attach, childNode);
                    }
                    else
                    {
                        attach.AppendChild(EvalChild(childNode));
                    }
                }
            }
            return attach;
        }

        void AppendEvalation(XmlNode attach, XmlNode childNode)
        {
            {
                {
                    {
                        string ts = "<template>" + ToXmlValue(childNode) + "</template>";
                        var tchiuld = getNodeAndSetSiblingNode(ts, childNode);
                        string ost = tchiuld.OuterXml;
                        LineInfoElementImpl.unsetReadonly(tchiuld);
                        Unifiable processChildNode = ProcessChildNode(tchiuld);
                        SaveResultOnChild(childNode, processChildNode);
                        var readNode = getNodeAndSetSiblingNode("<node>" + Unifiable.InnerXmlText(childNode) + "</node>", childNode);
                        LineInfoElementImpl.unsetReadonly(readNode);
                        if (readNode.ChildNodes.Count == 1)
                        {
                            XmlNode chilz = readNode.ChildNodes[0];
                            LineInfoElementImpl.chopParent(chilz);
                            attach.AppendChild(chilz);
                            return;
                        }
                        foreach (XmlNode child in readNode.ChildNodes)
                        {
                            LineInfoElementImpl.unsetReadonly(child);
                            attach.AppendChild(child.CloneNode(true));
                        }
                    }
                }
            }
        }

        protected override Unifiable ProcessLoad(LoaderOptions loaderOptions)
        {
            if (CheckNode("learn,load,graph"))
            {
               // LoaderOptions loaderOptions = loaderOptions0;// ?? LoaderOptions.GetDefault(request);
                
                //recurse here? 
                bool outRecurse;                
                if (TryParseBool(templateNode, "recurse", out outRecurse))
                {
                    loaderOptions.recurse = outRecurse;
                }

                GraphMaster g = request.Graph;
                var g0 = g;
                String graphName = GetAttribValue("graph", null);
                if (graphName != null)
                {
                    g = Proc.GetGraph(graphName, g0);
                    if (g != null) request.Graph = g;
                }
                Unifiable path = GetAttribValue("filename,uri,file,url,dir,path,pathname,directory", null);
                string command = GetAttribValue("command,cmd", null);
                try
                {
                    //templateNode.LocalName
                    string documentInfo =  DocumentInfo();;
                    loaderOptions = request.LoadOptions;
                    request.LoadingFrom = documentInfo;
                    loaderOptions.CtxGraph = request.Graph;
                    string innerXML = InnerXmlText(templateNode);


                    if (!string.IsNullOrEmpty(command))
                    {
                        string more = "";
                        if (!string.IsNullOrEmpty(innerXML))
                        {
                            more = " - " + innerXML;
                        }
                        TargetBot.BotDirective(request, "@" + command + " " + request.Graph.ScriptingName + more,
                                               writeToLog);
                        //QueryHasSuceededN++;
                    }
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(innerXML) && innerXML.Contains("<"))
                            {
                                try
                                {
                                    long successes = request.Loader.loadAIMLNode(templateNode, loaderOptions, request);
                                    return "" + successes;
                                }
                                finally
                                {
                                    
                                }
                            }
                            else if (path == "")
                            {
                                writeToLogWarn("ERROR! Attempted (but failed) to <learn> some new AIML from the following URI: '{0}' - '{1}'", path, innerXML);
                            }
                            else
                            {
                                path = path ?? innerXML;
                                loaderOptions.Loading0 = path;
                                long forms = request.Loader.loadAIMLURI(path, loaderOptions);
                                QueryHasSuceededN++;
                                return "" + forms; // Succeed();
                            }
                        }
                        catch (ChatSignal e)
                        {
                            throw;
                        }
                        catch (Exception e2)
                        {
                            Proc.writeToLog(e2);
                            writeToLogWarn("ERROR! Attempted (but failed) to <learn> some new AIML from the following URI: {0} error {1}", path, e2);
                        }

                    }
                }
                finally
                {
                    request.Graph = g0;
                } 
            }
            return Unifiable.Empty;
        }
    }
}
