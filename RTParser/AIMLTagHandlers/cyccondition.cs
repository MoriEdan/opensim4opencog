using System;
using System.Xml;
using System.Text;
using System.Text.RegularExpressions;

namespace RTParser.AIMLTagHandlers
{
    /// <summary>
    /// The cyccondition element instructs the AIML interpreter to return specified contents depending 
    /// upon the results of matching a predicate against a pattern. 
    /// 
    /// NB: The cyccondition element has three different types. The three different types specified 
    /// here are distinguished by an xsi:type attribute, which permits a validating XML Schema 
    /// processor to validate them. Two of the types may contain li elements, of which there are 
    /// three different types, whose validity is determined by the type of enclosing cyccondition. In 
    /// practice, an AIML interpreter may allow the omission of the xsi:type attribute and may instead 
    /// heuristically determine which type of cyccondition (and hence li) is in use. 
    /// 
    /// Block Condition 
    /// ---------------
    /// 
    /// The blockCondition type of cyccondition has a required attribute "name", which specifies an AIML 
    /// predicate, and a required attribute "value", which contains a simple pattern expression. 
    ///
    /// If the contents of the value attribute match the value of the predicate specified by name, then 
    /// the AIML interpreter should return the contents of the cyccondition. If not, the empty Unifiable "" 
    /// should be returned.
    /// 
    /// Single-predicate Condition 
    /// --------------------------
    /// 
    /// The singlePredicateCondition type of cyccondition has a required attribute "name", which specifies 
    /// an AIML predicate. This form of cyccondition must contain at least one li element. Zero or more of 
    /// these li elements may be of the valueOnlyListItem type. Zero or one of these li elements may be 
    /// of the defaultListItem type.
    /// 
    /// The singlePredicateCondition type of cyccondition is processed as follows: 
    ///
    /// Reading each contained li in order: 
    ///
    /// 1. If the li is a valueOnlyListItem type, then compare the contents of the value attribute of 
    /// the li with the value of the predicate specified by the name attribute of the enclosing 
    /// cyccondition. 
    ///     a. If they match, then return the contents of the li and stop processing this cyccondition. 
    ///     b. If they do not match, continue processing the cyccondition. 
    /// 2. If the li is a defaultListItem type, then return the contents of the li and stop processing
    /// this cyccondition.
    /// 
    /// Multi-predicate Condition 
    /// -------------------------
    /// 
    /// The multiPredicateCondition type of cyccondition has no attributes. This form of cyccondition must 
    /// contain at least one li element. Zero or more of these li elements may be of the 
    /// nameValueListItem type. Zero or one of these li elements may be of the defaultListItem type.
    /// 
    /// The multiPredicateCondition type of cyccondition is processed as follows: 
    ///
    /// Reading each contained li in order: 
    ///
    /// 1. If the li is a nameValueListItem type, then compare the contents of the value attribute of 
    /// the li with the value of the predicate specified by the name attribute of the li. 
    ///     a. If they match, then return the contents of the li and stop processing this cyccondition. 
    ///     b. If they do not match, continue processing the cyccondition. 
    /// 2. If the li is a defaultListItem type, then return the contents of the li and stop processing 
    /// this cyccondition. 
    /// 
    /// ****************
    /// 
    /// Condition List Items
    /// 
    /// As described above, two types of cyccondition may contain li elements. There are three types of 
    /// li elements. The type of li element allowed in a given cyccondition depends upon the type of that 
    /// cyccondition, as described above. 
    /// 
    /// Default List Items 
    /// ------------------
    /// 
    /// An li element of the type defaultListItem has no attributes. It may contain any AIML template 
    /// elements. 
    ///
    /// Value-only List Items
    /// ---------------------
    /// 
    /// An li element of the type valueOnlyListItem has a required attribute value, which must contain 
    /// a simple pattern expression. The element may contain any AIML template elements.
    /// 
    /// Name and Value List Items
    /// -------------------------
    /// 
    /// An li element of the type nameValueListItem has a required attribute name, which specifies an 
    /// AIML predicate, and a required attribute value, which contains a simple pattern expression. The 
    /// element may contain any AIML template elements. 
    /// </summary>
    public class cyccondition : RTParser.Utils.AIMLTagHandler
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
        public cyccondition(RTParser.RTPBot bot,
                        RTParser.User user,
                        RTParser.Utils.SubQuery query,
                        RTParser.Request request,
                        RTParser.Result result,
                        XmlNode templateNode)
            : base(bot, user, query, request, result, templateNode)
        {
            this.isRecursive = false;
        }

