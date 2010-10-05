﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using MushDLR223.ScriptEngines;
using MushDLR223.Utilities;
using RTParser.Utils;
using RTParser.Variables;

namespace RTParser.Database
{
    public class TripleStoreFromEnglish : ITripleStore, IEntityFilter
    {
        private readonly HashSet<string> ExcludeRels = new HashSet<string>();

        private readonly HashSet<string> ExcludeVals = new HashSet<string>();

        private void AddDefaultExclusions()
        {
            foreach (string str in
                new[]
                    {
                        "topic",
                        "he",
                        "she",
                        "name",
                        "id",
                        "username",
                        "userid",
                        "they",
                        "",
                        "it",
                        // because  "$user emotion is $value" should be "$bot feels $value emotion towards $user"
                        "emotion", 
                        "it",
                        "they",
                    })
            {
                AddExcludedRelation(str);
            }
            foreach (string str in
                new[]
                    {
                        "unknown_user",
                        "unknown.*",
                    })
            {
                AddExcludedValue(str);
            }
        }

        private readonly IEnglishFactiodEngine EnglishFactiodStore;
        readonly RTPBot TheBot;
        private readonly WordExpander WordNetExpand;
        private bool ExtremeDebug;
        private XmlNode templateNodeInit;
        public IEntityFilter EntityFilter { get; private set; }

        public TripleStoreFromEnglish(IEnglishFactiodEngine englishFactiodStore, RTPBot theBot, WordExpander expander)
        {
            EntityFilter = this;
            TheBot = theBot;
            EnglishFactiodStore = englishFactiodStore;
            WordNetExpand = expander;
            AddDefaultExclusions();
        }

        public int assertTriple(string subject, string relation, string value)
        {
            var templateNode = templateNodeInit;
            string factoidSRV = GenFormatFactoid(subject, relation, value, templateNode);
            if (IsExcludedSRV(subject, relation, value, factoidSRV, writeToLog, "assertTriple")) return -1;
            return EnglishFactiodStore.InsertFactiod(factoidSRV, templateNode, WordNetExpand);
        }

        public int retractTriple(string subject, string relation, string value)
        {
            if (!EnglishFactiodStore.IsDbPresent) return 0;
            var templateNode = templateNodeInit;
            string factoidSRV = GenFormatFactoid(subject, relation, value, templateNode);
            if (IsExcludedSRV(subject, relation, value, factoidSRV, writeToLog, "retractTriple")) return -1;
            return EnglishFactiodStore.DeleteTopScoring(factoidSRV, templateNode, true);
        }

        public int retractAllTriple(string subject, string relation)
        {
            if (!EnglishFactiodStore.IsDbPresent) return 0;
            var templateNode = templateNodeInit;
            string factoidSR = GenFormatFactoid(subject, relation, "", templateNode);
            if (IsExcludedSRV(subject, relation, "", factoidSR, writeToLog, "retractAllTriple")) return -1;
            return EnglishFactiodStore.DeleteTopScoring(factoidSR, templateNode, true);
        }

        public int updateTriple(string subject, string relation, string value)
        {
            var templateNode = templateNodeInit;
            string factoidSRV = GenFormatFactoid(subject, relation, value, templateNode);
            string factoidSR = GenFormatFactoid(subject, relation, "", templateNode);
            if (IsExcludedSRV(subject, relation, "", factoidSRV,
                              writeToLog, "updateTriple {0} => ", factoidSR)) return -1;
            int deleted = EnglishFactiodStore.DeleteTopScoring(factoidSR, templateNode, true);

            return deleted + EnglishFactiodStore.InsertFactiod(factoidSRV, templateNode, WordNetExpand);
        }

        public String queryTriple(string subject, string relation, XmlNode templateNode)
        {
            templateNode = templateNode ?? this.templateNodeInit;
            string factoidSR = GenFormatFactoid(subject, relation, "", templateNode);
            if (IsExcludedSRV(subject, relation, "", factoidSR, writeToLog, "queryTriple")) return String.Empty;

            float threshold = 0.5f;
            float minTerms = 0.3f;
            bool expandWithWordNet = true;
            bool expandOnNoHits = false;

            bool tf;
            if (StaticXMLUtils.TryParseBool(templateNode, "expand", out tf))
            {
                expandOnNoHits = tf;
            }
            if (StaticXMLUtils.TryParseBool(templateNode, "wordnet,synonyms", out tf))
            {
                expandWithWordNet = tf;
            }
            float reliability;
            string result = EnglishFactiodStore.AskQuery(factoidSR, writeToLog, (any) => null, templateNode, threshold,
                                                         expandWithWordNet, expandOnNoHits, out reliability);
            if (!string.IsNullOrEmpty(result) && result.ToLower().StartsWith(factoidSR.ToLower()) &&
                reliability > threshold)
            {
                writeToLog("Success! queryTriple {0}, {1} => {2}", subject, relation, result);
                return result.Substring(factoidSR.Length).Trim();
            }
            writeToLog("queryTriple {0}, {1} => '{2}' (returning String.Empty)", subject, relation, result);
            return String.Empty;
        }

