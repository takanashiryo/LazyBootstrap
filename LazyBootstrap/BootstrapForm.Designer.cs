namespace LazyBootstrap
{
    partial class BootstrapForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BootstrapForm));
            this.lblEaServer = new System.Windows.Forms.Label();
            this.txtEaServer = new System.Windows.Forms.TextBox();
            this.lblNetworkIp = new System.Windows.Forms.Label();
            this.txtNetworkIp = new System.Windows.Forms.TextBox();
            this.lblSubnetMask = new System.Windows.Forms.Label();
            this.txtSubnetMask = new System.Windows.Forms.TextBox();
            this.groupBoxOptions = new System.Windows.Forms.GroupBox();
            this.btnKillProcesses = new System.Windows.Forms.Button();
            this.chkNoRestoreRotation = new System.Windows.Forms.CheckBox();
            this.btnSwitchRotation = new System.Windows.Forms.Button();
            this.cmbRotation = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.chkNetDump = new System.Windows.Forms.CheckBox();
            this.chkNoAsphyxia = new System.Windows.Forms.CheckBox();
            this.chkWindowed = new System.Windows.Forms.CheckBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.groupBoxTools = new System.Windows.Forms.GroupBox();
            this.btnEditConfig = new System.Windows.Forms.Button();
            this.btnClearCache = new System.Windows.Forms.Button();
            this.lblLogOutput = new System.Windows.Forms.Label();
            this.txtLogOutput = new System.Windows.Forms.TextBox();
            this.lblPcbId = new System.Windows.Forms.Label();
            this.txtPcbId = new System.Windows.Forms.TextBox();
            this.groupBoxCompatLayer = new System.Windows.Forms.GroupBox();
            this.btnUnloadCompat = new System.Windows.Forms.Button();
            this.btnLoadCompat = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.optionsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.expertModeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupBoxOptions.SuspendLayout();
            this.groupBoxTools.SuspendLayout();
            this.groupBoxCompatLayer.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblEaServer
            // 
            this.lblEaServer.AutoSize = true;
            this.lblEaServer.Location = new System.Drawing.Point(13, 37);
            this.lblEaServer.Name = "lblEaServer";
            this.lblEaServer.Size = new System.Drawing.Size(89, 12);
            this.lblEaServer.TabIndex = 0;
            this.lblEaServer.Text = "EA服务器地址：";
            // 
            // txtEaServer
            // 
            this.txtEaServer.Location = new System.Drawing.Point(114, 34);
            this.txtEaServer.Name = "txtEaServer";
            this.txtEaServer.Size = new System.Drawing.Size(250, 21);
            this.txtEaServer.TabIndex = 1;
            // 
            // lblNetworkIp
            // 
            this.lblNetworkIp.AutoSize = true;
            this.lblNetworkIp.Location = new System.Drawing.Point(13, 91);
            this.lblNetworkIp.Name = "lblNetworkIp";
            this.lblNetworkIp.Size = new System.Drawing.Size(89, 12);
            this.lblNetworkIp.TabIndex = 4;
            this.lblNetworkIp.Text = "网络适配器IP：";
            // 
            // txtNetworkIp
            // 
            this.txtNetworkIp.Location = new System.Drawing.Point(114, 88);
            this.txtNetworkIp.Name = "txtNetworkIp";
            this.txtNetworkIp.Size = new System.Drawing.Size(250, 21);
            this.txtNetworkIp.TabIndex = 5;
            // 
            // lblSubnetMask
            // 
            this.lblSubnetMask.AutoSize = true;
            this.lblSubnetMask.Location = new System.Drawing.Point(13, 118);
            this.lblSubnetMask.Name = "lblSubnetMask";
            this.lblSubnetMask.Size = new System.Drawing.Size(101, 12);
            this.lblSubnetMask.TabIndex = 6;
            this.lblSubnetMask.Text = "网络适配器掩码：";
            // 
            // txtSubnetMask
            // 
            this.txtSubnetMask.Location = new System.Drawing.Point(114, 115);
            this.txtSubnetMask.Name = "txtSubnetMask";
            this.txtSubnetMask.Size = new System.Drawing.Size(250, 21);
            this.txtSubnetMask.TabIndex = 7;
            // 
            // groupBoxOptions
            // 
            this.groupBoxOptions.Controls.Add(this.btnKillProcesses);
            this.groupBoxOptions.Controls.Add(this.chkNoRestoreRotation);
            this.groupBoxOptions.Controls.Add(this.btnSwitchRotation);
            this.groupBoxOptions.Controls.Add(this.cmbRotation);
            this.groupBoxOptions.Controls.Add(this.label1);
            this.groupBoxOptions.Controls.Add(this.chkNetDump);
            this.groupBoxOptions.Controls.Add(this.chkNoAsphyxia);
            this.groupBoxOptions.Controls.Add(this.chkWindowed);
            this.groupBoxOptions.Controls.Add(this.btnStart);
            this.groupBoxOptions.Location = new System.Drawing.Point(15, 142);
            this.groupBoxOptions.Name = "groupBoxOptions";
            this.groupBoxOptions.Size = new System.Drawing.Size(349, 169);
            this.groupBoxOptions.TabIndex = 8;
            this.groupBoxOptions.TabStop = false;
            this.groupBoxOptions.Text = "启动选项";
            // 
            // btnKillProcesses
            // 
            this.btnKillProcesses.Font = new System.Drawing.Font("宋体", 9F);
            this.btnKillProcesses.Location = new System.Drawing.Point(17, 99);
            this.btnKillProcesses.Name = "btnKillProcesses";
            this.btnKillProcesses.Size = new System.Drawing.Size(125, 58);
            this.btnKillProcesses.TabIndex = 8;
            this.btnKillProcesses.Text = "结束所有进程";
            this.btnKillProcesses.UseVisualStyleBackColor = true;
            this.btnKillProcesses.Click += new System.EventHandler(this.btnKillProcesses_Click);
            // 
            // chkNoRestoreRotation
            // 
            this.chkNoRestoreRotation.AutoSize = true;
            this.chkNoRestoreRotation.Location = new System.Drawing.Point(163, 89);
            this.chkNoRestoreRotation.Name = "chkNoRestoreRotation";
            this.chkNoRestoreRotation.Size = new System.Drawing.Size(120, 16);
            this.chkNoRestoreRotation.TabIndex = 7;
            this.chkNoRestoreRotation.Text = "退出时不还原屏幕";
            this.chkNoRestoreRotation.UseVisualStyleBackColor = true;
            // 
            // btnSwitchRotation
            // 
            this.btnSwitchRotation.Location = new System.Drawing.Point(268, 134);
            this.btnSwitchRotation.Name = "btnSwitchRotation";
            this.btnSwitchRotation.Size = new System.Drawing.Size(54, 23);
            this.btnSwitchRotation.TabIndex = 6;
            this.btnSwitchRotation.Text = "切换";
            this.btnSwitchRotation.UseVisualStyleBackColor = true;
            this.btnSwitchRotation.Click += new System.EventHandler(this.btnSwitchRotation_Click);
            // 
            // cmbRotation
            // 
            this.cmbRotation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRotation.FormattingEnabled = true;
            this.cmbRotation.Location = new System.Drawing.Point(163, 136);
            this.cmbRotation.Name = "cmbRotation";
            this.cmbRotation.Size = new System.Drawing.Size(99, 20);
            this.cmbRotation.TabIndex = 5;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(161, 121);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(65, 12);
            this.label1.TabIndex = 4;
            this.label1.Text = "屏幕旋转：";
            // 
            // chkNetDump
            // 
            this.chkNetDump.AutoSize = true;
            this.chkNetDump.Location = new System.Drawing.Point(163, 67);
            this.chkNetDump.Name = "chkNetDump";
            this.chkNetDump.Size = new System.Drawing.Size(66, 16);
            this.chkNetDump.TabIndex = 3;
            this.chkNetDump.Text = "NetDump";
            this.chkNetDump.UseVisualStyleBackColor = true;
            // 
            // chkNoAsphyxia
            // 
            this.chkNoAsphyxia.AutoSize = true;
            this.chkNoAsphyxia.Location = new System.Drawing.Point(163, 45);
            this.chkNoAsphyxia.Name = "chkNoAsphyxia";
            this.chkNoAsphyxia.Size = new System.Drawing.Size(84, 16);
            this.chkNoAsphyxia.TabIndex = 2;
            this.chkNoAsphyxia.Text = "不启动氧无";
            this.chkNoAsphyxia.UseVisualStyleBackColor = true;
            // 
            // chkWindowed
            // 
            this.chkWindowed.AutoSize = true;
            this.chkWindowed.Location = new System.Drawing.Point(163, 23);
            this.chkWindowed.Name = "chkWindowed";
            this.chkWindowed.Size = new System.Drawing.Size(84, 16);
            this.chkWindowed.TabIndex = 1;
            this.chkWindowed.Text = "窗口化启动";
            this.chkWindowed.UseVisualStyleBackColor = true;
            // 
            // btnStart
            // 
            this.btnStart.Font = new System.Drawing.Font("宋体", 15F);
            this.btnStart.Location = new System.Drawing.Point(17, 28);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(125, 60);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "启动";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // groupBoxTools
            // 
            this.groupBoxTools.Controls.Add(this.btnEditConfig);
            this.groupBoxTools.Controls.Add(this.btnClearCache);
            this.groupBoxTools.Location = new System.Drawing.Point(15, 317);
            this.groupBoxTools.Name = "groupBoxTools";
            this.groupBoxTools.Size = new System.Drawing.Size(349, 100);
            this.groupBoxTools.TabIndex = 9;
            this.groupBoxTools.TabStop = false;
            this.groupBoxTools.Text = "工具";
            // 
            // btnEditConfig
            // 
            this.btnEditConfig.Location = new System.Drawing.Point(17, 61);
            this.btnEditConfig.Name = "btnEditConfig";
            this.btnEditConfig.Size = new System.Drawing.Size(305, 28);
            this.btnEditConfig.TabIndex = 1;
            this.btnEditConfig.Text = "编辑 spicecfg";
            this.btnEditConfig.UseVisualStyleBackColor = true;
            this.btnEditConfig.Click += new System.EventHandler(this.btnEditConfig_Click);
            // 
            // btnClearCache
            // 
            this.btnClearCache.Location = new System.Drawing.Point(17, 27);
            this.btnClearCache.Name = "btnClearCache";
            this.btnClearCache.Size = new System.Drawing.Size(305, 28);
            this.btnClearCache.TabIndex = 0;
            this.btnClearCache.Text = "清除 data_mods 缓存";
            this.btnClearCache.UseVisualStyleBackColor = true;
            this.btnClearCache.Click += new System.EventHandler(this.btnClearCache_Click);
            // 
            // lblLogOutput
            // 
            this.lblLogOutput.AutoSize = true;
            this.lblLogOutput.Location = new System.Drawing.Point(379, 37);
            this.lblLogOutput.Name = "lblLogOutput";
            this.lblLogOutput.Size = new System.Drawing.Size(65, 12);
            this.lblLogOutput.TabIndex = 11;
            this.lblLogOutput.Text = "日志输出：";
            // 
            // txtLogOutput
            // 
            this.txtLogOutput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLogOutput.Location = new System.Drawing.Point(381, 52);
            this.txtLogOutput.Multiline = true;
            this.txtLogOutput.Name = "txtLogOutput";
            this.txtLogOutput.ReadOnly = true;
            this.txtLogOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLogOutput.Size = new System.Drawing.Size(391, 461);
            this.txtLogOutput.TabIndex = 12;
            // 
            // lblPcbId
            // 
            this.lblPcbId.AutoSize = true;
            this.lblPcbId.Location = new System.Drawing.Point(13, 64);
            this.lblPcbId.Name = "lblPcbId";
            this.lblPcbId.Size = new System.Drawing.Size(47, 12);
            this.lblPcbId.TabIndex = 2;
            this.lblPcbId.Text = "PCBID：";
            // 
            // txtPcbId
            // 
            this.txtPcbId.Location = new System.Drawing.Point(114, 61);
            this.txtPcbId.Name = "txtPcbId";
            this.txtPcbId.Size = new System.Drawing.Size(250, 21);
            this.txtPcbId.TabIndex = 3;
            // 
            // groupBoxCompatLayer
            // 
            this.groupBoxCompatLayer.Controls.Add(this.btnUnloadCompat);
            this.groupBoxCompatLayer.Controls.Add(this.btnLoadCompat);
            this.groupBoxCompatLayer.Location = new System.Drawing.Point(15, 423);
            this.groupBoxCompatLayer.Name = "groupBoxCompatLayer";
            this.groupBoxCompatLayer.Size = new System.Drawing.Size(349, 80);
            this.groupBoxCompatLayer.TabIndex = 10;
            this.groupBoxCompatLayer.TabStop = false;
            this.groupBoxCompatLayer.Text = "AMD/Intel 兼容层";
            // 
            // btnUnloadCompat
            // 
            this.btnUnloadCompat.Location = new System.Drawing.Point(180, 29);
            this.btnUnloadCompat.Name = "btnUnloadCompat";
            this.btnUnloadCompat.Size = new System.Drawing.Size(142, 33);
            this.btnUnloadCompat.TabIndex = 1;
            this.btnUnloadCompat.Text = "卸载";
            this.btnUnloadCompat.UseVisualStyleBackColor = true;
            this.btnUnloadCompat.Click += new System.EventHandler(this.btnUnloadCompat_Click);
            // 
            // btnLoadCompat
            // 
            this.btnLoadCompat.Location = new System.Drawing.Point(17, 29);
            this.btnLoadCompat.Name = "btnLoadCompat";
            this.btnLoadCompat.Size = new System.Drawing.Size(142, 33);
            this.btnLoadCompat.TabIndex = 0;
            this.btnLoadCompat.Text = "载入";
            this.btnLoadCompat.UseVisualStyleBackColor = true;
            this.btnLoadCompat.Click += new System.EventHandler(this.btnLoadCompat_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip1.Location = new System.Drawing.Point(0, 516);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(784, 22);
            this.statusStrip1.SizingGrip = false;
            this.statusStrip1.TabIndex = 13;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(33, 17);
            this.statusLabel.Text = "就绪";
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.optionsMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(784, 24);
            this.menuStrip1.TabIndex = 14;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // optionsMenuItem
            // 
            this.optionsMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.expertModeMenuItem});
            this.optionsMenuItem.Name = "optionsMenuItem";
            this.optionsMenuItem.Size = new System.Drawing.Size(62, 20);
            this.optionsMenuItem.Text = "选项(&O)";
            // 
            // expertModeMenuItem
            // 
            this.expertModeMenuItem.CheckOnClick = true;
            this.expertModeMenuItem.Name = "expertModeMenuItem";
            this.expertModeMenuItem.Size = new System.Drawing.Size(180, 22);
            this.expertModeMenuItem.Text = "专家模式(&E)";
            this.expertModeMenuItem.Click += new System.EventHandler(this.expertModeMenuItem_Click);
            // 
            // BootstrapForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 538);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.groupBoxCompatLayer);
            this.Controls.Add(this.txtPcbId);
            this.Controls.Add(this.lblPcbId);
            this.Controls.Add(this.txtLogOutput);
            this.Controls.Add(this.lblLogOutput);
            this.Controls.Add(this.groupBoxTools);
            this.Controls.Add(this.groupBoxOptions);
            this.Controls.Add(this.txtSubnetMask);
            this.Controls.Add(this.lblSubnetMask);
            this.Controls.Add(this.txtNetworkIp);
            this.Controls.Add(this.lblNetworkIp);
            this.Controls.Add(this.txtEaServer);
            this.Controls.Add(this.lblEaServer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.MaximizeBox = false;
            this.Name = "BootstrapForm";
            this.Text = "SDVX 懒人包 启动程序";
            this.groupBoxOptions.ResumeLayout(false);
            this.groupBoxOptions.PerformLayout();
            this.groupBoxTools.ResumeLayout(false);
            this.groupBoxCompatLayer.ResumeLayout(false);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblEaServer;
        private System.Windows.Forms.TextBox txtEaServer;
        private System.Windows.Forms.Label lblNetworkIp;
        private System.Windows.Forms.TextBox txtNetworkIp;
        private System.Windows.Forms.Label lblSubnetMask;
        private System.Windows.Forms.TextBox txtSubnetMask;
        private System.Windows.Forms.GroupBox groupBoxOptions;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.CheckBox chkNetDump;
        private System.Windows.Forms.CheckBox chkNoAsphyxia;
        private System.Windows.Forms.CheckBox chkWindowed;
        private System.Windows.Forms.Button btnSwitchRotation;
        private System.Windows.Forms.ComboBox cmbRotation;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBoxTools;
        private System.Windows.Forms.Button btnEditConfig;
        private System.Windows.Forms.Button btnClearCache;
        private System.Windows.Forms.Label lblLogOutput;
        private System.Windows.Forms.TextBox txtLogOutput;
        private System.Windows.Forms.Label lblPcbId;
        private System.Windows.Forms.TextBox txtPcbId;
        private System.Windows.Forms.GroupBox groupBoxCompatLayer;
        private System.Windows.Forms.Button btnUnloadCompat;
        private System.Windows.Forms.Button btnLoadCompat;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.CheckBox chkNoRestoreRotation;
        private System.Windows.Forms.Button btnKillProcesses;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem optionsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem expertModeMenuItem;
    }
}