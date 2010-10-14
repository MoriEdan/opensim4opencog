using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using MushDLR223.Utilities;
using PatternInfo = RTParser.Unifiable;
using ThatInfo = RTParser.Unifiable;
using TopicInfo = RTParser.Unifiable;
using GuardInfo = RTParser.Unifiable;
using ResponseInfo = RTParser.Unifiable;

namespace RTParser.Utils
{
    [Serializable]
    abstract public class CategoryInfoImpl1 : GraphLinkInfo, IAIMLInfo, IComparable<TemplateInfo>, CategoryInfo
    {
        public Unifiable Filename { get; set; }

        public abstract GuardInfo Guard { get; set; }
        public abstract ResponseInfo Response { get; set; }
        public GraphMaster InGraph { get; set; }

        public PatternInfo Pattern { get; set; }
        public List<ConversationCondition> Preconds { get; set; }
        public TemplateInfo Template { get { return (TemplateInfo)this; } }
        public ThatInfo That { get; set; }
        public TopicInfo Topic { get; set; }

        static XmlNode CheckXml(XmlNode xmlNode)
        {
            if (xmlNode != null) return xmlNode;
            return xmlNode;
        }

        //private TemplateInfo ParentCategory;

        abstract public bool IsTraced { get; set; }
        virtual public bool IsDisabled { get; set; }
        abstract public bool IsDisabledOutput { get; set; }
        abstract public bool IsSearchDisabled { get; set; }

        protected CategoryInfoImpl1(PatternInfo pattern, XmlNode cateNode, LoaderOptions options)
            : base(cateNode)
        {
            Pattern = pattern;
            Filename = options.CurrentFilename;
        }

        public string WhyDisabled
        {
            get
            {
                if (Template.IsDisabledOutput)
                {
                    return "IsDisabledOutput";
                }
                else if (IsSearchDisabled)
                {
                    return "IsSearchDisabled";
                }
                else if (this.IsDisabled)
                {
                    return "IsDisabled";
                }
                return null;
            }
        }

        public XmlNode CategoryXml { get { return CheckXml(StaticXMLUtils.FindNodeOrHigher("category", CheckXml(srcNode), null)); } }

        public virtual XmlNode TemplateXml
        {
            get { return CheckXml(StaticXMLUtils.FindNode("template", srcNode, null)); }
        }

        public XmlNode TopicXml
        {
            get { return CheckXml(StaticXMLUtils.FindNode("topic", CategoryXml, null)) ?? StaticXMLUtils.FindHigher("topic", CategoryXml, null) ?? TopicStar; }
        }

        public XmlNode ThatXml
        {
            get { return CheckXml(StaticXMLUtils.FindNode("that", CategoryXml, null)) ?? StaticXMLUtils.FindHigher("that", CategoryXml, null) ?? ThatStar; }
        }

        #region IAIMLInfo Members

        string IAIMLInfo.SourceInfo()
        {
            return StaticXMLUtils.LocationInfo(TemplateXml);
        }

        public virtual GraphMaster Graph
        {
            get
            {
                Node gmNode = Template.GraphmasterNode;
                return gmNode == null ? InGraph : gmNode.Graph;
            }
        }

        //protected abstract XmlNode TemplateXml { get; set; }
        public virtual Node GraphmasterNode { get; set; }

        virtual public string ToFileString(PrintOptions printOptions)
        {
            //if (XmlDocumentLineInfo.SkipXmlns && this.srcNode.Attributes != null) this.srcNode.Attributes.RemoveNamedItem("xmlns");
            string s = "";
            if (IsDisabled)
            {
                if (!printOptions.WriteDisabledItems) return s;
            }
            string graphName = ((IAIMLInfo) this).Graph.ScriptingName;
            if (printOptions.IncludeGraphName)
            {
                if (graphName != printOptions.CurrentGraphName)
                {
                    if (printOptions.InsideAiml)
                    {
                        s += "\n</aiml>\n";
                        s += string.Format("\n<aiml graph=\"{0}\">\n", graphName);
                        printOptions.CurrentGraphName = graphName;
                    }
                    else
                    {
                        printOptions.InsideAiml = true;
                        s += string.Format("\n<aiml graph=\"{0}\">\n", graphName);
                        printOptions.CurrentGraphName = graphName;
                    }
                }
            }

            s += GetSourceWithTopic(printOptions);

            string escapedStuff = "";

            if (!printOptions.IncludeLineInfoExternal)
            {
                if (!printOptions.GroupFileElements)
                {
                    if (printOptions.IncludeFileNamePerNode)
                    {
                        string cfile = Filename ?? StaticXMLUtils.FileNameOfXmlNode(srcNode);
                        if (cfile != printOptions.InsideFilename)
                        {
                            escapedStuff += cfile;
                            printOptions.InsideFilename = cfile;
                        }
                    }
                }
                if (printOptions.IncludeLinenoPerNode)
                {
                    escapedStuff += ":" + LineNumberInfo;
                }
            }

            escapedStuff = StaticXMLUtils.MakeXmlCommentSafe(escapedStuff);
            if (IsDisabled)
            {
                s = DLRConsole.SafeFormat("<!-- {0} {1} {2} -->", MakeXmlCommentSafe(WhyDisabled), MakeXmlCommentSafe(s), escapedStuff);
            } else
            {
                s = DLRConsole.SafeFormat("{0} <!-- {1}  -->", s, escapedStuff);
            }
            return s;
        }

