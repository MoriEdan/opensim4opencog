using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace RTParser.Utils
{
    public class PrintOptions
    {
        public PrintOptions()
        {
            //tw = new StringWriter(sw);
        }   
        public XmlWriterSettings XMLWriterSettings = new XmlWriterSettings();

        public string CurrentGraphName;

        public bool InsideAiml = false;

        public XmlWriter GetXMLTextWriter(TextWriter w)
        {
            lock (this)
            {
                if (w != lastW)
                {
                    lastW = w;
                    currentWriter = XmlWriter.Create(w, XMLWriterSettings);
                }
            }
            return currentWriter;
        }

        public bool IncludeLinenoPerNode = true;
        public bool IncludeFileNamePerNode = true;

        public bool RemoveDuplicates = true;       
        public bool CleanWhitepaces = true;
        public bool IncludeLineInfoExternal = true;
        public bool GroupFileElements = false;
        public bool CategoryPerLine = false;
        public bool IncludeGraphName = true;
        public List<object> dontPrint = new List<object>(); 
        public string InsideThat = null;
        public string InsideTopic = null;
        public string InsideFilename = null;
        public Request RequestImplicits;
        readonly public StringBuilder sw = new StringBuilder();
        //readonly public StringWriter tw;

        public static PrintOptions CONSOLE_LISTING = new PrintOptions();
        public static PrintOptions VERBOSE_FOR_MATCHING = new PrintOptions();

        public static PrintOptions SAVE_TO_FILE = new PrintOptions()
                                                      {
                                                          CategoryPerLine = false,
                                                      };

        private XmlWriter currentWriter;
        private TextWriter lastW;
        public bool WriteDisabledItems = true;
        public bool WriteStatistics = true;

        public string FormatXML(XmlNode srcNode)
        {
            lock (sw)
            {
                sw.Length = 0;
                sw.Capacity = 100000;
                //var tw = new StringWriter(sw);
                XMLWriterSettings.Encoding = Encoding.ASCII;
                XMLWriterSettings.CloseOutput = false;
                XMLWriterSettings.OmitXmlDeclaration = true;

                XMLWriterSettings.IndentChars = " ";// = true;
                XMLWriterSettings.Indent = false;
                var v = XmlWriter.Create(sw, XMLWriterSettings);
                //v.Formatting = Formatting.Indented;
                //v.Namespaces = false;
               

                
                try
                {
                    srcNode.WriteTo(v);
                    v.Flush();
                    var s = sw.ToString();
                    return s;
                }
                catch (Exception exception)
                {                    
                    throw exception;
                }
            }
        }

        private string hide = "";
        public bool DontPrint(object cws)
        {
            if (hide.Contains("" + cws)) return true;
            return false;
        }

        public void Writting(object cws)
        {
            if (hide.Length > 30000)
                hide = "" + cws;
            else
                hide += "" + cws;
        }

        public void ClearHistory()
        {
            InsideAiml = false;
            InsideTopic = null;
            InsideThat = null;
            InsideFilename = null;
            CurrentGraphName = null;
            dontPrint.Clear();
            sw.Length = 0;
            sw.Capacity = 100000;
            hide = "";
        }
    }
}