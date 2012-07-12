using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MushDLR223.ScriptEngines;
using MushDLR223.Utilities;
using OpenMetaverse;
using System.IO;
using Settings=OpenMetaverse.Settings;
using Console = MushDLR223.Utilities.DLRConsole;

namespace Cogbot
{
    public class Program
    {
        private static void Usage()
        {
            DLRConsole.SystemWriteLine("Usage: " + Environment.NewLine +
                    "cogbot.exe --first firstname --last lastname --pass password [--loginuri=\"uri\"] [--startpos \"sim/x/y/z\"] [--master \"master name\"] [--masterkey \"master uuid\"] [--gettextures] [--scriptfile \"filename\"] [--nogui]");
        }

        public static DLRConsole consoleBase;


        [STAThread]
        internal static void Main(string[] args)
        {
            ClientManagerConfig.UsingCogbotFromRadgast = false;
            ClientManagerConfig.UsingRadgastFromCogbot = true;
           // MainProgram.CommandLine = new CommandLine {DisableSound = false};

            if (!ClientManager.AllocedConsole)
            {
                ClientManager.AllocedConsole = DLRConsole.AllocConsole();
            }

            if (ClientManager.MainThread == null)
            {
                ClientManager.MainThread = Thread.CurrentThread;
            }
            //  NativeMethods.AllocConsole();
            // Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainRun(args);
        }

        public static void MainRun(string[] args)
        {
            ClientManagerConfig.arguments = new Parser(args);
            consoleBase = new DLRConsole("textform");
            ClientManager manager = ClientManager.SingleInstance;
            manager.outputDelegate = new OutputDelegate(WriteLine);
            if (!manager.ProcessCommandArgs())
            {
                Usage();
                return;
            }
            manager.Run();
        }


        public void WriteLine(ConsoleColor color, string format, params object[] args)
        {
            DLRConsole.WriteConsoleLine(color, format, args);
            /*
            try
            {
                if (color != ConsoleColor.White)
                    DLRConsole.SystemForegroundColor = color;
                DLRConsole.SystemWriteLine(format, args);
            }
            finally
            {
                Console.ResetColor();                
            }
             */
        }
        public string CmdPrompt(string p)
        {
            Console.SystemWrite(p);
            Console.SystemFlush();
            return Console.ReadLine();
        }

        public static void WriteLine(string str, params object[] args)
        {
            if (consoleBase == null)
            {
                DLRConsole.DebugWriteLine(str, args);
                return;
            }
            int index = str.IndexOf("]");
            if (index > 0 && str.StartsWith("["))
            {
                string sender = str.Substring(0, index).Trim();
                if (sender.StartsWith("[")) sender = sender.Substring(1);
                str = str.Substring(index + 1).Trim();
                consoleBase.Notice(sender, str, args);
            }
            else
            {
                consoleBase.Notice(str, args);
            }
        }

        public static void Notice(string sender, string str, params object[] args)
        {
            if (consoleBase == null)
            {
                try
                {
                    DLRConsole.SystemWriteLine("[" + sender + "] " + str, args);
                }
                catch (FormatException)
                {

                    DLRConsole.SystemWriteLine("[" + sender + "] " + str);
                }
                return;
            }
            consoleBase.Notice(sender, str, args);
        }
    }
}