        public string GetSourceWithTopic(PrintOptions printOptions)
        {
            string s = "";
            XmlNode topic1 = this.TopicXml;
            string insideTopic = printOptions.InsideTopic;
            string thisTopic = Topic ?? insideTopic;
            thisTopic = StaticXMLUtils.GetAttribValue(topic1, "name,topic", () => thisTopic, null);
            bool hasTopic = (thisTopic != insideTopic);
            if (hasTopic)
            {
                s += "<topic name=\"";                                    
                s += thisTopic;
                s += "\">";
            }
            else if (Topic != null && !Topic.IsCatchAll)
            {
                hasTopic = true;
                s += "<topic name=\"";
                string n = (string)Topic.FullPath;
                s += n;
                s += "\">";
            }

            s += printOptions.FormatXML(CategoryXml);
            s += GetRuleStrings;

            if (hasTopic) s += "</topic>";
            printOptions.InsideTopic = insideTopic;
            return s;
        }

        public string LineNumberInfo
        {
            get
            {
                return StaticXMLUtils.GetLineNumberOfXmlNode(srcNode);
            }
        }

        public abstract string GetRuleStrings { get; }

        protected virtual string PreconditionKey { get; set; }

        #endregion

        public abstract int CompareTo(TemplateInfo other);

        public override string ToString()
        {
            return CategoryXml.OuterXml + " " + StaticXMLUtils.LocationEscapedInfo(CategoryXml);
        }

        public string XMLInfo()
        {
            return CategoryXml.OuterXml;
        }

        public void AddTemplate(TemplateInfo templateInfo)
        {
            //if (Template != null && Template != templateInfo) throw new InvalidCastException("non null " + Template);
            //Template = templateInfo;
            //  TemplateInfos.Add(templateInfo);
        }

        public static CategoryInfo GetCategoryInfo(PatternInfo info, XmlNode node, LoaderOptions filename, XmlNode templateNode,
            ResponseInfo template, GuardInfo guard, TopicInfo topicInfo, Node patternNode, ThatInfo thatInfo, IEnumerable<ConversationCondition> conds)
        {
            return filename.CtxGraph.FindCategoryInfo(info, node, filename, templateNode, template, guard, topicInfo,
                                                      patternNode, thatInfo, conds);
        }

        public static CategoryInfo MakeCategoryInfo(PatternInfo info, XmlNode cateNode, LoaderOptions filename,
            XmlNode templateNode, ResponseInfo template, GuardInfo guard, TopicInfo topicInfo, Node patternNode, ThatInfo thatInfo, IEnumerable<ConversationCondition> conds)
        {
            if (NoInfo) return null;
            var vv = new TemplateInfoImpl(info, cateNode, templateNode, filename, template, guard, topicInfo, patternNode,
                                      thatInfo);
            vv.AddRules(conds);
            return vv;
        }

        internal void Check()
        {
            throw new NotImplementedException();
        }

        public virtual bool Matches(string pattern)
        {
            if (pattern == null || pattern == "*" || pattern == "") return true;
            string s = ToFileString(PrintOptions.VERBOSE_FOR_MATCHING);
            if (pattern.Contains(s)) return true;
            return Regex.Matches(s, pattern).Count > 0;
        }

        public void AddPrecondition(ConversationCondition info)
        {
            if (info == null)
            {
                return;
            }
            if (Preconds == null) Preconds = new List<ConversationCondition>();
            if (Preconds.Contains(info)) return;
            Preconds.Add(info);
        }

        public void AddPrecondition(ThatInfo info)
        {
            if (info == null)
            {
                return;
            } 
            AddPrecondition(new ConversationCondition(info.PatternNode));
        }