        public int assertDictionary(string subject, SettingsDictionary dictionary)
        {
            int asserts = 0;
            foreach (var relation in dictionary.SettingNames(1))
            {
                Unifiable value = dictionary.grabSettingNoDebug(relation);
                asserts += updateTriple(subject, relation, value);
            }
            return asserts;
        }
        public string GenFormatFactoid(string subject, string relation, string value, XmlNode templateNode)
        {
            string subj = Entify(subject);
            var dictionary = TheBot.GetDictionary(subj) as SettingsDictionary;

            bool noValue = string.IsNullOrEmpty(value);
            Unifiable formatter;
            {
                if (noValue)
                {
                    // query mode
                    formatter = GetDictValue(dictionary, relation, "format-query");
                }
                else
                {
                    // assert mode
                    formatter = GetDictValue(dictionary, relation, "format-assert");
                }
            }

            if (Unifiable.IsNullOrEmpty(formatter) || Unifiable.IsTrueOrYes(formatter) || formatter == "default")
            {
                formatter = " {0} {1} is {2} ";
            }

            if (Unifiable.IsFalseOrNo(formatter))
            {
                return "false";
            }

            return ExpandFormat(subj, relation, value, noValue, dictionary, formatter, templateNode);
        }

        public string ExpandFormat(string subj, string relation, string value, bool isQuery, SettingsDictionary dictionary, Unifiable formatter, XmlNode templateNode)
        {
            string pred = Entify(relation);

            string botName = !IsBotRobot(subj) ? Entify(TheBot.UserID) : Entify(TheBot.LastUser.UserID);
            {

                var whword = GetDictValue(dictionary, relation, "format-whword");

                if (!Unifiable.IsNullOrEmpty(whword) && isQuery) value = whword;

                if (Unifiable.IsFalseOrNo(formatter))
                {
                    return "false";
                }

                formatter = ReplaceWord(formatter, "$subject", "{0}");
                formatter = ReplaceWord(formatter, "$verb", "{1}");
                formatter = ReplaceWord(formatter, "$object", "{2}");

                formatter = ReplaceWord(formatter, "$user", "{0}");
                formatter = ReplaceWord(formatter, "$relation", "{1}");
                formatter = ReplaceWord(formatter, "$value", "{2}");

                formatter = ReplaceWord(formatter, "$predicate", pred);

                formatter = ReplaceWord(formatter, "$set-return",
                                        TheBot.RelationMetaProps.grabSettingNoDebug(relation + "." + "set-return"));
                formatter = ReplaceWord(formatter, "$default", TheBot.DefaultPredicates.grabSettingNoDebug(relation));

                formatter = ReplaceWord(formatter, "$botname", botName);
                formatter = ReplaceWord(formatter, "$bot", botName);
            }

            string english = " " + String.Format(formatter, subj, pred, Entify(value)).Trim() + " ";
            english = ReplaceWord(english, "I", subj);
            english = ReplaceWord(english, "you", botName);
            english = english.Trim();
            User user = TheBot.FindUser(subj);
            english = FixPronouns(english, user.grabSettingNoDebug);
            return english;
        }

        public bool IsBotRobot(string subject)
        {
            User user = TheBot.FindUser(subject);
            if (user == null) return false;
            if (user == TheBot.BotAsUser) return true;
            if (!user.IsRoleAcct) return false;
            return false;
        }

        public static string ReplaceWord(string formatter, string find, string replace)
        {
            formatter = ReplaceInsensitive(formatter, " " + find + "'", " " + replace + "'");
            formatter = ReplaceInsensitive(formatter, " " + find + " ", " " + replace + " ");
            return formatter;
        }

        private static string ReplaceInsensitive(string original,
                            string pattern, string replacement)
        {
            int count, position0, position1;
            count = position0 = position1 = 0;
            string upperString = original.ToUpper();
            string upperPattern = pattern.ToUpper();
            int inc = (original.Length / pattern.Length) *
                      (replacement.Length - pattern.Length);
            char[] chars = new char[original.Length + Math.Max(0, inc)];
            while ((position1 = upperString.IndexOf(upperPattern,
                                                    position0)) != -1)
            {
                for (int i = position0; i < position1; ++i)
                    chars[count++] = original[i];
                for (int i = 0; i < replacement.Length; ++i)
                    chars[count++] = replacement[i];
                position0 = position1 + pattern.Length;
            }
            if (position0 == 0) return original;
            for (int i = position0; i < original.Length; ++i)
                chars[count++] = original[i];
            return new string(chars, 0, count);
        }


        public string Entify(string subject)
        {
            if (string.IsNullOrEmpty(subject)) return "";
            string subj = subject.Replace("_", " ");
            subj = subject.Replace(".", " ");
            return subj.Trim();
        }

