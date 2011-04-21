using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Windows.Forms;
using System.Xml;
using AIMLbot;
using LAIR.ResourceAPIs.WordNet;
using MushDLR223.ScriptEngines;
using MushDLR223.Utilities;
using MushDLR223.Virtualization;
using org.opencyc.api;
using RTParser.AIMLTagHandlers;
using RTParser.Database;
using RTParser.GUI;
using RTParser.Prolog;
using RTParser.Utils;
using RTParser.Variables;
using RTParser.Web;
using Console=System.Console;
using UPath = RTParser.Unifiable;
using UList = System.Collections.Generic.List<RTParser.Utils.TemplateInfo>;

namespace RTParser
{
    /// <summary>
    /// </summary>
    public partial class RTPBot
    {
        internal readonly Dictionary<string, SystemExecHandler> ConsoleCommands = new Dictionary<string, SystemExecHandler>();
        int UseHttpd = -1;
        private static Bot ConsoleRobot;
        public static string AIMLDEBUGSETTINGS =
            "clear -spam +user +bina +error +aimltrace +cyc -dictlog -tscore +loaded";

        //    "clear +*";
        public static string[] RUNTIMESETTINGS = { "-GRAPH", "-USERTRACE" };

        public static string[] RUNTIMESETTINGS_RADEGAST = {
                                                              "CLEAR",
                                                              "+*",
                                                              "+STARTUP",
                                                              "+ERROR",
                                                              "+EXCEPTION",
                                                              "+GRAPH",
                                                              "+AIMLFILE",
                                                            //  "-AIMLLOADER",
                                                              "-DEBUG9",
                                                            //  "-ASSET"
                                                          };

        public static readonly TextFilter LoggedWords = new TextFilter(RUNTIMESETTINGS_RADEGAST)
                                                            {
                                                            }; //maybe should be ERROR", "STARTUP


        #region Logging methods

        /// <summary>
        /// The last message to be entered into the log (for testing purposes)
        /// </summary>
        public string LastLogMessage = String.Empty;

        public OutputDelegate outputDelegate;

        public void writeToLog(Exception e)
        {
            writeDebugLine(writeException(e));
        }

        static public String writeException(Exception e)
        {
            if (e == null) return "-write no exception-";
            string s = "ERROR: " + e.Message + " " + e.StackTrace;
            Exception inner = e.InnerException;
            if (inner != null && inner != e)
                s = s + writeException(inner);
            return s;
        }

        /// <summary>
        /// Writes a (timestamped) message to the Processor's log.
        /// 
        /// Log files have the form of yyyyMMdd.log.
        /// </summary>
        /// <param name="message">The message to log</param>
        //public OutputDelegate writeToLog;
        private static string lastMessage = null;
        public void writeToLog(string message, params object[] args)
        {
            message = SafeFormat(message, args);
            if (String.IsNullOrEmpty(message)) return;
            if (lastMessage == message)
            {
                return;
            }
            lastMessage = message;
            bool writeToConsole = true; // outputDelegate == null;

            //message = message.Trim() + Environment.NewLine;
            if (outputDelegate != null)
            {
                try
                {
                    outputDelegate(message);
                }
                catch (Exception)
                {
                    writeToConsole = true;
                }
            }
            if (outputDelegate != writeDebugLine)
            {
                if (writeToConsole) writeDebugLine(message);
            }
            message = string.Format("[{0}]: {1}", DateTime.Now, Trim(message));
            writeToFileLog(message);
        }

        public Object LoggingLock = new object();
        public IDisposable HttpTextServer;
        public static int NextHttp = 5580;
        public static int NextHttpIncrement = 100;
        private TimeSpan MaxWaitTryEnter = TimeSpan.FromSeconds(10);
        internal RTPBotCommands rtpbotcommands;
        private static AIMLPadEditor GUIForm;
        private static Thread GUIFormThread;


        public void writeToFileLog(string message)
        {
            LastLogMessage = message;
            if (!IsLogging) return;
            lock (LoggingLock)
            {
                //  this.LogBuffer.Add(DateTime.Now.ToString() + ": " + message + Environment.NewLine);
                LogBuffer.Add(message);
                if (LogBuffer.Count > MaxLogBufferSize - 1)
                {
                    // Write out to log file
                    HostSystem.CreateDirectory(PathToLogs);

                    Unifiable logFileName = DateTime.Now.ToString("yyyyMMdd") + ".log";
                    FileInfo logFile = new FileInfo(HostSystem.Combine(PathToLogs, logFileName));
                    StreamWriter writer;
                    if (!logFile.Exists)
                    {
                        writer = logFile.CreateText();
                    }
                    else
                    {
                        writer = logFile.AppendText();
                    }

                    foreach (string msg in LogBuffer)
                    {
                        writer.WriteLine(msg);
                    }
                    writer.Close();
                    LogBuffer.Clear();
                }
            }
            if (!Equals(null, WrittenToLog))
            {
                WrittenToLog();
            }
        }

        #endregion

        private static void MainConsoleWriteLn(string fmt, params object[] ps)
        {
            writeDebugLine("-" + fmt, ps);
        }

