﻿/*
 * This is the main for the actual headless cogbot processor.
 * Usually it's run by the Radegast cogbot plugin
 * 
 *      "you're going to find lots of things in the code that make little sense"
 *                                             - dmiles, 5/18/2012
 *                                             
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Cogbot;
using CommandLine;
using MushDLR223.ScriptEngines;
using MushDLR223.Utilities;
using OpenMetaverse;
using Radegast;
using CommandLine.Text;
#if USE_SAFETHREADS
using Thread = MushDLR223.Utilities.SafeThread;
#endif

namespace ABuildStartup
{

    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            string[] use = Environment.GetCommandLineArgs() ?? new string[0];
            ClientManagerConfig.StartLispThreadAtPluginInit = true;
            if (use.Length > 0)
            {
                string arg0 = use[0].ToLower();
                if (arg0.EndsWith(".vshost.exe"))
                {
                    ClientManagerConfig.IsVisualStudio = true;
                    arg0 = arg0.Replace(".vshost.exe", ".exe");
                }
                if (arg0.EndsWith(Application.ExecutablePath.ToLower()))
                {
                    var n = new List<string>();
                    foreach (var s in use)
                    {
                        n.Add(s);
                    }
                    n.RemoveAt(0);
                    use = n.ToArray();
                }
            }
            Main0(use);
        }

        static public Exception _appException = null;
        private static void DoAndExit(ThreadStart o)
        {
            try
            {
                ExitCode = 0;
                o();
                /*
                Thread t = new Thread(o, 0);
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
                t.Join();
                */
                _appException = null;
            }
            catch (Exception e)
            {
                if (_appException == null) _appException = e;
            }
            finally
            {
                FilteredWriteLine("ExitCode: " + ExitCode + " for " + Environment.CommandLine);
            }
            if (_appException != null)
            {
                FilteredWriteLine("" + _appException);
                if (ExitCode == 0) ExitCode = 1;
            }
            if (ExitCode != 0)
            {
                FilteredWriteLine("ExitCode: " + ExitCode + " for " + Environment.CommandLine);
            }

            if (UseApplicationExit)
            {
                Application.Exit();
                Environment.Exit(ExitCode);
            }
            return;
        }

        static public int ExitCode
        {
            get
            {
                return Environment.ExitCode;
            }

            set
            {
                Environment.ExitCode = value;
            }
        }

        public static TextFilter filter = DLRConsole.TheGlobalLogFilter;// //new TextFilter() { "+*" };
        static public void FilteredWriteLine(string str, params object[] args)
        {

            OutputDelegate del = new OutputDelegate(ClientManager.Real ?? DLRConsole.DebugWriteLine);
            if (ClientManager.Filter == null)
            {
                ClientManager.Filter = new OutputDelegate(del);
            }
            filter.writeDebugLine(del, str, args);
        }

        public class ABuildStartupCommandLine: Radegast.CommandLine 
        {
#if false
            [Option("u", "username", HelpText = "Username, use quotes to supply \"First Last\"")]
            public string Username = string.Empty;

            [Option("p", "password", HelpText = "Account password")]
            public string Password = string.Empty;

            [Option("a", "autologin", HelpText = "Automatially login with provided user credentials")]
            public bool AutoLogin = false;

            [Option("g", "grid", HelpText = "Grid ID to login into, try --list-grids to see IDs used for this parameter")]
            public string Grid = string.Empty;

            [Option("l", "location", HelpText = "Login location: last, home or regionname. Regioname can also be in format regionname/x/y/z")]
            public string Location = string.Empty;

            [Option(null, "list-grids", HelpText = "Lists grid IDs used for --grid option")]
            public bool ListGrids = false;

            [Option(null, "loginuri", HelpText = "Use this URI to login (don't use with --grid)")]
            public string LoginUri = string.Empty;

            [Option(null, "no-sound", HelpText = "Disable sound")]
            public bool DisableSound = false;
#endif
            [Option(null, "aiml", HelpText = "Runs just the AIML interpretor")]
            public bool AIMLOnly = false;

            [Option(null, "swipl", HelpText = "Runs just the SWIPL interpretor", Required=false)]
            public bool SWIPLOnly = false;

            [Option(null, "httpd", HelpText = "Start bot HTTPD on Port")]
            public string Port = null;

            [Option(null, "nogui", HelpText = "Headless daemon operation")]
            public string NoGUI = null;

            [Option(null, "lcd", HelpText = "locally change the dirrectory")]
            public string LocalChanfgeDir = "unset";

            public HelpText GetHeader()
            {
                HelpText header = new HelpText(Assembly.GetExecutingAssembly().CodeBase);
                header.AdditionalNewLineAfterOption = true;
                header.Copyright = new CopyrightInfo("Radegast Development Team", 2009, 2010);
                header.AddPreOptionsLine("http://radegastclient.org/");
                return header;
            }

            [HelpOption("h", "help", HelpText = "Display this help screen.")]
            public string GetUsage()
            {
                HelpText usage = GetHeader();
                usage.AddOptions(this);
                usage.AddPostOptionsLine("Example: automatically login user called Some Resident to his last location on the Second Life main grid (agni)");
                usage.AddPostOptionsLine("program.exe -a -g agni -u \"Some Resident\" -p \"secret\"  -l last");
                return usage.ToString();
            }
        }

        public static ABuildStartupCommandLine ABuildStartCommandLine;
        /// <summary>
        /// runs in a STA thread (by creating one)  Does not "join"
        /// </summary>
        /// <param name="args"></param>        
        public static void Main0(string[] args)
        {
            if (true || Thread.CurrentThread.ApartmentState == ApartmentState.STA)
            {
                Thread newThread = new Thread(new ThreadStart(() => Main1(args)));
                newThread.SetApartmentState(ApartmentState.STA);
                newThread.Start();
                //newThread.Join();
                return;
            }
        }


        /// <summary>
        /// runs in current thread
        /// </summary>
        /// <param name="args"></param>
        [STAThread]
        public static void Main1(string[] args)
        {
            var DLRConsoleError = DLRConsole.Error;
            DLRConsole.DetectMainEnv(DLRConsoleError);
            if (ABuildStartCommandLine == null)
            {
                ABuildStartCommandLine = new ABuildStartupCommandLine();
            }
            CommandLineParser parser = new CommandLineParser(new CommandLineParserSettings(DLRConsoleError));
            if (!parser.ParseArguments(args, ABuildStartCommandLine, null))
            {
                DLRConsoleError.WriteLine("Usage: " + ABuildStartCommandLine.GetUsage());
            }
            else
            {
              //  DLRConsoleError.WriteLine("Used: " + ABuildStartCommandLine.GetUsage());
            }

            args = args ?? new string[0];
            //if (args.Length == 0) args = new string[] { /*"--httpd", "--aiml",*/ "Nephrael", "Rae" };
            //if (args.Length == 0) args = new string[] { "--httpd", "--aiml", "Zeno", "Aironaut" };
            //if (args.Length == 0) args = new string[] { "--httpd", "--aiml", "test", "suite" }; 
            //if (args.Length == 0) args = new string[] { "--httpd", "--aiml", "BinaBot", "Daxeline" };            
            //if (args.Length == 0) args = new string[] { "--swipl" };                  

            if (ClientManager.MainThread == null)
            {
                ClientManager.MainThread = Thread.CurrentThread;
            }

            ClientManagerConfig.arguments = new Parser(args);
            string[] oArgs;
            // Change current working directory to Program install dir?
            if (ClientManagerConfig.arguments.GetAfter("--lcd", out oArgs))
            {
                Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            }
            if (!ClientManagerConfig.arguments.GetAfter("--noexcpt", out oArgs))
            {
                SetExceptionHandlers(true);
            }
            if (ClientManagerConfig.arguments.GetAfter("--aiml", out oArgs))
            {
                string[] newArgs = oArgs;
                AllocConsole();
                RunType("AIMLbot:RTParser.RTPBot", args);
                return;
            }
            if (ClientManagerConfig.arguments.GetAfter("--plwin", out oArgs))
            {
                DoAndExit(() =>
                              {
                                  var firstProc = new System.Diagnostics.Process();
                                  firstProc.StartInfo.FileName = @"c:\Program Files\pl\bin\swipl-win.exe";
                                  firstProc.StartInfo.Arguments = "-f prolog/cogbot.pl";// "-f StartCogbot.pl";

                                  firstProc.EnableRaisingEvents = true;
                                  AllocConsole();
                                  firstProc.Start();
                                  firstProc.WaitForExit();
                              });                
                return;
            }     
            if (ClientManagerConfig.arguments.GetAfter("--swipl", out oArgs))
            {
                string[] newArgs = oArgs;
                if (newArgs.Length == 0) newArgs = new string[] { "-f", "cynd/cogbot.pl" };
                RunType("PrologBotModule:PrologScriptEngine.PrologScriptInterpreter", newArgs);
                return;
            }
            if (ClientManagerConfig.arguments.GetAfter("--main", out oArgs))
            {
                string[] newArgs = oArgs;
                AllocConsole();
                string c = ClientManagerConfig.arguments["--main"];
                RunType(c, newArgs);
                return;
            }
            if (ClientManagerConfig.arguments.GetWithout("--noconfig", out oArgs))
            {
                ClientManagerConfig.arguments = new Parser(oArgs);
                ClientManagerConfig.NoLoadConfig = true;
            }
            if (ClientManagerConfig.arguments.GetWithout("--console", out oArgs))
            {
                ClientManager.dosBox = true;
                ClientManagerConfig.arguments = new Parser(oArgs);
            }
            if (ClientManagerConfig.arguments.GetWithout("--nogui", out oArgs))
            {
                ClientManagerConfig.noGUI = true;
            } else
            {
                try
                {
                    // probe for X windows
                    var f = System.Windows.Forms.SystemInformation.MenuAccessKeysUnderlined;
                }
                catch (Exception)
                {
                    // X windows missing
                    ClientManagerConfig.noGUI = true;
                }
            }
            if (ClientManager.dosBox) AllocConsole();

            DoAndExit(() =>
                          {
                              MainProgram.CommandLine = new Radegast.CommandLine();
                              ClientManagerConfig.arguments = new Parser(args);
                              if (ClientManagerConfig.noGUI)
                              {
                                  ClientManagerConfig.UsingRadgastFromCogbot = true;
                                  Cogbot.Program.MainRun(args);
                              }
                              else
                              {
                                  ClientManagerConfig.UsingCogbotFromRadgast = true;
                                  RadegastMain(args);
                              }
                          });

        }

        public static void AllocConsole()
        {
            if (!ClientManager.AllocedConsole)
            {
                ClientManager.AllocedConsole = DLRConsole.AllocConsole();
            }
        }

        /// <summary>
        /// Parsed command line options
        /// </summary>
        //public static CommandLine CommandLine;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]

        public static void RadegastMain(string[] args)
        {
            // Read command line options
            Radegast.CommandLine CommandLine = MainProgram.CommandLine = new Radegast.CommandLine();
            CommandLineParser parser = new CommandLineParser(new CommandLineParserSettings(DLRConsole.Out));
            if (!parser.ParseArguments(args, MainProgram.CommandLine))
            {
              //  Environment.Exit(1);
            }

            // Change current working directory to Radegast install dir
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            if (DLRConsole.HasWinforms)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
            }

            // Create main Radegast instance

            // See if we only wanted to display list of grids
            if (CommandLine.ListGrids)
            {
                DLRConsole.SystemWriteLine(CommandLine.GetHeader());
                DLRConsole.SystemWriteLine();
                Radegast.GridManager grids = new Radegast.GridManager();
                DLRConsole.SystemWriteLine("Use Grid ID as the parameter for --grid");
                DLRConsole.SystemWriteLine("{0,-25} - {1}", "Grid ID", "Grid Name");
                DLRConsole.SystemWriteLine("========================================================");

                for (int i = 0; i < grids.Count; i++)
                {
                    DLRConsole.SystemWriteLine("{0,-25} - {1}", grids[i].ID, grids[i].Name);
                }

                Environment.Exit(0);
            }

            if (false) Cogbot.ClientManager.SingleInstance.ProcessCommandArgs();
            ClientManager.InSTAThread(StartRadegast, "StartExtraRadegast").Join();
            //StartRadegast();
        }

        public static void StartRadegast()
        {
            // Create main Radegast instance

            RadegastInstance instance = new RadegastInstance(new GridClient());
            // See if we only wanted to display list of grids

            var mf = instance.MainForm;
            Application.Run(mf);
            instance = null;
        }

        public static bool UsingExceptionHandlers = false;
        public static bool UseApplicationExit = true;

        public static void SetExceptionHandlers(bool use)
        {
            if (use == UsingExceptionHandlers) return;
            UsingExceptionHandlers = use;
            if (use)
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += HandleThreadException;
                Application.ApplicationExit += HandleAppExit;
                Application.EnterThreadModal += HandleEnterThreadModal;
                Application.LeaveThreadModal += HandleLeaveThreadModal;
                Application.ThreadExit += HandleThreadExit;
                AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
                AppDomain.CurrentDomain.ProcessExit += HandleProcessExit;
            }
            else
            {
                // or ThrowException?
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.Automatic); 
                Application.ThreadException -= HandleThreadException;
                Application.ApplicationExit -= HandleAppExit;
                Application.EnterThreadModal -= HandleEnterThreadModal;
                Application.LeaveThreadModal -= HandleLeaveThreadModal;
                Application.ThreadExit -= HandleThreadExit;
                AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
                AppDomain.CurrentDomain.ProcessExit -= HandleProcessExit;
            }
        }

        private static void HandleProcessExit(object sender, EventArgs e)
        {
            DebugWrite("HandleProcessExit");
            var v = ClientManager.SingleInstance;
            if (v != null)
            {
                v.Quit();
            }
        }

        private static void HandleEnterThreadModal(object sender, EventArgs e)
        {
            DebugWrite("HandleEnterThreadModal");
        }
        private static void HandleLeaveThreadModal(object sender, EventArgs e)
        {
            DebugWrite("HandleLeaveThreadModal");
        }

        private static void HandleThreadExit(object sender, EventArgs e)
        {
            DebugWrite("HandleThreadExit");
        }

        private static void DebugWrite(string handlethreadexit)
        {
            FilteredWriteLine(handlethreadexit);
            bool wasMain = (Thread.CurrentThread == ClientManager.MainThread);
        }

        private static void HandleAppExit(object sender, EventArgs e)
        {
            DebugWrite("HandleAppExit");
            var v = ClientManager.SingleInstance;
            if (v != null)
            {
                v.Quit();
            }
            Environment.Exit(0);
        }

        private static void RunType(string c, string[] newArgs)
        {
            DoAndExit(() =>
            {
                Type t = Type.GetType(c, false, false);
                if (t == null) t = Type.GetType(c, false, true);
                if (t==null && c.Contains(":"))
                {
                    var ds = c.Split(':');
                    c = ds[1];
                    Assembly asem = AppDomain.CurrentDomain.Load(ds[0]);
                    t = asem.GetType(c, false, false);
                    if (t == null) t = asem.GetType(c, false, true);
                }
                if (t != null)
                {
                    RunType(t, newArgs);
                }
                else
                {
                    throw new Exception("Class not found: " + c);
                }
            });
        }

        private static void RunType(Type t, string[] newArgs)
        {
            MethodInfo mi = t.GetMethod("Main", new Type[] { typeof(string[]) });
            if (mi != null)
            {
                mi.Invoke(null, new object[] { newArgs });
                return;
            }
            mi = t.GetMethod("Main", new Type[] { });
            if (mi != null)
            {

                // Environment.CommandLine = "foo";
                mi.Invoke(null, new object[] { });
                return;
            }
            foreach (var s in t.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
            {
                if (s.Name.ToLower() == "main")
                {
                    var ps = s.GetParameters();
                    if (ps.Length == 0)
                    {

                        s.Invoke(null, new object[] { });
                        return;
                    }
                    if (ps.Length == 1)
                    {
                        mi.Invoke(null, new object[] { newArgs });
                        return;
                    }
                }
            }
            throw new MethodAccessException("No main for " + t);
        }

        static void HandleThreadException(object sender, ThreadExceptionEventArgs e)
        {
            _appException = (Exception)e.Exception;
            FilteredWriteLine("!!HandleThreadException!! " + e.Exception);
        }

        static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _appException = (Exception)e.ExceptionObject;
            FilteredWriteLine("!!HandleUnhandledException!! " + e.ExceptionObject);
        }
    }
}