        public Unifiable GetDictValue(SettingsDictionary dict, string relation, string meta)
        {
            if (dict != null)
            {
                string formatter = dict.GetMeta(relation, meta);
                if (!TextPatternUtils.IsNullOrEmpty(formatter))
                    return formatter;
            }
            string prop = relation + "." + meta;
            return TheBot.RelationMetaProps.grabSetting(prop);
        }



        private bool IsExcludedSRV(string subject, string relation, string value, string factoidSRV,
                                   OutputDelegate writeToLog, string fmtString, params object[] fmtArgs)
        {
            bool ExcludedFactPattern = false;
            bool debug = (writeToLog != null);
            fmtString = DLRConsole.SafeFormat(fmtString, fmtArgs);
            if (factoidSRV == "false")
            {
                if (!debug) return true;
                writeToLog("ExcludedSRV: '{0}' Format '{1}'", fmtString, relation);
                ExcludedFactPattern = true;
            }
            fmtString += " " + factoidSRV;
            if (IsExcludedRelation(relation))
            {
                if (!debug) return true;
                writeToLog("ExcludedSRV: '{0}' Relation '{1}'", fmtString, relation);
                if (!ExtremeDebug) return true;
                ExcludedFactPattern = true;
            }
            if (IsExcludedSubject(subject))
            {
                if (!debug) return true;
                writeToLog("ExcludedSRV: '{0}' Subject '{1}'", fmtString, subject);
                if (!ExtremeDebug) return true;
                ExcludedFactPattern = true;
            }
            if (IsExcludedValue(value))
            {
                if (!debug) return true;
                writeToLog("ExcludedSRV: '{0}' Value '{1}'", fmtString, value);
                ExcludedFactPattern = true;
            }
            if (debug) writeToLog(factoidSRV + " " + fmtString, fmtArgs);
            return ExcludedFactPattern;
        }

        public bool IsExcludedSubject(string subject)
        {
            if (string.IsNullOrEmpty(subject)) return true;
            return IsExcludedValue(subject);
        }

        public bool IsExcludedRelation(string value)
        {
            if (string.IsNullOrEmpty(value)) return true;
            if (AIMLTagHandler.ReservedAttributes.Contains(value)) return true;
            return NullOrMatchInSet(ExcludeRels, value);
        }

        public void AddExcludedRelation(string value)
        {
            AddRegex(ExcludeRels, "^" + value + "$");
        }

        public void AddExcludedValue(string value)
        {
            AddRegex(ExcludeVals, "^" + value + "$");
        }

        private static void AddRegex(ICollection<string> rels, string value)
        {
            lock (rels)
            {
                value = value.ToLower();
                value = value.Replace("_", " ");
                value = value.Replace("~", ".*");
                rels.Add(value);
            }
        }

        private static bool NullOrMatchInSet(ICollection<string> rels, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                if (rels == null) return true;
                lock (rels) return rels.Contains("") || rels.Contains("^$");
            }
            if (rels == null) return false;
            lock (rels)
            {
                value = value.Replace("_", " ").ToLower();
                foreach (var rel in rels)
                {
                    if (Regex.IsMatch(value, rel))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool IsExcludedValue(string value)
        {
            return NullOrMatchInSet(ExcludeVals, value);
        }

        private void writeToLog(string s, params object[] p)
        {
            //bool tempB = TheBot.IsLogging;
            //TheBot.IsLogging = true;
            //TheBot.writeToLog("LUCENE: " + s, p);
            //TheBot.IsLogging = tempB;
            s = DLRConsole.SafeFormat(s, p);

            if (s.ToUpper().Contains("EXCLUDE")) return;
            DLRConsole.DebugWriteLine("TRIPLESTORE: " + s);
        }

        internal string FixPronouns(string english, Func<string, Unifiable> whoAmI)
        {
            string englishToLower = english.ToLower();
            {
                foreach (var pronoun in
                    new[]
                        {
                            "him", "he", "she", "her", "them", "they", "it", "this", 
                            "i", "you", "me", "my", "your", "our","their",
                        })
                {
                    if (englishToLower.Contains(pronoun))
                    {
                        if (whoAmI == null)
                        {
                            writeToLog("FixPronouns: DONT KNOW THE USER TO RESOLVE '" + pronoun + "'");
                            return english;
                        }
                        Unifiable v = Unifiable.Create(whoAmI(pronoun));
                        bool goodReplacement = !Unifiable.IsIncomplete(v) && !Unifiable.IsNullOrEmpty(v) && !Unifiable.IsMissing(v);
                        if (!goodReplacement)
                        {
                            writeToLog("FixPronouns: BAD REPLACEMENT '" + pronoun + "' => '" + v + "'");
                            continue;
                        }
                        english = ReplaceWord(english, pronoun, v);
                        english = ReplaceWord(english, pronoun + "s", v + "s");
                    }
                }

            }
            return english;
        }
    }
}