        public static void Main(string[] args)
        {
            //Application.Run(new RTParser.GUI.AIMLPadEditor("layout check",new RTPBot()));
            Run(args);
        }
        public static void Run(string[] args)
        {
            RTPBot myBot = null;
            TaskQueueHandler.TimeProcess("ROBOTCONSOLE: STARTUP", () => { myBot = Startup(args); });
            if (new List<string>(args).Contains("--gui"))
            {
                TaskQueueHandler.TimeProcess("ROBOTCONSOLE: RUN", () => RunGUI(args, myBot, MainConsoleWriteLn));
            } else
            {
                TaskQueueHandler.TimeProcess("ROBOTCONSOLE: RUN", () => Run(args, myBot, MainConsoleWriteLn));
            }
        }

        private static RTPBot Startup(string[] args)
        {
            RTPBot myBot;
            lock (typeof(Bot))
            {
                TaskQueueHandler.TimeProcess("ROBOTCONSOLE: PREPARE", () => Prepare(args));
                myBot = ConsoleRobot;
            }
            TaskQueueHandler.TimeProcess("ROBOTCONSOLE: LOAD", () => Load(args, myBot, MainConsoleWriteLn));
            TaskQueueHandler.TimeProcess("ROBOTCONSOLE: NOP", () => { });
            return myBot;
        }

        public static void Prepare(string[] args)
        {
            RTPBot myBot = new Bot();
            ConsoleRobot = myBot as Bot;
            OutputDelegate writeLine = MainConsoleWriteLn;
            for (int index = 0; index < args.Length; index++)
            {
                string s = args[index];
                if (s == "--httpd")
                {
                    UseBreakpointOnError = false;
                    if (index + 1 < args.Length)
                    {
                        int portNum;
                        if (int.TryParse(args[index + 1], out portNum))
                        {
                            myBot.UseHttpd = portNum;
                            if (portNum == NextHttp)
                            {
                                NextHttp += NextHttpIncrement;
                            }
                        }
                        else
                        {
                            myBot.UseHttpd = NextHttp;
                            NextHttp += NextHttpIncrement;
                        }
                    }
                }
            }
        }

        public void StartHttpServer()
        {
            string[] oArgs;
            if (UseHttpd > 0 && this.HttpTextServer == null)
            {
                ScriptExecutorGetter geter = new WebScriptExecutor(this);
                HttpTextServer = MushDLR223.Utilities.HttpServerUtil.CreateHttpServer(geter, UseHttpd, UserID);
            }
        }

        public static void Load(string[] args, RTPBot myBot, OutputDelegate writeLine)
        {
            myBot.outputDelegate = null; /// ?? Console.Out.WriteLine;

            // writeLine = MainConsoleWriteLn;
            bool gettingUsername = false;
            myBot.loadGlobalBotSettings();
            string myName = "BinaBot Daxeline";
            //myName = "Test Suite";
            //myName = "Kotoko Irata";
            //myName = "Nephrael Rae";
            if (args != null)
            {
                string newName = "";
                foreach (string s in args)
                {
                    if (s == "--breakpoints")
                    {
                        UseBreakpointOnError = true;
                        continue;
                    }
                    if (s == "--nobreakpoints")
                    {
                        UseBreakpointOnError = false;
                        continue;
                    }
                    if (s == "--aiml" || s == "--botname")
                    {
                        gettingUsername = true;
                        continue;
                    }
                    if (s.StartsWith("-"))
                    {
                        gettingUsername = false;
                        writeLine("passing option '" + s + "' to another program");
                        continue;
                    }
                    if (gettingUsername)
                    {
                        newName += " " + s;
                    }
                }
                newName = newName.Trim();
                if (newName.Length > 1)
                {
                    myName = newName;
                }
            }
            writeLine(Environment.NewLine);
            writeLine("Botname: " + myName);
            writeLine(Environment.NewLine);
            myBot.isAcceptingUserInput = false;
            writeLine("-----------------------------------------------------------------");
            myBot.SetName(myName);
            myBot.isAcceptingUserInput = true;
        }
        public static void RunGUI(string[] args, RTPBot myBot, OutputDelegate writeLine)
        {
            GUIForm = new GUI.AIMLPadEditor(myBot.NameAsSet, myBot);
            Application.Run(GUIForm);
        }

        public static void Run(string[] args, RTPBot myBot, OutputDelegate writeLine)
        {
            string evidenceCode = "<topic name=\"collectevidencepatterns\"> " +
                                  "<category><pattern>HOW ARE YOU</pattern><template>" +
                                  "<think><setevidence evidence=\"common-greeting\" prob=1.0 /></think>" +
                                  "</template></category></topic>" +
                                  "";
            //Added from AIML content now
            // myBot.AddAiml(evidenceCode);
            User myUser = myBot.LastUser;
            Request request = myUser.CreateRequest("current user toplevel", myBot.BotAsUser);
            myBot.LastRequest = request;
            myBot.BotDirective(myUser, request, "@help", writeLine);
            writeLine("-----------------------------------------------------------------");
            AIMLDEBUGSETTINGS = "clear +*";
            myBot.BotDirective(myUser, request, "@log " + AIMLDEBUGSETTINGS, writeLine);
            writeLine("-----------------------------------------------------------------");
            DLRConsole.SystemFlush();

            //string userJustSaid = String.Empty;
            myBot.LastUser = myUser;
            while (true)
            {
                myUser = myBot.LastUser;
                writeLine("-----------------------------------------------------------------");
                string input = TextFilter.ReadLineFromInput(writeLine, myUser.UserName + "> ");
                if (input == null)
                {
                    Environment.Exit(0);
                }
                input = Trim(input);
                if (input.ToLower() == "@quit")
                {
                    return;
                }
                if (input.ToLower() == "@exit")
                {
                    Environment.Exit(Environment.ExitCode);
                }
                myBot.AcceptInput(writeLine, input, myUser);
            }
        }

