﻿using System.Windows.Forms;

namespace TheSimiansModule
{
    partial class SimThinkerDebug 
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.clientToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loginToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.logoutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.simbotStopToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.simbotThinkToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.simbotOffToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.submitButton = new System.Windows.Forms.Button();
            this.consoleInputText = new System.Windows.Forms.TextBox();
            this.consoleText = new System.Windows.Forms.TextBox();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.clientToolStripMenuItem,
            this.toolsToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(596, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // clientToolStripMenuItem
            // 
            this.clientToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loginToolStripMenuItem,
            this.logoutToolStripMenuItem,
            this.simbotStopToolStripMenuItem,
            this.simbotThinkToolStripMenuItem,
            this.simbotOffToolStripMenuItem});
            this.clientToolStripMenuItem.Name = "clientToolStripMenuItem";
            this.clientToolStripMenuItem.Size = new System.Drawing.Size(51, 20);
            this.clientToolStripMenuItem.Text = "SimBot";
            this.clientToolStripMenuItem.Click += new System.EventHandler(this.clientToolStripMenuItem_Click);
            // 
            // loginToolStripMenuItem
            // 
            this.loginToolStripMenuItem.Name = "loginToolStripMenuItem";
            this.loginToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.loginToolStripMenuItem.Text = "simbot info";
            this.loginToolStripMenuItem.Click += new System.EventHandler(this.NamedItemClick);
            // 
            // logoutToolStripMenuItem
            // 
            this.logoutToolStripMenuItem.Name = "logoutToolStripMenuItem";
            this.logoutToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.logoutToolStripMenuItem.Text = "simbot start";
            this.logoutToolStripMenuItem.Click += new System.EventHandler(this.NamedItemClick);
            // 
            // simbotStopToolStripMenuItem
            // 
            this.simbotStopToolStripMenuItem.Name = "simbotStopToolStripMenuItem";
            this.simbotStopToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.simbotStopToolStripMenuItem.Text = "simbot stop";
            // 
            // simbotThinkToolStripMenuItem
            // 
            this.simbotThinkToolStripMenuItem.Name = "simbotThinkToolStripMenuItem";
            this.simbotThinkToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.simbotThinkToolStripMenuItem.Text = "simbot think";
            // 
            // simbotOffToolStripMenuItem
            // 
            this.simbotOffToolStripMenuItem.Name = "simbotOffToolStripMenuItem";
            this.simbotOffToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.simbotOffToolStripMenuItem.Text = "simbot off";
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.toolsToolStripMenuItem.Text = "Tools";
            // 
            // submitButton
            // 
            this.submitButton.Enabled = false;
            this.submitButton.Location = new System.Drawing.Point(501, 354);
            this.submitButton.Name = "submitButton";
            this.submitButton.Size = new System.Drawing.Size(70, 19);
            this.submitButton.TabIndex = 13;
            this.submitButton.Text = "Submit";
            this.submitButton.UseVisualStyleBackColor = true;
            this.submitButton.Click += new System.EventHandler(this.submitButton_Click);
            // 
            // consoleInputText
            // 
            this.consoleInputText.AcceptsReturn = true;
            this.consoleInputText.Enabled = false;
            this.consoleInputText.Location = new System.Drawing.Point(12, 354);
            this.consoleInputText.Name = "consoleInputText";
            this.consoleInputText.Size = new System.Drawing.Size(483, 20);
            this.consoleInputText.TabIndex = 12;
            this.consoleInputText.TextChanged += new System.EventHandler(this.consoleInputText_TextChanged);
            this.consoleInputText.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.consoleInputText_KeyPress);
            // 
            // consoleText
            // 
            this.consoleText.BackColor = System.Drawing.SystemColors.Window;
            this.consoleText.Location = new System.Drawing.Point(12, 27);
            this.consoleText.Multiline = true;
            this.consoleText.Name = "consoleText";
            this.consoleText.ReadOnly = true;
            this.consoleText.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.consoleText.Size = new System.Drawing.Size(572, 321);
            this.consoleText.TabIndex = 11;
            this.consoleText.TextChanged += new System.EventHandler(this.consoleText_TextChanged);
            // 
            // SimThinkerDebug
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.submitButton);
            this.Controls.Add(this.consoleInputText);
            this.Controls.Add(this.consoleText);
            this.Controls.Add(this.menuStrip1);
            this.Name = "SimThinkerDebug";
            this.Size = new System.Drawing.Size(596, 386);
            this.Load += new System.EventHandler(this.TextForm_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem clientToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loginToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem logoutToolStripMenuItem;
        private System.Windows.Forms.Button submitButton;
        private System.Windows.Forms.TextBox consoleInputText;
        private System.Windows.Forms.TextBox consoleText;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private ToolStripMenuItem simbotStopToolStripMenuItem;
        private ToolStripMenuItem simbotThinkToolStripMenuItem;
        private ToolStripMenuItem simbotOffToolStripMenuItem;
    }
}