        protected override Unifiable ProcessChange()
        {
            if (this.templateNode.Name.ToLower() == "cyccondition")
            {
                // heuristically work out the type of cyccondition being processed

                if (this.templateNode.Attributes.Count == 2) // block
                {
                    string name = String.Empty;
                    Unifiable value = Unifiable.Empty;

                    if (this.templateNode.Attributes[0].Name == "name")
                    {
                        name = this.templateNode.Attributes[0].Value;
                    }
                    else if (this.templateNode.Attributes[0].Name == "value")
                    {
                        value = this.templateNode.Attributes[0].Value;
                    }

                    if (this.templateNode.Attributes[1].Name == "name")
                    {
                        name = this.templateNode.Attributes[1].Value;
                    }
                    else if (this.templateNode.Attributes[1].Name == "value")
                    {
                        value = this.templateNode.Attributes[1].Value;
                    }

                    if ((name.Length > 0) & (!value.IsEmpty))
                    {
                        Unifiable actualValue = this.request.Predicates.grabSetting(name);
                        //Regex matcher = new Regex(value.Replace(" ", "\\s").Replace("*", "[\\sA-Z0-9]+"), RegexOptions.IgnoreCase);
                        //if (matcher.IsMatch(actualValue))
                        if (value.IsMatch(actualValue))
                        {
                            return Unifiable.InnerXmlText(templateNode);
                        }
                    }
                }
                else if (this.templateNode.Attributes.Count == 1) // single predicate
                {
                    if (this.templateNode.Attributes[0].Name == "name")
                    {
                        string name = this.templateNode.Attributes[0].Value;
                        foreach (XmlNode childLINode in this.templateNode.ChildNodes)
                        {
                            if (childLINode.Name.ToLower() == "li")
                            {
                                if (childLINode.Attributes.Count == 1)
                                {
                                    if (childLINode.Attributes[0].Name.ToLower() == "value")
                                    {
                                        Unifiable actualValue = this.request.Predicates.grabSetting(name);
                                        Unifiable value = childLINode.Attributes[0].Value;
                                        //Regex matcher = new Regex(value.Replace(" ", "\\s").Replace("*", "[\\sA-Z0-9]+"), RegexOptions.IgnoreCase);
                                        //if (matcher.IsMatch(actualValue))
                                        if (value.IsMatch(actualValue))
                                        {
                                            return Unifiable.InnerXmlText(childLINode);
                                        }
                                    }
                                }
                                else if (childLINode.Attributes.Count == 0)
                                {
                                    return Unifiable.InnerXmlText(childLINode);
                                }
                            }
                        }
                    }
                }
                else if (this.templateNode.Attributes.Count == 0) // multi-predicate
                {
                    foreach (XmlNode childLINode in this.templateNode.ChildNodes)
                    {
                        if (childLINode.Name.ToLower() == "li")
                        {
                            if (childLINode.Attributes.Count == 2)
                            {
                                string name = string.Empty;
                                Unifiable value = Unifiable.Empty;
                                if (childLINode.Attributes[0].Name == "name")
                                {
                                    name = childLINode.Attributes[0].Value;
                                }
                                else if (childLINode.Attributes[0].Name == "value")
                                {
                                    value = childLINode.Attributes[0].Value;
                                }

                                if (childLINode.Attributes[1].Name == "name")
                                {
                                    name = childLINode.Attributes[1].Value;
                                }
                                else if (childLINode.Attributes[1].Name == "value")
                                {
                                    value = childLINode.Attributes[1].Value;
                                }

                                if ((name.Length > 0) & (!value.IsEmpty))
                                {
                                    Unifiable actualValue = this.request.Predicates.grabSetting(name);
                                    if (value.IsMatch(actualValue))
                                    {
                                        return Unifiable.InnerXmlText(childLINode);
                                    }
                                }
                            }
                            else if (childLINode.Attributes.Count == 0)
                            {
                                return Unifiable.InnerXmlText(childLINode);
                            }
                        }
                    }
                }
            }
            return Unifiable.Empty;
        }
    }
}