        public void AcceptInput(OutputDelegate writeLine, string input, User myUser)
        {
            RTPBot myBot = this;
            User BotAsAUser = myBot.BotAsUser;
            myUser = myUser ?? myBot.LastUser;
            string myName = BotAsAUser.UserName;
            {
                writeLine("-----------------------------------------------------------------");
                if (String.IsNullOrEmpty(input))
                {
                    writeLine(myName + "> " + BotAsAUser.JustSaid);
                    return;
                }
                try
                {
                    Unifiable cmdprefix = myUser.Predicates.grabSettingNoDebug("cmdprefix");
                    if (cmdprefix == null) cmdprefix = myBot.GlobalSettings.grabSettingNoDebug("cmdprefix");
                    if (!input.Contains("@") && !IsNullOrEmpty(cmdprefix))
                    {
                        input = cmdprefix.AsString() + " " + input;
                    }
                    if (input == "@")
                    {
                        input = myUser.JustSaid;
                    }

                    bool myBotBotDirective = false;
                    if (!input.StartsWith("@"))
                    {
                        //      string userJustSaid = input;
                        input = "@locally " + myUser.UserName + " - " + input;
                    }
                    User user = myUser;
                    TaskQueueHandler.TimeProcess(
                        "ROBOTCONSOLE: " + input,
                        () =>
                            {
                                myBotBotDirective = myBot.BotDirective(user, input, writeLine);
                            });
                    //if (!myBotBotDirective) continue;
                    writeLine("-----------------------------------------------------------------");
                    writeLine("{0}: {1}", myUser.UserName, myUser.JustSaid);
                    writeLine("---------------------");
                    writeLine("{0}: {1}", myName, BotAsAUser.JustSaid);
                    writeLine("-----------------------------------------------------------------");
                }
                catch (Exception e)
                {
                    writeLine("Error: {0}", e);
                }
            }
        }


        public object LightWeigthBotDirective(string input, Request request)
        {
            StringWriter sw = new StringWriter();
            OutputDelegate all = new OutputDelegate((s, args) =>
                                                        {
                                                            request.WriteLine(s, args);
                                                            sw.WriteLine(s, args);
                                                            writeDebugLine(s, args);
                                                        });
            var ss = input.Split(new string[] {"@"},StringSplitOptions.RemoveEmptyEntries);
            bool b = false;
            foreach (string s in ss)
            {
                if (BotDirective(request.Requester, request, "@" + s, all)) b = true;
            }
            string sws = sw.ToString();
            if (!b) return Unifiable.FAIL_NIL;
            return sws;
        }

        public bool BotDirective(User user, string input, OutputDelegate console)
        {
            try
            {
                User requester = (user ?? LastUser ?? BotAsUser);
                //Request request = requester.CreateRequest(input, BotAsUser);
                return BotDirective(requester, (Request) null, input, console);
            }
            catch (Exception e)
            {
                DLRConsole.DebugWriteLine("ERROR in BotDirective: " + e);
                return false;
            }
        }

        public bool BotDirective(User user, Request request, string input, OutputDelegate console)
        {
            try
            {
                _lastRequest = request;
                return BotDirective(user, input, console, null);
            }
            catch (Exception e)
            {
                DLRConsole.DebugWriteLine("ERROR in BotDirective: " + e);
                return false;
            }
        }
        public bool BotDirective(User user, string input, OutputDelegate console, ThreadControl control)
        {
            User targetBotUser = this.BotAsUser;
            if (input == null) return false;
            input = Trim(input);
            if (input == "") return false;
            if (input.StartsWith("@"))
            {
                input = input.TrimStart(new[] { ' ', '@' });
            }
            if (input == "gui")
            {
                if (GUIFormThread == null)
                {
                    GUIFormThread = new Thread(() => {
                        GUIForm = GUIForm ?? new GUI.AIMLPadEditor(NameAsSet, this);
                        Application.Run(GUIForm);
                    });
                    GUIFormThread.TrySetApartmentState(ApartmentState.STA);
                    GUIFormThread.Start();
                }
                if (GUIForm != null) GUIForm.Show();               
                return true;
            }
            User myUser = user ?? LastUser ?? FindOrCreateUser(UNKNOWN_PARTNER);
            int firstWhite = input.IndexOf(' ');
            if (firstWhite == -1) firstWhite = input.Length - 1;
            string cmd = Trim(ToLower(input.Substring(0, firstWhite + 1)));
            string args = Trim(input.Substring(firstWhite + 1));
            bool showHelp = false;
            if (cmd == "help")
            {
                showHelp = true;
                console("Commands are prefixed with @cmd");
                console("@help shows help -- command help comming soon!");
                console("@quit -- exits the aiml subsystem");
            }



            if (RTPBotCommands.ExecAnyAtAll(this, input, myUser, cmd, console, showHelp, args, targetBotUser, control)) return true;
            if (cmd == "query" || showHelp) if (rtpbotcommands.ExecQuery(this.LastRequest, cmd, console, showHelp, args, myUser))
                    return true;

            if (showHelp) console("@user [var [value]] -- lists or changes the current users get/set vars.");
            if (cmd == "user")
            {
                return myUser.DoUserCommand(args, console);
            }
            GraphMaster G = myUser.StartGraph;
            Request request = this.LastRequest;

            if (G.DoGraphCommand(cmd, console, showHelp, args, request)) return true;

            if (RTPBotCommands.ChGraphCmd(request, showHelp, console, cmd, myUser, args)) return true;

            if (DoLogCmd(console, showHelp, cmd, args)) return true;

            if (RTPBotCommands.CallOrExecCmd(request, showHelp, console, cmd, myUser, args)) return true;

            bool uc = BotUserDirective(myUser, input, console);
            if (uc)
            {
                return true;
            }
            if (showHelp)
            {
                string help = "Exec handlers: ";
                lock (ExecuteHandlers)
                {
                    foreach (KeyValuePair<string, SystemExecHandler> systemExecHandler in ExecuteHandlers)
                    {
                        help += " @" + systemExecHandler.Key;
                    }
                }
                console(help);
                return true;
            }
            SystemExecHandler handler;
            if (SettingsDictionary.TryGetValue(ExecuteHandlers, cmd, out handler))
            {
                object result = handler(args, request);
                console("" + result);
                return true;
            }

            if (RTPBotCommands.TaskCommand(request, console, cmd, args)) return true;
            console("unknown: @" + input);
            return false;
        }

    }

