using System;
using System.Collections.Generic;
using System.Xml;
using System.Text;
using AltAIMLParser;
using AltAIMLbot;
using AltAIMLbot.Utils;

/******************************************************************************************
AltAIMLBot -- Copyright (c) 2011-2012,Kino Coursey, Daxtron Labs

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
**************************************************************************************************/

namespace AltAIMLParser
{
}

namespace AltAIMLbot.AIMLTagHandlers
{
    public class behavior : Utils.AIMLTagHandler
    {

        public behavior(AltBot bot,
                User user,
                Utils.SubQuery query,
                Request request,
                Result result,
                XmlNode templateNode)
            : base(bot, user, query, request, result, templateNode)
        {
            this.isRecursive = false;
        }



        protected override string ProcessChange()
        {
            if (this.TemplateNodeName == "behavior")
            {
                // Simply pass the contents to the defineBehavior
                try
                {
                    String templateNodeTotalValue = this.TemplateNodeOuterXml;
                    String myName = "root";
                    try
                    {
                        if (TemplateNodeAttributes["id"] != null)
                        {
                            myName = TemplateNodeAttributes["id"].Value;
                        }
                    }
                    catch
                    {
                        myName = "root";
                    }


                    this.user.rbot.sm.defineBehavior((string)myName, (string)templateNodeTotalValue);
                }
                catch
                {
                }

            }
            return String.Empty;

        }
    }
}
