using System;
using System.Text;
using System.Text.RegularExpressions;
using MushDLR223.ScriptEngines;
using MushDLR223.Utilities;
using RTParser.Variables;

namespace RTParser.Normalize
{
    /// <summary>
    /// Checks the text for any matches in the bot's substitutions dictionary and makes
    /// any appropriate changes.
    /// </summary>
    public class ApplySubstitutions : RTParser.Utils.TextTransformer
    {
        public ApplySubstitutions(RTParser.RTPBot bot, Unifiable inputString)
            : base(bot, inputString)
        { }

        public ApplySubstitutions(RTParser.RTPBot bot)
            : base(bot)
        { }

        /// <summary>
        /// Produces a random "marker" Unifiable that tags text that is already the result of a substitution
        /// </summary>
        /// <param name="len">The length of the marker</param>
        /// <returns>the resulting marker</returns>
        private static Unifiable getMarker(int len)
        {
            char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
            StringBuilder result = new StringBuilder();
            Random r = new Random(len);
            for (int i = 0; i < len; i++)
            {
                result.Append(chars[r.Next(chars.Length)]);
            }
            return result.ToString();
        }

        protected override Unifiable ProcessChange()
        {
            if (inputString != null) return inputString;
            return ApplySubstitutions.Substitute(this.Proc.InputSubstitutions, this.inputString);
        }

        /// <summary>
        /// Static helper that applies replacements from the passed dictionary object to the 
        /// target Unifiable
        /// </summary>
        /// <param name="bot">The bot for whom this is being processed</param>
        /// <param name="dictionary">The dictionary containing the substitutions</param>
        /// <param name="target">the target Unifiable to which the substitutions are to be applied</param>
        /// <returns>The processed Unifiable</returns>
        public static string Substitute(ISettingsDictionary dictionary, string target)
        {
            string marker = ApplySubstitutions.getMarker(5);
            string markerSP = ApplySubstitutions.getMarker(3);
            string result = " " + Unifiable.ToVMString(target) + " ";
            result = SubstituteResults(dictionary, marker, markerSP,result, false);
            return result.Replace(marker, "").Replace(markerSP, " ");
        }

        private static string SubstituteResults(ISettingsDictionary dictionary, string marker, string markerSP, string result, bool backwards)
        {
            System.Collections.Generic.IEnumerable<string> dictionarySettingNames = dictionary.SettingNames(0);

            int did = 0;
            foreach (string settingName in dictionarySettingNames)
            {
                if (did++ > 200) break;
                var grabSetting = dictionary.grabSetting(settingName);

                string fromValue = settingName;
                string toValue = grabSetting.AsString();

                // reverse the to/from
                if (backwards)
                {
                    string swapValue = toValue;
                    toValue = fromValue;
                    fromValue = swapValue;
                }

                if (string.IsNullOrEmpty(fromValue.Trim())) continue;

                toValue = GetToValue(toValue);

                string replacement;
                if (toValue.Trim().Length == 0)
                {
                    replacement = " ";
                }
                else
                {
                    toValue = toValue.Trim();
                    if (toValue.ToLower().Replace("\\b", " ").Trim() == fromValue.ToLower().Replace("\\b", " ").Trim())
                    {
                        continue;
                    }
                    replacement = marker + toValue.Replace(" ", markerSP) + marker;
                }


                string finalPattern = GetFinalPattern(fromValue);
                //if (Regex.IsMatch(result, finalPattern, RegexOptions.IgnoreCase))
                {
                    string testResult = Regex.Replace(result, finalPattern, replacement, RegexOptions.IgnoreCase);
                    if (testResult != result)
                    {
                        if (!Regex.IsMatch(result, finalPattern, RegexOptions.IgnoreCase))
                        {
                            OutputDelegate to = DLRConsole.DebugWriteLine;
                            to("\n  SUBST :");
                            to("   R: '{0}'", result);
                            to("  PT: '{0}'", fromValue);
                            to("   V: '{0}'", toValue);
                            to("   M: '{0}'", finalPattern);
                            to("  PR: '{0}'", replacement);
                            to("  RS: '{0}'", testResult);
                            to("  TS: '{0}'", testResult.Replace(marker, "").Replace(markerSP, " "));
                        }
                    }
                    result = testResult;
                }
                
            }
            return result;
        }

        private static string GetToValue(string toValue)
        {
            if (toValue.Contains("\\b"))
            {
                writeDebugLine("REGEX ERROR: to=" + toValue);
                toValue = toValue.Replace("\\b", " ");
            }
            toValue = toValue.Replace("\t", " ");
            toValue = toValue.Replace("\r", " ");
            toValue = toValue.Replace("\n", " ");
            return toValue;
        }

        private static string GetFinalPattern(string fromValue)
        {
            // good regex
            if (fromValue.Contains("\\b")) return fromValue;
            string fromValueTrim = fromValue.Trim();
            // probably good regex
            if (fromValueTrim.Contains("\\") && fromValueTrim.Length > 1) return fromValue;
            //Unifiable match = "\\b"+@p2.Trim().Replace(" ","\\s*")+"\\b";
            return "\\b" + makeRegexSafe(fromValueTrim) + "\\b";
        }

        public static string SubstituteRecurse(RTParser.RTPBot bot, SettingsDictionary dictionary, string target)
        {
            string result = Unifiable.ToVMString(target);
            String prev = "";
            while (prev != result)
            {
                prev = result;
                result = Substitute(dictionary, target);
            }
            return  new StringUnifiable(result);
        }

        /// <summary>
        /// Given an input, escapes certain characters so they can be used as part of a regex
        /// </summary>
        /// <param name="input">The raw input</param>
        /// <returns>the safe version</returns>
        private static string makeRegexSafe(string input)
        {
            string result = input;
            if (!input.Contains("\\"))
            {
                //.Replace("\\", "");
                result = result.Replace(")", "\\)");
                result = result.Replace("(", "\\(");
                result = result.Replace(".", "\\.");
                string regexEscape = Regex.Escape(input);
                if (result != regexEscape)
                {
                    //writeDebugLine("'{0}!=Regex.Escape('{1}')", result, Regex.Escape(input));
                    return regexEscape;
                }
            }
            return result;
        }
    }
}