    public partial class RTPBotCommands //: RTPBot
    {
        private static bool SplitOff(string args, string s, out string user, out string said)
        {
            return TextPatternUtils.SplitOff(args, s, out user, out said);
        }

        public static RTPBot robotIn;

        public RTPBotCommands(RTPBot bot)
        {
            robotIn = bot;
        }

        internal static bool ExecAnyAtAll(RTPBot robot, string input, User myUser, string cmd, OutputDelegate console, bool showHelp, string args, User targetBotUser, ThreadControl control)
        {
            
            if (showHelp)
                console(
                    "@withuser <user> - <text>  -- (aka. simply @)  runs text/command intentionally setting LastUser");
            if (cmd == "withuser" || cmd == "@")
            {
                string said;
                string user;
                if (!SplitOff(args, "-", out user, out said))
                {
                    user = myUser.UserName;
                    said = args;
                }
                User wasUser = robot.FindUser(user);
                Result res = robot.GlobalChatWithUser(said, user, null, RTPBot.writeDebugLine, true, false);
                // detect a user "rename"
                robot.DetectUserChange(myUser, wasUser, user);
                robot.OutputResult(res, console, false);
                return true;
            }

            if (showHelp)
                console(
                    "@locally <user> - <text>  -- runs text/command not intentionally not setting LastUser");
            if (cmd == "locally" || cmd == "@")
            {
                string said;
                string user;
                if (!SplitOff(args, "-", out user, out said))
                {
                    user = myUser.UserName;
                    said = args;
                }
                User wasUser = robot.FindUser(user);
                Result res = robot.GlobalChatWithUser(said, user, null, RTPBot.writeDebugLine, true, true);
                Request request = res.request;
                request.ResponderSelfListens = false;
                // detect a user "rename"
                bool userChanged = robot.DetectUserChange(myUser, wasUser, user);
                User theResponder = (res.Responder ?? res.request.Responder).Value;
                if (userChanged)
                {
                    //myUser = FindUser(user);
                    request.SetSpeakerAndResponder(myUser, theResponder);
                }
                var justsaid = robot.OutputResult(res, console, false);
                if (theResponder == null)
                {
                    theResponder = (myUser == targetBotUser) ? request.Requester : targetBotUser;
                    robot.writeToLog("Making the responder " + theResponder);
                }
                if (theResponder == null)
                {
                    return true;
                }
                myUser.LastResponder = theResponder;
                theResponder.JustSaid = justsaid;
                // ReSharper disable ConditionIsAlwaysTrueOrFalse
                if (robot.ProcessHeardPreds && request.ResponderSelfListens)
                    // ReSharper restore ConditionIsAlwaysTrueOrFalse
                    robot.HeardSelfSayResponse(theResponder, myUser, justsaid, res, control);

                return true;
            }
            if (showHelp)
                console(
                    "@aimladd [graphname] <aiml/> -- inserts aiml content into graph (default LastUser.ListeningGraph )");
            if (cmd == "aimladd" || cmd == "+")
            {
                int indexof = args.IndexOf("<");
                if (indexof < 0)
                {
                    console(
                        "@aimladd [graphname] <aiml/> -- inserts aiml content into graph (default LastUser.ListeningGraph )");
                    return true;
                }
                string gn = args.Substring(0, indexof);
                GraphMaster g = robot.GetGraph(gn, myUser.StartGraph);
                String aiml = RTPBot.Trim(args.Substring(indexof));
                robot.AddAiml(g, aiml, robot.LastRequest);
                console("Done with " + args);
                return true;
            }

            if (showHelp) console("@prolog <load.pl>");
            if (cmd == "prolog")
            {
                CSPrologMain.Main(args.Split(" \r\n\t".ToCharArray(), StringSplitOptions.RemoveEmptyEntries));
                return true;
            }

            if (showHelp) console("@pl text to say");
            if (cmd == "pl")
            {
                string callme = "alicebot2(['" + string.Join("','", args.ToUpper()
                                                                        .Split(" \r\n\t".ToCharArray(),
                                                                               StringSplitOptions.RemoveEmptyEntries)) +
                                "'],Out),writeq('----------------------------------------------'),writeq(Out),nl,halt.";
                CSPrologMain.Main(new[] {callme});
                return true;
            }

            if (showHelp) console("@reload -- reloads any changed files ");
            if (cmd == "reload")
            {
                robot.ReloadAll();
                return true;
                //                return;//Success("WorldSystemModule.MyBot.ReloadAll();");
            }

            if (cmd == "echo")
            {
                console(args);
                return true;
            }
            if (showHelp) console("@load <graph> - <uri>");
            if (cmd == "load")
            {
                Request request = robot.LastRequest;
                string graphname;
                string files;
                if (!SplitOff(args, "-", out graphname, out files))
                {
                    graphname = "current";
                    files = args;
                }
                GraphMaster G = robot.GetGraph(graphname, myUser.StartGraph);
                AIMLLoader loader = robot.GetLoader(request);
                LoaderOptions reqLoadOptionsValue = request.LoadOptions.Value;
                var prev = request.Graph;
                try
                {
                    request.Graph = G;
                    reqLoadOptionsValue.CtxGraph = G;
                    loader.loadAIMLURI(files, reqLoadOptionsValue);
                    // maybe request.TargetBot.ReloadHooks.Add(() => request.Loader.loadAIMLURI(args, reqLoadOptionsValue));
                    console("Done with " + files);
                }
                finally
                {
                    request.Graph = prev;
                }
                return true;
            }
            if (showHelp) console("@say [who -] <text> -- fakes that the 'who' (default bot) just said it");
            if (cmd == "say")
            {
                console("say> " + args);
                string who, said;
                if (!SplitOff(args, "-", out who, out said))
                {
                    who = targetBotUser.UserID;
                    said = args;
                }
                User factSpeaker = robot.FindOrCreateUser(who);
                robot.HeardSelfSayVerbal(factSpeaker, factSpeaker.LastResponder.Value, args, robot.LastResult, control);
                return true;
            }

            if (showHelp) console("@say1 [who -] <sentence> -- fakes that 'who' (default bot) just said it");
            if (cmd == "say1")
            {
                console("say1> " + args);
                string who, said;
                if (!SplitOff(args, "-", out who, out said))
                {
                    who = targetBotUser.UserID;
                    said = args;
                }
                User factSpeaker = robot.FindOrCreateUser(who);
                robot.HeardSomeoneSay1Sentence(factSpeaker, factSpeaker.LastResponder.Value, said, robot.LastResult,
                                            control);
                return true;
            }

            if (showHelp)
                console(
                    "@set [type] [name [value]] -- emulates get/set tag in AIML.  'type' defaults to =\"user\" therefore same as @user");
            if (cmd == "set")
            {
                console(robot.DefaultPredicates.ToDebugString());
                return myUser.DoUserCommand(args, console);
                return true;
            }
            //Request request = robot.LastRequest;
            if (ExecCmdSetVar(robot, showHelp, input, console, cmd, args, targetBotUser)) return true;
            if (ExecCmdBot(robot, showHelp, console, cmd, args, targetBotUser)) return true;

            if (showHelp || cmd == "proof" || cmd == "botproof")
            {
                lock (myUser.TemplatesLock)
                {
                if (RTPBotCommands.ExecProof(robot, cmd, console, showHelp, args, myUser))
                    return true;
            }}
            return false;
        }

