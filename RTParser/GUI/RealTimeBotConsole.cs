﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MushDLR223.ScriptEngines;
using MushDLR223.Utilities;
using RTParser;
using RTParser.Utils;
using Exception=System.Exception;
using String=System.String;
using RTParser.Variables;

namespace RTParser.GUI
{
    public sealed partial class AIMLPadEditor : Form
    {
        private delegate void ShowDelegate();
        
        //public event FormClosedEventHandler Closing;
        private MenuStrip _MainMenuStrip;

        public MenuStrip MainMenuStrip
        {
            get { return _MainMenuStrip; }
            set { _MainMenuStrip = value; }
        }

        public void Show()
        {
            if (this.InvokeRequired)
            {
                Invoke(new ShowDelegate(Show), new object[] { });
                return;
            } 
            Reactivate();
            TextForm_ResizeEnd(null, null);
        }

        private void Reactivate()
        {
            try
            {
                baseShow();

                //if (this.WindowState == FormWindowState.Minimized)              
              //  base.WindowState = FormWindowState.Normal;
                if (!base.Visible) base.Visible = true;
                //base.Activate();
               // client.ShowTab(GetTabName());
                
            }
            catch (Exception e)
            {
                DLRConsole.DebugWriteLine("" + e);
            }
        }

        public string GetTabName()
        {
            return _tabname;
        }

        private void baseShow()
        {
            try
            {
                base.Show();
            }
            catch (Exception e)
            {
                DLRConsole.DebugWriteLine("" + e);
            }
        }


        private RTParser.RTPBot robot;
        private readonly string _tabname;
        private Thread TodoThread;
        private object ExecuteCommandLock = new object();
        private User user;

        public AIMLPadEditor(string name, RTParser.RTPBot bc)
        {
            robot = bc;
            _tabname = name;
            this.Name = name;
            InitializeComponent();
            this.Size = new Size(this.Size.Width + 300, this.Size.Height);
            this.Text = string.Format("SimThinker Debug {0}", name);
            this.SizeChanged += TextForm_ResizeEnd;
            this.Resize += TextForm_ResizeEnd;
            this.Dock = DockStyle.Fill;
            Visible = true;
            consoleInputText.Enabled = true;
            consoleInputText.Focus();
        }

        private void TextForm_ResizeEnd(object sender, EventArgs e)
        {
            return;
            consoleText.Size = new Size(this.Size.Width - 65, this.Size.Height - 100);
            consoleInputText.Top = this.Size.Height - 60;
            thatInputBox.Top = this.Size.Height - 120;
            consoleInputText.Size = new Size(this.Size.Width - 125, consoleInputText.Height);
            this.submitButton.Location = new System.Drawing.Point(this.Size.Width - 90, consoleInputText.Top);
        }

        private void TextForm_Load(object sender, EventArgs e)
        {
            PopulateRobotChange();
        }

        private void consoleInputText_KeyUp(object sender, KeyEventArgs e)
        {
            if (Convert.ToInt32(e.KeyValue) == 13)
                acceptConsoleInput();
        }


        public void acceptConsoleInput()
        {
            string text = consoleInputText.Text;
            if (text.Length > 0)
            {
                //WriteLine(text);
                AddHistory(consoleInputText);
                consoleInputText.Text = "";
                ExecuteCommand(text);
                //if (describeNext)
                //{
                //    describeNext = false;
                //    describeSituation();
                //}
            }
        }

        private static void AddHistory(ComboBox box)
        {
            box.Items.Insert(0, box.Text);
        }

        private void ExecuteCommand(string text)
        {
            lock (ExecuteCommandLock)
            {
                if (TodoThread != null)
                {
                    TodoThread.Abort();
                    TodoThread = null;
                }
                AIMLPadEditor edit = this;
                submitButton.Enabled = false;
                TodoThread = new Thread(() =>
                                            {
                                                robot.AcceptInput(WriteLine, text, user);
                                                if (InvokeRequired)
                                                {
                                                    Invoke(new ThreadStart(() =>
                                                                               {
                                                                                   edit.PopulateRobotChange();
                                                                                   edit.PopulateUserChange();
                                                                                   submitButton.Enabled = true;
                                                                                   consoleInputText.SelectAll();
                                                                               }));
                                                }
                                            });
                TodoThread.Start();
            }
        }

        public void WriteLine(string str, params object[] args)
        {
            try
            {
                if (str == null) return;
                if (args != null && args.Length > 0) str = String.Format(str, args);
                str = str.TrimEnd();
                if (str == "") return;

                str = str.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
                //if (str.ToLower().Contains("look")) return;
                if (IsDisposed) return; // for (un)clean exits
                InvokeIfNeeded(this, () => { doOutput(str); });
            }
            catch (Exception e)
            {
                DLRConsole.DebugWriteLine("" + e);
            }
        }

