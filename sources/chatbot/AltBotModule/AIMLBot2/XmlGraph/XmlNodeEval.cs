using System.Collections.Generic;
using System.Xml;
using AltAIMLbot.Utils;
using MushDLR223.ScriptEngines;

namespace AltAIMLbot.Utils
{
    public delegate IEnumerable<XmlNode> XmlNodeEval(XmlNode src, Request request, OutputDelegate outputDelegate);
}