        [HelpText("@setvar dictname.name [value] -- get/sets a variable using a global namespace context")]
        [CommandText("setvar")]
        private static bool ExecCmdSetVar(RTPBot robot, bool showHelp, string input, OutputDelegate console, string cmd, string args, User myUser)
        {
         //   RTPBot robot = request.TargetBot;
            if (showHelp)
                console(
                    "@setvar dictname.name [value] -- get/sets a variable using a global namespace context");
            if (cmd == "setvar")
            {
                myUser.DoUserCommand(args, console);
                robot.GlobalSettings.DoSettingsCommand(input, console); ;
                return myUser.DoUserCommand(args, console);
            }
            return false;
        }

        [HelpText("@bot [var [value]] -- lists or changes the bot GlobalPredicates.\n  example: @bot ProcessHeardPreds True or @bot ProcessHeardPreds False")]
        [CommandText("bot")]
        private static bool ExecCmdBot(RTPBot robot, bool showHelp, OutputDelegate console, string cmd, string args, User targetBotUser)
        {
            if (showHelp) console("@bot [var [value]] -- lists or changes the bot GlobalPredicates.\n  example: @bot ProcessHeardPreds True or @bot ProcessHeardPreds False");
            if (cmd == "bot")
            {
                console(robot.HeardPredicates.ToDebugString());
                console(robot.RelationMetaProps.ToDebugString());
                return targetBotUser.DoUserCommand(args, console);
            }
            return false;
        }