        public void SetCategoryTag(Unifiable generatedPath, PatternInfo patternInfo, CategoryInfo category,
                                   XmlNode outerNode, XmlNode templateNode, GuardInfo guard, ThatInfo thatInfo)
        {
#if false
            var node = this.RootNode;
            if (SilentTagsInPutParent && AIMLLoader.IsSilentTag(templateNode))
            {
                GraphMaster parent1 = makeParent();
                this.Parents.Add(parent1);
                parent1.Size++;
                node = parent1.RootNode;
                writeToLog("Adding to Parent " + category);
            }
            Node created = Node.addCategoryTag(node, generatedPath, patternInfo,
                                category, outerNode, templateNode, guard, thatInfo, this);

            this.Size++;
#endif
            // keep count of the number of categories that have been processed
        }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            GraphMaster useRef = InGraph ?? Graph;
            if (useRef == null)
            {
                TemplateInfo ci = obj as TemplateInfo;
                if (ci != null)
                {
                    useRef = ci.InGraph ?? ci.Graph;
                }
            }
            if (useRef == null || useRef.CompareTemplatesForReferenceIdentity)
            {
                return ReferenceEquals(this, obj);
            }
            return Equals(obj as CategoryInfo, true, false);
        }
        public bool Equals(CategoryInfo other, bool compareGraphs, bool comparePaths)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            bool vv;
            var otherNode = other.GraphmasterNode;
            var GraphmasterNode = this.GraphmasterNode;
            if (!comparePaths)
            {
                if (otherNode == null || GraphmasterNode == null)
                {
                    comparePaths = true;
                    compareGraphs = false;
                }
            }
            if (compareGraphs)
            {
                vv = ReferenceEquals(otherNode, GraphmasterNode);
                if (!vv) return false;
            }
            else
            {
                vv = (otherNode ?? GraphmasterNode) == (GraphmasterNode ?? otherNode);
                if (!vv)
                {
                    return false;
                }
            }
            if (comparePaths)
            {
                vv = Unifiable.SAME_MEANING(other.Pattern, Pattern);
                if (!vv) return false;
                vv = Unifiable.SAME_MEANING(other.That, That) && Unifiable.SAME_MEANING(other.Topic, Topic);
                if (!vv) return false;
            }
            if (!SameTemplate(other.Template)) return false;
            if (!SamePreconds(other.Preconds)) return false;
            if (CategoryXml.OuterXml != other.CategoryXml.OuterXml)
            {
                return false;
            }
            if (Filename != other.Filename)
            {
                return true;
            }
            return true;
        }

        private bool SamePreconds(List<ConversationCondition> otherPreconds)
        {
            if (Preconds == null || Preconds.Count == 0)
            {
                return otherPreconds == null || otherPreconds.Count == 0;
            }
            if (otherPreconds == null || otherPreconds.Count == 0)
            {
                return Preconds.Count == 0;
            }
            lock (Preconds)
            {
                lock (otherPreconds)
                    foreach (var precond in Preconds)
                    {
                        if (!otherPreconds.Contains(precond)) return false;
                    }
            }
            return true;
        }

        virtual public bool SameTemplate(TemplateInfo other)
        {
            if (!ReferenceEquals(Template, this))
            {
                if (!Equals(other, Template))
                {
                    return false;
                }
            }
            else
            {
                if (!Unifiable.SAME_MEANING(other.Response, Response)) return false;
                if (!Unifiable.SAME_MEANING(other.Guard, Guard)) return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (Preconds != null ? Preconds.GetHashCode() : 0);
                result = (result*397) ^ (Pattern != null ? Pattern.GetHashCode() : 0);
                result = (result*397) ^ (That != null ? That.GetHashCode() : 0);
                result = (result*397) ^ (Topic != null ? Topic.GetHashCode() : 0);
                result = (result*397) ^ (GraphmasterNode != null ? GraphmasterNode.GetHashCode() : 0);
                return result;
            }
        }
    }

    public interface CategoryInfo : IAIMLInfo, IndexTarget
    {
        TopicInfo Topic { get; }
        PatternInfo Pattern { get; }
        ThatInfo That { get; set; }

        List<ConversationCondition> Preconds { get; }

        bool IsDisabled { get; set; }
        bool IsDisabledOutput { get; set; }
        bool IsSearchDisabled { get; set; }

        XmlNode CategoryXml { get; }

        TemplateInfo Template { get; }

        
        bool IsTraced { get; set; }
        Node GraphmasterNode { get; set; }
        Unifiable Filename { get; }
        ResponseInfo Response { get; set; }

        //  void SetCategoryTag(Unifiable categoryPath, PatternInfo patternInfo, CategoryInfo categoryInfo, XmlNode outerNode, XmlNode templateNode, GuardInfo guard, ThatInfo thatInfo);
        bool Matches(string match);
        void AddPrecondition(ThatInfo node);
        void AddPrecondition(ConversationCondition node);
        void AddTemplate(TemplateInfo newTemplateInfo);
    }
}