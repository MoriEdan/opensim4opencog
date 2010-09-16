using System;
using System.Text;
using System.Collections.Generic;
using RTParser.Utils;
using RTParser.Variables;
using SPLITTER = System.Func<string[]>;
using Unifiable = System.String;
namespace RTParser.Normalize
{
    /// <summary>
    /// Splits the raw input into its constituent sentences. Split using the tokens found in 
    /// the bots Splitters Unifiable array.
    /// </summary>
    public class SplitIntoSentences
    {
        /// <summary>
        /// The bot this sentence splitter is associated with
        /// </summary>
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        private SPLITTER bot;
        // ReSharper restore FieldCanBeMadeReadOnly.Local

        /// <summary>
        /// The raw input Unifiable
        /// </summary>
        private Unifiable inputString;

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="bot">The bot this sentence splitter is associated with</param>
        /// <param name="inputString">The raw input Unifiable to be processed</param>
        public SplitIntoSentences(SPLITTER bot, Unifiable inputString)
        {
            this.bot = bot;
            this.inputString = inputString;
        }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="bot">The bot this sentence splitter is associated with</param>
        public SplitIntoSentences(SPLITTER bot)
        {
            this.bot = bot;
        }

        /// <summary>
        /// Splits the supplied raw input into an array of strings according to the tokens found in
        /// the bot's Splitters List<>
        /// </summary>
        /// <param name="inputString">The raw input to split</param>
        /// <returns>An array of strings representing the constituent "sentences"</returns>
        public Unifiable[] Transform(Unifiable inputString0)
        {
            this.inputString = inputString0;
            return this.Transform();
        }

        /// <summary>
        /// Splits the raw input supplied via the ctor into an array of strings according to the tokens
        /// found in the bot's Splitters List<>
        /// </summary>
        /// <returns>An array of strings representing the constituent "sentences"</returns>
        public Unifiable[] Transform()
        {
            string[] tokens = null;
            if (bot != null)
            {
                tokens = bot();
            }
            string inputStringString = this.inputString.AsString();
            Unifiable[] nodes = this.inputString.ToArray();
            int currentTokenbNum = 0;
            int startTokenNum = 0;
            if (tokens == null)
            {
                List<Unifiable> tidyString = new List<Unifiable>();
                tokens = new[] { "! ", "? ", ". ", ", ", "<br>", "<p>" };
                foreach (string token in tokens)
                {
                    inputStringString = inputStringString.Replace(token, token + " <split/> ");
                }
                string[] sss = inputStringString.Split(new[] { "<split/>" },
                                                       System.StringSplitOptions.RemoveEmptyEntries);
                foreach (string rawSentence in sss)
                {
                    string tidySentence = rawSentence.Replace("<br>", " ").Replace("<p>", " ").Trim().Replace("  ", " ").Replace("  ", " ");

                    tidySentence = StaticAIMLUtils.ForInputTemplate(tidySentence);
                    if (tidySentence.Length > 0)
                    {
                        tidyString.Add(tidySentence);
                    }
                    if (tidySentence == "." || tidySentence == "?")
                    {
                        continue;
                    }
                }
                if (tidyString.Count > 0)
                {
                    return tidyString.ToArray();
                }
                else
                {
                    return new Unifiable[] { inputStringString };
                }
                inputStringString = inputStringString.TrimStart(".!? ,".ToCharArray());
                while (inputStringString.Length > 0)
                {
                    int index = inputStringString.IndexOfAny(".!?".ToCharArray());
                    if (index > 1)
                    {
                        string ss = inputStringString.Substring(0, index + 1);
                        tidyString.Add(ss);
                        inputStringString = inputStringString.Substring(index + 1);
                        continue;
                    }
                    tidyString.Add(inputStringString);
                }
                return tidyString.ToArray();
            }
            string[] rawResult = this.inputString.AsString().Split(tokens, System.StringSplitOptions.RemoveEmptyEntries);
            List<Unifiable> tidyResult = new List<Unifiable>();
            foreach (string rawSentence in rawResult)
            {
                string tidySentence = rawSentence.Trim();
                if (tidySentence.Length > 0)
                {
                    tidyResult.Add(tidySentence);
                }
            }
            return (Unifiable[])tidyResult.ToArray();
        }
    }
}