        [HelpText("@[bot]proof [clear|enable|reset|disable|[save [filename.aiml]]] - clears or prints a content buffer being used")]
        [CommandText("proof", "prf", "botproof")]
        static internal bool ExecProof(RTPBot robot, string cmd, OutputDelegate console, bool showHelp, string args, User myUser)
        {

            if (showHelp)
                console("@[bot]proof [clear|enable|reset|disable|[save [filename.aiml]]] - clears or prints a content buffer being used");
            if (cmd == "botproof")
            {
                myUser = robot.BotAsUser;
                cmd = "proof";
            }
            if (cmd == "proof")
            {
                PrintOptions printOptions = PrintOptions.CONSOLE_LISTING;
                Request request = robot.LastRequest;
                if (request != null) printOptions = request.WriterOptions;
                printOptions.ClearHistory();
                console("-----------------------------------------------------------------");
                Request ur = robot.MakeRequestToBot(args, myUser);
                int i;
                Result r = myUser.LastResult;
                if (args.StartsWith("save"))
                {
                    args = StaticXMLUtils.Trim(args.Substring(4));
                    string hide = StaticAIMLUtils.GetTemplateSource(myUser.VisitedTemplates, printOptions);
                    console(hide);
                    if (args.Length > 0) HostSystem.AppendAllText(args, hide + "\n");
                    return true;
                }
                if (int.TryParse(args, out i))
                {
                    r = myUser.GetResult(i);
                    console("-----------------------------------------------------------------");
                    if (r != null)
                        RTPBot.PrintResult(r, console, printOptions);
                }
                else
                {
                    if (args.StartsWith("disable"))
                    {
                        lock (myUser.TemplatesLock)
                        {
                            DisableTemplates(myUser, myUser.ProofTemplates);
                            if (args.EndsWith("all"))
                            {
                                DisableTemplates(myUser, myUser.VisitedTemplates);
                                DisableTemplates(myUser, myUser.UsedChildTemplates);
                            }
                        }
                    }
                    if (args == "enable" || args == "reset")
                    {
                        foreach (TemplateInfo C in myUser.DisabledTemplates)
                        {
                            C.IsDisabled = false;
                            myUser.VisitedTemplates.Add(C);
                        }
                        myUser.DisabledTemplates.Clear();
                    }
                    TimeSpan sleepBetween = TimeSpan.FromMilliseconds(500);
                    console("-----------------------------------------------------------------");
                    console("-------DISABLED--------------------------------------");
                    RTPBot.PrintTemplates(myUser.DisabledTemplates, console, printOptions, sleepBetween);
                    console("-----------------------------------------------------------------");
                    console("-------PROOF--------------------------------------");
                    RTPBot.PrintTemplates(myUser.ProofTemplates, console, printOptions, sleepBetween);
                    console("-----------------------------------------------------------------");
                    console("-------CHILD--------------------------------------");
                    RTPBot.PrintTemplates(myUser.UsedChildTemplates, console, printOptions, sleepBetween);
                    console("-----------------------------------------------------------------");
                    console("-------USED--------------------------------------");
                    RTPBot.PrintTemplates(myUser.VisitedTemplates, console, printOptions, sleepBetween);
                    console("-----------------------------------------------------------------");

                    if (args == "clear" || args == "reset")
                    {
                        // dont revive disabled templates on "clear" // myUser.DisabledTemplates.Clear();
                        myUser.ProofTemplates.Clear();
                        myUser.UsedChildTemplates.Clear();
                        myUser.VisitedTemplates.Clear();
                        console("--------------------ALL CLEARED------------------------------------------");
                    }
                    // clear history so next call to @proof shows something
                    printOptions.ClearHistory();
                }

                return true;
            }
            return false;
        }

        private static void DisableTemplates(User myUser, ICollection<TemplateInfo> templateInfos)
        {
            foreach (TemplateInfo C in GraphMaster.CopyOf(templateInfos))
            {
                C.IsDisabled = true;
                myUser.DisabledTemplates.Add(C);
            }
            templateInfos.Clear();
        }

        internal bool ExecQuery(Request request, string cmd, OutputDelegate console, bool showHelp, string args, User myUser)
        {
            if (showHelp) console("@query <text> - conducts a findall using all tags");
            if (cmd == "query")
            {
                RTPBot robot = request.TargetBot;
                PrintOptions printOptions = request.WriterOptions ?? PrintOptions.CONSOLE_LISTING;
                console("-----------------------------------------------------------------");
                if (args == "")
                {
                    QuerySettings ur0 = myUser.GetQuerySettings();
                    if (ur0.MinOutputs != QuerySettings.UNLIMITED)
                    {
                        console("- query mode on -");
                        QuerySettings.ApplySettings(QuerySettings.FindAll, ur0);
                    }
                    else
                    {
                        console("- query mode off -");
                        QuerySettings.ApplySettings(QuerySettings.CogbotDefaults, ur0);
                    }
                    return true;
                }

                Request ur = robot.MakeRequestToBot(args, myUser);

                // Adds findall to request
                QuerySettings.ApplySettings(QuerySettings.FindAll, ur);

                ur.IsTraced = myUser.IsTraced;
                console("-----------------------------------------------------------------");
                var result = robot.ChatWithToplevelResults(ur, request.CurrentResult);//, myUser, targetBotUser, myUser.ListeningGraph);
                console("-----------------------------------------------------------------");
                RTPBot.PrintResult(result, console, printOptions);
                console("-----------------------------------------------------------------");
                return true;
            }
            return false;
        }