        public void doOutput(string str, params object[] args)
        {
            if (str == null) return;
            if (args.Length > 0) str = String.Format(str, args);
            str = str.TrimEnd();
            if (str == "") return;
            try
            {
                // lock (consoleText)
                {
                    if (consoleText.IsDisposed) return; // for (un)clean exits
                    consoleText.AppendText(String.Format("{0}\r\n", str));
                }
            }
            catch (Exception e)
            {
                DLRConsole.DebugWriteLine("" + e);
                // probably dead anyway ...
            }
            return;
        }

    
        private void thatInputBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedIndexChanged(thatInputBox);
            SetVariable("that", thatInputBox);
        }

        private void consoleInputText_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedIndexChanged(consoleInputText);      
            //acceptConsoleInput();
        }

        private void SelectedIndexChanged(ComboBox box)
        {
            box.Text = box.SelectedItem.ToString();

        }

        private void submitButton_Click_1(object sender, EventArgs e)
        {
            acceptConsoleInput();
        }

        private void thatInputBox_KeyUp(object sender, KeyEventArgs e)
        {
            KeyUpBox(thatInputBox, e);
            SetVariable("that", thatInputBox);
        }

        private void KeyUpBox(ComboBox box, KeyEventArgs e)
        {
            if (e.KeyValue == 13)
            {
                if (box.Items.Count > 0 && box.Text != box.Items[0].ToString())
                {
                    box.Items.Insert(0, box.Text);
                }
            }
        }

        private void topicInputBox_KeyUp(object sender, KeyEventArgs e)
        {
            KeyUpBox(topicInputBox, e);
            SetVariable("topic", topicInputBox);
        }

        private void SetVariable(string topic, ComboBox box)
        {
            if (user != null) user.Predicates.updateSetting(topic, box.Text);
        }

        private void userNameBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            user = robot.FindOrCreateUser(userNameBox.Text);
            PopulateUserChange();
        }

        public void PopulateUserChange()
        {
            GetVariable("that", thatInputBox);
            GetVariable("topic", topicInputBox);
            GetVariable("username", userNameBox);
            GetVariable("lastsaid", consoleInputText);
            GetVariable("startgraph", graphNameBox);
            PoputateVariablesList();
        }

        private void PoputateVariablesList()
        {
            var dict = SelectedDictionrary();
            if (dict == null) return;
            variablesOutput.Text = dict.ToDebugString().Replace("\n", "\r\n");
        }

        private SettingsDictionary SelectedDictionrary()
        {
            switch(dictionaryNameBox.Text)
            {
                case "@bot":
                    return robot.BotAsUser.Predicates;
                case "@global":
                    return robot.GlobalSettings;
                case "@user":
                    return robot.LastUser.Predicates;
                case "@get":
                case "@set":
                default:
                    break;
            }
            if (user != null) return user.Predicates;
            return null;
        }

        public void PopulateRobotChange()
        {
            if (robot == null) return;
            GetVariable("you", robotNameBox);
            robotNameBox.Text = robot.NameAsSet;
            foreach (var name in RTPBot.Robots.Keys)
            {
                InsertNewItem(robotNameBox,name);
            }
            foreach (var user0 in robot.SetOfUsers)
            {
                InsertNewItem(userNameBox, user0.UserName);
            }
            foreach (var g in RTPBot.GraphsByName.Values)
            {
                InsertNewItem(graphNameBox, g.ScriptingName);
            }
            foreach (var g in RTPBot.GraphsByName.Keys)
            {
                InsertNewItem(graphNameBox, g);
            }
            user = robot.LastUser;
        }

        private static void InsertNewItem(ComboBox box, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            lock (box)
            {
                if (box.Items.Contains(name)) return;
                box.Items.Insert(0, name);
            }
        }

        private void GetVariable(string name, ComboBox box)
        {
            if (user != null)
            {
                var value = user.Predicates.grabSetting(name);
                if (value != null)
                {
                    InvokeIfNeeded(box, () => box.Text = value.AsString());
                }
            }
        }

        private void InvokeIfNeeded(Control box, Action func)
        {
            if (box.InvokeRequired)
            {
                box.Invoke(func);
            }
            else
            {
                func();
            }
        }

        private void robotNameBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            robot =  RTPBot.FindOrCreateRobot(robotNameBox.Text);
            PopulateRobotChange();
        }

        private void graphNameBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetVariable("startgraph", graphNameBox);
        }

        private void graphNameBox_KeyUp(object sender, KeyEventArgs e)
        {
            KeyUpBox(graphNameBox, e);
            SetVariable("startgraph", graphNameBox);
            if (user != null) user.StartGraphName = graphNameBox.Text;
        }

        private void dictionaryNameBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            PoputateVariablesList();
        }

        private void consoleText_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