        internal static bool ChGraphCmd(Request request, bool showHelp, OutputDelegate console, string cmd, User myUser, string args)
        {
            
            if (showHelp) console("@chgraph <graph> - changes the users graph");
            if (cmd == "graph" || cmd == "chgraph" || cmd == "cd")
            {
                RTPBot robot = request.TargetBot;
                PrintOptions printOptions = request.WriterOptions ?? PrintOptions.CONSOLE_LISTING;
                GraphMaster current = myUser.StartGraph;
                GraphMaster graph = robot.FindGraph(args, current);
                if (args != "" && graph != null && graph != current)
                {
                    console("Changing to graph " + graph);
                    myUser.StartGraph = graph;
                    console("-----------------------------------------------------------------");
                    return true;
                }
                if (args == "~")
                {
                    graph = robot.GetUserGraph(myUser.UserID);
                    console("Changing to user graph " + graph);
                    myUser.StartGraph = graph;
                    console("-----------------------------------------------------------------");
                    return true;
                }
                if (args == "")
                {
                    console("-----------------------------------------------------------------");
                    foreach (var ggg in new ListAsSet<GraphMaster>(GraphMaster.CopyOf(robot.LocalGraphsByName).Values))
                    {
                        console("-----------------------------------------------------------------");
                        console("local=" + ggg + " keys='" + AsString(ggg.GraphNames) + "'");
                        ggg.WriteMetaHeaders(console, printOptions);
                        console("-----------------------------------------------------------------");
                    }
                    console("-----------------------------------------------------------------");
                    foreach (var ggg in new ListAsSet<GraphMaster>(GraphMaster.CopyOf(RTPBot.GraphsByName).Values))
                    {
                        console("-----------------------------------------------------------------");
                        console("global=" + ggg + " keys='" + AsString(ggg.GraphNames) + "'");
                        ggg.WriteMetaHeaders(console, printOptions);
                        console("-----------------------------------------------------------------");
                    }
                }
                console("-----------------------------------------------------------------");
                console("StartGraph=" + current);
                console("HearSelfSayGraph=" + myUser.HeardSelfSayGraph);
                console("HeardYouSayGraph=" + myUser.HeardYouSayGraph);
                console("-----------------------------------------------------------------");
                return true;
            }
            return false;
        }

        private static string AsString(IEnumerable names)
        {
            string s = "";
            bool comma = false;
            foreach (var name in names)
            {
                if (comma)
                {
                    s += ",";
                }
                else
                {
                    comma = true;
                }
                s += name;
            }
            return s;
        }

        internal static bool CallOrExecCmd(Request request, bool showHelp, OutputDelegate console, string cmd, User myUser, string args)
        {
            if (showHelp)
                console("@eval <source>  --- runs source based on users language setting interp='" +
                        myUser.Predicates.grabSetting("interp") + "'");
            if (cmd == "eval")
            {
                cmd = "call";
                args = "@" + myUser.Predicates.grabSettingOrDefault("interp", "cloj") + " " + args;
            }

            RTPBot robot = request.TargetBot;
            PrintOptions printOptions = request.WriterOptions ?? PrintOptions.CONSOLE_LISTING;

            SystemExecHandler seh;
            if (robot.ConsoleCommands.TryGetValue(cmd.ToLower(), out seh))
            {
                robot.writeToLog("@" + cmd + " = " + seh(args, myUser.CurrentRequest));
            }

            if (showHelp) console("@call <lang> <source>  --- runs script source");
            if (cmd == "call")
            {
                string source; // myUser ?? LastUser.ShortName ?? "";
                string slang;
                if (args.StartsWith("@"))
                {
                    args = args.Substring(1);
                    int lastIndex = args.IndexOf(" ");
                    if (lastIndex > 0)
                    {
                        source = RTPBot.Trim(args.Substring(lastIndex + 1));
                        slang = RTPBot.Trim(args.Substring(0, lastIndex));
                    }
                    else
                    {
                        source = args;
                        slang = null;
                    }
                }
                else
                {
                    source = args;
                    slang = null;
                }
                Request ur = robot.MakeRequestToBot(args, myUser);
                if (source != null)
                {
                    try
                    {
                        console(robot.SystemExecute(source, slang, ur));
                    }
                    catch (Exception e)
                    {
                        console("SystemExecute " + source + " " + slang + " caused " + e);
                    }
                }
                return true;
            }
            return false;
        }

        [HelpText("@tasks/threads/kill - control tasks")]
        [CommandText("tasks", "thread", "threads", "kill")]
        internal static bool TaskCommand(Request request, OutputDelegate console, string cmd, string args)
        {
            RTPBot robot = request.TargetBot;
            if (cmd == "tasks")
            {
                int n = 0;
                IList<Thread> botCommandThreads = robot.ThreadList;
                List<string> list = new List<string>();
                if (false) lock (botCommandThreads)
                    {
                        int num = botCommandThreads.Count;
                        foreach (Thread t in botCommandThreads)
                        {
                            n++;
                            num--;
                            //System.Threading.ThreadStateException: Thread is dead; state cannot be accessed.
                            //  at System.Threading.Thread.IsBackgroundNative()
                            if (!t.IsAlive)
                            {
                                list.Add(string.Format("{0}: {1} IsAlive={2}", num, t.Name, t.IsAlive));
                            }
                            else
                            {
                                list.Insert(0, string.Format("{0}: {1} IsAlive={2}", num, t.Name, t.IsAlive));
                            }
                        }
                    }
                int found = 0;
                lock (TaskQueueHandler.TaskQueueHandlers)
                {
                    foreach (var queueHandler in TaskQueueHandler.TaskQueueHandlers)
                    {
                        found++;
                        if (queueHandler.Busy)
                            list.Insert(0, queueHandler.ToDebugString(true));
                        else
                        {
                            list.Add(queueHandler.ToDebugString(true));
                        }

                    }
                }
                foreach (var s in list)
                {
                    console(s);
                }
                console("TaskQueueHandlers: " + found + ", threads: " + n);
                return true;
            }
            if (cmd.StartsWith("thread"))
            {
                if (args == "list" || args == "")
                {
                    int n = 0;
                    var botCommandThreads = robot.ThreadList;
                    List<string> list = new List<string>();
                    lock (botCommandThreads)
                    {
                        int num = botCommandThreads.Count;
                        foreach (Thread t in botCommandThreads)
                        {
                            n++;
                            num--;
                            //System.Threading.ThreadStateException: Thread is dead; state cannot be accessed.
                            //  at System.Threading.Thread.IsBackgroundNative()
                            if (!t.IsAlive)
                            {
                                list.Add(string.Format("{0}: {1} IsAlive={2}", num, t.Name, t.IsAlive));
                            }
                            else
                            {
                                list.Insert(0, string.Format("{0}: {1} IsAlive={2}", num, t.Name, t.IsAlive));
                            }
                        }
                    }
                    foreach (var s in list)
                    {
                        console(s);
                    }
                    console("Total threads: " + n);
                    return true;
                }
                else
                {
                    cmd = args;
                    ThreadStart thread = () =>
                                             {
                                                 try
                                                 {
                                                     try
                                                     {

                                                         robot.BotDirective(request.Requester, request, args, console);
                                                     }
                                                     catch (Exception e)
                                                     {
                                                         console("Problem with " + args + " " + e);
                                                     }
                                                 }
                                                 finally
                                                 {
                                                     try
                                                     {
                                                         robot.HeardSelfSayQueue.RemoveThread(Thread.CurrentThread);
                                                     }
                                                     catch (OutOfMemoryException) { }
                                                     catch (StackOverflowException) { }
                                                     catch (Exception) { }
                                                     console("done with " + cmd);
                                                 }
                                             };
                    String threadName = "ThreadCommnand for " + cmd;
                    robot.HeardSelfSayQueue.MakeSyncronousTask(thread, threadName, TimeSpan.FromSeconds(20));
                    console(threadName);
                    return true;
                }
                if (args.StartsWith("kill"))
                {
                    int n = 0;
                    var botCommandThreads = robot.ThreadList;
                    lock (botCommandThreads)
                    {
                        int num = botCommandThreads.Count;
                        foreach (Thread t in botCommandThreads)
                        {
                            n++;
                            num--;
                            //System.Threading.ThreadStateException: Thread is dead; state cannot be accessed.
                            //  at System.Threading.Thread.IsBackgroundNative()
                            if (!t.IsAlive)
                            {
                                console("Removing {0}: {1} IsAlive={2}", num, t.Name, t.IsAlive);
                            }
                            else
                            {
                                console("Killing/Removing {0}: {1} IsAlive={2}", num, t.Name, t.IsAlive);
                                try
                                {
                                    if (t.IsAlive)
                                    {
                                        //  aborted++;
                                        t.Abort();
                                    }
                                    t.Join();
                                }
                                catch (Exception) { }
                            }
                            robot.HeardSelfSayQueue.RemoveThread(t);
                        }
                    }
                }
                return true;
            }
            return false;
        }
    }

    internal class CommandTextAttribute : Attribute
    {
        public string[] Value;
        public CommandTextAttribute(params string[] cmdnames)
        {
            Value = cmdnames;
        }
    }

    internal class HelpTextAttribute : Attribute
    {
        public string Value;
        public HelpTextAttribute(string text)
        {
            Value = text;
        }
    }

    public partial class RTPBot
    {

        private void AddBotCommand(string s, Action action)
        {
            if (action != null)
                ConsoleCommands.Add(s, delegate(string cmd, Request user)
                                           {
                                               action();
                                               return cmd;
                                           });
        }

        private static void UnseenWriteline(string real)
        {
            string message = real.ToUpper();
            if ((message.Contains("ERROR") && !message.Contains("TIMEOUTMESSAGE")) || message.Contains("EXCEPTION"))
            {
                DLRConsole.SYSTEM_ERR_WRITELINE("HIDDEN ERRORS: {0}", real);
                return;
            }
            DLRConsole.SYSTEM_ERR_WRITELINE("SPAMMY: {0}", real);
        }

        public void TraceTest(String s, Action action)
        {
           // return;
            writeChatTrace(s);
            return;
            action();
        }

        public static Exception RaiseErrorStatic(InvalidOperationException invalidOperationException)
        {
            writeDebugLine(writeException(invalidOperationException));
            return invalidOperationException;
        }

        public Exception RaiseError(Exception invalidOperationException)
        {
            writeDebugLine(writeException(invalidOperationException));
            return invalidOperationException;
        }
    }
}