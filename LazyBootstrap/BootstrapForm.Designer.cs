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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BootstrapForm));
            this.groupBoxOptions = new System.Windows.Forms.GroupBox();
            this.chkNoRestoreRotation = new System.Windows.Forms.CheckBox();
            this.btnOpenLog = new System.Windows.Forms.Button();
            this.btnEditConfig = new System.Windows.Forms.Button();
            this.btnSwitchRotation = new System.Windows.Forms.Button();
            this.cmbRotation = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.chkUsePreconfig = new System.Windows.Forms.CheckBox();
            this.btnAdvancedOptions = new System.Windows.Forms.Button();
            this.chkNoAsphyxia = new System.Windows.Forms.CheckBox();
            this.chkWindowed = new System.Windows.Forms.CheckBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnKillProcesses = new System.Windows.Forms.Button();
            this.groupBoxTools = new System.Windows.Forms.GroupBox();
            this.btnAudioPanel = new System.Windows.Forms.Button();
            this.btnInstallRuntime = new System.Windows.Forms.Button();
            this.btnClearCache = new System.Windows.Forms.Button();
            this.btnAddFirewallRule = new System.Windows.Forms.Button();
            this.lblLogOutput = new System.Windows.Forms.Label();
            this.txtLogOutput = new System.Windows.Forms.RichTextBox();
            this.groupBoxCompatLayer = new System.Windows.Forms.GroupBox();
            this.cmbCompatType = new System.Windows.Forms.ComboBox();
            this.lblCompatStatus = new System.Windows.Forms.Label();
            this.btnUnloadCompat = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusProgress = new System.Windows.Forms.ToolStripProgressBar();
            this.toolTip0 = new System.Windows.Forms.ToolTip(this.components);
            this.lblCurrentVersion = new System.Windows.Forms.Label();
            this.txtCurrentVersion = new System.Windows.Forms.TextBox();
            this.lblRevision = new System.Windows.Forms.Label();
            this.txtRevision = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnManageServer = new System.Windows.Forms.Button();
            this.groupBoxOptions.SuspendLayout();
            this.groupBoxTools.SuspendLayout();
            this.groupBoxCompatLayer.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxOptions
            // 
            this.groupBoxOptions.Controls.Add(this.chkNoRestoreRotation);
            this.groupBoxOptions.Controls.Add(this.btnOpenLog);
            this.groupBoxOptions.Controls.Add(this.btnEditConfig);
            this.groupBoxOptions.Controls.Add(this.btnSwitchRotation);
            this.groupBoxOptions.Controls.Add(this.cmbRotation);
            this.groupBoxOptions.Controls.Add(this.label4);
            this.groupBoxOptions.Controls.Add(this.label2);
            this.groupBoxOptions.Controls.Add(this.label1);
            this.groupBoxOptions.Controls.Add(this.chkUsePreconfig);
            this.groupBoxOptions.Controls.Add(this.btnManageServer);
            this.groupBoxOptions.Controls.Add(this.btnAdvancedOptions);
            this.groupBoxOptions.Controls.Add(this.chkNoAsphyxia);
            this.groupBoxOptions.Controls.Add(this.chkWindowed);
            this.groupBoxOptions.Controls.Add(this.btnStart);
            this.groupBoxOptions.Location = new System.Drawing.Point(20, 46);
            this.groupBoxOptions.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBoxOptions.Name = "groupBoxOptions";
            this.groupBoxOptions.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBoxOptions.Size = new System.Drawing.Size(491, 260);
            this.groupBoxOptions.TabIndex = 8;
            this.groupBoxOptions.TabStop = false;
            this.groupBoxOptions.Text = "启动选项";
            // 
            // chkNoRestoreRotation
            // 
            this.chkNoRestoreRotation.AutoSize = true;
            this.chkNoRestoreRotation.Location = new System.Drawing.Point(217, 223);
            this.chkNoRestoreRotation.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.chkNoRestoreRotation.Name = "chkNoRestoreRotation";
            this.chkNoRestoreRotation.Size = new System.Drawing.Size(149, 20);
            this.chkNoRestoreRotation.TabIndex = 7;
            this.chkNoRestoreRotation.Text = "退出时不还原屏幕";
            this.toolTip0.SetToolTip(this.chkNoRestoreRotation, "退出游戏进程时保持当前的旋转方向");
            this.chkNoRestoreRotation.UseVisualStyleBackColor = true;
            // 
            // btnOpenLog
            // 
            this.btnOpenLog.Location = new System.Drawing.Point(25, 209);
            this.btnOpenLog.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnOpenLog.Name = "btnOpenLog";
            this.btnOpenLog.Size = new System.Drawing.Size(167, 37);
            this.btnOpenLog.TabIndex = 12;
            this.btnOpenLog.Text = "查看 log.txt";
            this.btnOpenLog.UseVisualStyleBackColor = true;
            this.btnOpenLog.Click += new System.EventHandler(this.btnOpenLog_Click);
            // 
            // btnEditConfig
            // 
            this.btnEditConfig.Location = new System.Drawing.Point(27, 166);
            this.btnEditConfig.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnEditConfig.Name = "btnEditConfig";
            this.btnEditConfig.Size = new System.Drawing.Size(167, 37);
            this.btnEditConfig.TabIndex = 1;
            this.btnEditConfig.Text = "编辑 spicecfg";
            this.toolTip0.SetToolTip(this.btnEditConfig, "如果你勾选了“使用预配置文件”，则会编辑预配置文件");
            this.btnEditConfig.UseVisualStyleBackColor = true;
            this.btnEditConfig.Click += new System.EventHandler(this.btnEditConfig_Click);
            // 
            // btnSwitchRotation
            // 
            this.btnSwitchRotation.Location = new System.Drawing.Point(396, 187);
            this.btnSwitchRotation.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnSwitchRotation.Name = "btnSwitchRotation";
            this.btnSwitchRotation.Size = new System.Drawing.Size(72, 26);
            this.btnSwitchRotation.TabIndex = 6;
            this.btnSwitchRotation.Text = "切换";
            this.btnSwitchRotation.UseVisualStyleBackColor = true;
            this.btnSwitchRotation.Click += new System.EventHandler(this.btnSwitchRotation_Click);
            // 
            // cmbRotation
            // 
            this.cmbRotation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRotation.FormattingEnabled = true;
            this.cmbRotation.Location = new System.Drawing.Point(217, 187);
            this.cmbRotation.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cmbRotation.Name = "cmbRotation";
            this.cmbRotation.Size = new System.Drawing.Size(159, 24);
            this.cmbRotation.TabIndex = 5;
            this.toolTip0.SetToolTip(this.cmbRotation, "选择你希望屏幕旋转的角度（逆时针）");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(215, 127);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(82, 16);
            this.label2.TabIndex = 4;
            this.label2.Text = "高级选项：";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(215, 166);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(82, 16);
            this.label1.TabIndex = 4;
            this.label1.Text = "屏幕旋转：";
            // 
            // chkUsePreconfig
            // 
            this.chkUsePreconfig.AutoSize = true;
            this.chkUsePreconfig.Checked = true;
            this.chkUsePreconfig.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkUsePreconfig.Location = new System.Drawing.Point(25, 127);
            this.chkUsePreconfig.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.chkUsePreconfig.Name = "chkUsePreconfig";
            this.chkUsePreconfig.Size = new System.Drawing.Size(134, 20);
            this.chkUsePreconfig.TabIndex = 9;
            this.chkUsePreconfig.Text = "使用预配置文件";
            this.toolTip0.SetToolTip(this.chkUsePreconfig, "使用预先配置好的最优配置文件来启动游戏，以防止因错误的系统内建配置造成的干扰\r\n两者互相独立，取消勾选以使用系统内建配置");
            this.chkUsePreconfig.UseVisualStyleBackColor = true;
            this.chkUsePreconfig.CheckedChanged += new System.EventHandler(this.chkUsePreconfig_CheckedChanged);
            // 
            // btnAdvancedOptions
            // 
            this.btnAdvancedOptions.Location = new System.Drawing.Point(320, 125);
            this.btnAdvancedOptions.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnAdvancedOptions.Name = "btnAdvancedOptions";
            this.btnAdvancedOptions.Size = new System.Drawing.Size(73, 28);
            this.btnAdvancedOptions.TabIndex = 13;
            this.btnAdvancedOptions.Text = "设定";
            this.toolTip0.SetToolTip(this.btnAdvancedOptions, "打开高级选项菜单");
            this.btnAdvancedOptions.UseVisualStyleBackColor = true;
            this.btnAdvancedOptions.Click += new System.EventHandler(this.btnAdvancedOptions_Click);
            // 
            // chkNoAsphyxia
            // 
            this.chkNoAsphyxia.AutoSize = true;
            this.chkNoAsphyxia.Location = new System.Drawing.Point(217, 60);
            this.chkNoAsphyxia.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.chkNoAsphyxia.Name = "chkNoAsphyxia";
            this.chkNoAsphyxia.Size = new System.Drawing.Size(104, 20);
            this.chkNoAsphyxia.TabIndex = 2;
            this.chkNoAsphyxia.Text = "不启动氧无";
            this.toolTip0.SetToolTip(this.chkNoAsphyxia, "启动时不启动氧无，连接在线服时可勾选");
            this.chkNoAsphyxia.UseVisualStyleBackColor = true;
            // 
            // chkWindowed
            // 
            this.chkWindowed.AutoSize = true;
            this.chkWindowed.Location = new System.Drawing.Point(217, 31);
            this.chkWindowed.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.chkWindowed.Name = "chkWindowed";
            this.chkWindowed.Size = new System.Drawing.Size(104, 20);
            this.chkWindowed.TabIndex = 1;
            this.chkWindowed.Text = "窗口化启动";
            this.toolTip0.SetToolTip(this.chkWindowed, "以窗口化模式运行游戏");
            this.chkWindowed.UseVisualStyleBackColor = true;
            // 
            // btnStart
            // 
            this.btnStart.Font = new System.Drawing.Font("宋体", 10F);
            this.btnStart.Location = new System.Drawing.Point(23, 27);
            this.btnStart.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(167, 90);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "启动游戏";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnKillProcesses
            // 
            this.btnKillProcesses.Font = new System.Drawing.Font("宋体", 9F);
            this.btnKillProcesses.Location = new System.Drawing.Point(25, 121);
            this.btnKillProcesses.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnKillProcesses.Name = "btnKillProcesses";
            this.btnKillProcesses.Size = new System.Drawing.Size(445, 39);
            this.btnKillProcesses.TabIndex = 8;
            this.btnKillProcesses.Text = "结束所有进程";
            this.toolTip0.SetToolTip(this.btnKillProcesses, "强制结束所有游戏相关进程");
            this.btnKillProcesses.UseVisualStyleBackColor = true;
            this.btnKillProcesses.Click += new System.EventHandler(this.btnKillProcesses_Click);
            // 
            // groupBoxTools
            // 
            this.groupBoxTools.Controls.Add(this.btnAudioPanel);
            this.groupBoxTools.Controls.Add(this.btnKillProcesses);
            this.groupBoxTools.Controls.Add(this.btnInstallRuntime);
            this.groupBoxTools.Controls.Add(this.btnClearCache);
            this.groupBoxTools.Controls.Add(this.btnAddFirewallRule);
            this.groupBoxTools.Location = new System.Drawing.Point(20, 314);
            this.groupBoxTools.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBoxTools.Name = "groupBoxTools";
            this.groupBoxTools.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBoxTools.Size = new System.Drawing.Size(491, 187);
            this.groupBoxTools.TabIndex = 9;
            this.groupBoxTools.TabStop = false;
            this.groupBoxTools.Text = "工具";
            // 
            // btnAudioPanel
            // 
            this.btnAudioPanel.Location = new System.Drawing.Point(27, 74);
            this.btnAudioPanel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnAudioPanel.Name = "btnAudioPanel";
            this.btnAudioPanel.Size = new System.Drawing.Size(208, 39);
            this.btnAudioPanel.TabIndex = 9;
            this.btnAudioPanel.Text = "打开音频控制面板";
            this.btnAudioPanel.UseVisualStyleBackColor = true;
            this.btnAudioPanel.Click += new System.EventHandler(this.btnAudioPanel_Click);
            // 
            // btnInstallRuntime
            // 
            this.btnInstallRuntime.Location = new System.Drawing.Point(256, 74);
            this.btnInstallRuntime.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnInstallRuntime.Name = "btnInstallRuntime";
            this.btnInstallRuntime.Size = new System.Drawing.Size(215, 39);
            this.btnInstallRuntime.TabIndex = 1;
            this.btnInstallRuntime.Text = "安装运行库";
            this.toolTip0.SetToolTip(this.btnInstallRuntime, "安装必要的游戏运行库");
            this.btnInstallRuntime.UseVisualStyleBackColor = true;
            this.btnInstallRuntime.Click += new System.EventHandler(this.btnInstallRuntime_Click);
            // 
            // btnClearCache
            // 
            this.btnClearCache.Location = new System.Drawing.Point(25, 27);
            this.btnClearCache.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnClearCache.Name = "btnClearCache";
            this.btnClearCache.Size = new System.Drawing.Size(209, 39);
            this.btnClearCache.TabIndex = 0;
            this.btnClearCache.Text = "清除 data_mods 缓存";
            this.toolTip0.SetToolTip(this.btnClearCache, "清除data_mods缓存文件\r\n更新游戏后需清除，以重建歌曲类模组数据库");
            this.btnClearCache.UseVisualStyleBackColor = true;
            this.btnClearCache.Click += new System.EventHandler(this.btnClearCache_Click);
            // 
            // btnAddFirewallRule
            // 
            this.btnAddFirewallRule.Location = new System.Drawing.Point(256, 27);
            this.btnAddFirewallRule.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnAddFirewallRule.Name = "btnAddFirewallRule";
            this.btnAddFirewallRule.Size = new System.Drawing.Size(215, 39);
            this.btnAddFirewallRule.TabIndex = 2;
            this.btnAddFirewallRule.Text = "添加防火墙规则";
            this.toolTip0.SetToolTip(this.btnAddFirewallRule, "添加允许游戏通过防火墙的规则");
            this.btnAddFirewallRule.UseVisualStyleBackColor = true;
            this.btnAddFirewallRule.Click += new System.EventHandler(this.btnAddFirewallRule_Click);
            // 
            // lblLogOutput
            // 
            this.lblLogOutput.AutoSize = true;
            this.lblLogOutput.Location = new System.Drawing.Point(523, 46);
            this.lblLogOutput.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblLogOutput.Name = "lblLogOutput";
            this.lblLogOutput.Size = new System.Drawing.Size(82, 16);
            this.lblLogOutput.TabIndex = 11;
            this.lblLogOutput.Text = "日志输出：";
            // 
            // txtLogOutput
            // 
            this.txtLogOutput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLogOutput.BackColor = System.Drawing.Color.Black;
            this.txtLogOutput.Location = new System.Drawing.Point(525, 73);
            this.txtLogOutput.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtLogOutput.Name = "txtLogOutput";
            this.txtLogOutput.ReadOnly = true;
            this.txtLogOutput.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.txtLogOutput.Size = new System.Drawing.Size(503, 555);
            this.txtLogOutput.TabIndex = 12;
            this.txtLogOutput.Text = "";
            // 
            // groupBoxCompatLayer
            // 
            this.groupBoxCompatLayer.Controls.Add(this.cmbCompatType);
            this.groupBoxCompatLayer.Controls.Add(this.lblCompatStatus);
            this.groupBoxCompatLayer.Controls.Add(this.btnUnloadCompat);
            this.groupBoxCompatLayer.Controls.Add(this.button1);
            this.groupBoxCompatLayer.Controls.Add(this.label3);
            this.groupBoxCompatLayer.Location = new System.Drawing.Point(20, 508);
            this.groupBoxCompatLayer.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBoxCompatLayer.Name = "groupBoxCompatLayer";
            this.groupBoxCompatLayer.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBoxCompatLayer.Size = new System.Drawing.Size(491, 121);
            this.groupBoxCompatLayer.TabIndex = 10;
            this.groupBoxCompatLayer.TabStop = false;
            this.groupBoxCompatLayer.Text = "AMD/Intel 显卡兼容层";
            this.toolTip0.SetToolTip(this.groupBoxCompatLayer, "载入AMD/Intel显卡的兼容层");
            // 
            // cmbCompatType
            // 
            this.cmbCompatType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCompatType.FormattingEnabled = true;
            this.cmbCompatType.Items.AddRange(new object[] {
            "dx9on12",
            "dxvk"});
            this.cmbCompatType.Location = new System.Drawing.Point(304, 81);
            this.cmbCompatType.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cmbCompatType.Name = "cmbCompatType";
            this.cmbCompatType.Size = new System.Drawing.Size(160, 24);
            this.cmbCompatType.TabIndex = 3;
            this.toolTip0.SetToolTip(this.cmbCompatType, "选择渲染模式\r\ndx9on12：默认，通常情况下可提供最好的效果，窗口化会白屏\r\ndxvk：备用，可能会出现轨道缺失等问题");
            // 
            // lblCompatStatus
            // 
            this.lblCompatStatus.AutoSize = true;
            this.lblCompatStatus.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold);
            this.lblCompatStatus.ForeColor = System.Drawing.Color.Red;
            this.lblCompatStatus.Location = new System.Drawing.Point(23, 86);
            this.lblCompatStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblCompatStatus.Name = "lblCompatStatus";
            this.lblCompatStatus.Size = new System.Drawing.Size(64, 15);
            this.lblCompatStatus.TabIndex = 2;
            this.lblCompatStatus.Text = "● 未知";
            // 
            // btnUnloadCompat
            // 
            this.btnUnloadCompat.Location = new System.Drawing.Point(256, 27);
            this.btnUnloadCompat.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnUnloadCompat.Name = "btnUnloadCompat";
            this.btnUnloadCompat.Size = new System.Drawing.Size(209, 44);
            this.btnUnloadCompat.TabIndex = 1;
            this.btnUnloadCompat.Text = "关闭";
            this.btnUnloadCompat.UseVisualStyleBackColor = true;
            this.btnUnloadCompat.Click += new System.EventHandler(this.btnUnloadCompat_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(25, 27);
            this.button1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(209, 44);
            this.button1.TabIndex = 0;
            this.button1.Text = "启用";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.btnLoadCompat_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(213, 86);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(82, 16);
            this.label3.TabIndex = 4;
            this.label3.Text = "渲染模式：";
            this.toolTip0.SetToolTip(this.label3, "选择渲染模式\r\ndx9on12：默认，通常情况下可提供最好的效果，窗口化会白屏\r\ndxvk：备用，可能会出现轨道缺失等问题");
            // 
            // statusStrip1
            // 
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel,
            this.statusProgress});
            this.statusStrip1.Location = new System.Drawing.Point(0, 645);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(1, 0, 19, 0);
            this.statusStrip1.Size = new System.Drawing.Size(1045, 26);
            this.statusStrip1.SizingGrip = false;
            this.statusStrip1.TabIndex = 13;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(41, 20);
            this.statusLabel.Text = "就绪";
            // 
            // statusProgress
            // 
            this.statusProgress.AutoSize = false;
            this.statusProgress.Name = "statusProgress";
            this.statusProgress.Size = new System.Drawing.Size(240, 18);
            this.statusProgress.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.statusProgress.Visible = false;
            // 
            // lblCurrentVersion
            // 
            this.lblCurrentVersion.AutoSize = true;
            this.lblCurrentVersion.Location = new System.Drawing.Point(20, 18);
            this.lblCurrentVersion.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblCurrentVersion.Name = "lblCurrentVersion";
            this.lblCurrentVersion.Size = new System.Drawing.Size(70, 16);
            this.lblCurrentVersion.TabIndex = 14;
            this.lblCurrentVersion.Text = "当前版本:";
            // 
            // txtCurrentVersion
            // 
            this.txtCurrentVersion.Location = new System.Drawing.Point(105, 15);
            this.txtCurrentVersion.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtCurrentVersion.Name = "txtCurrentVersion";
            this.txtCurrentVersion.ReadOnly = true;
            this.txtCurrentVersion.Size = new System.Drawing.Size(159, 22);
            this.txtCurrentVersion.TabIndex = 15;
            // 
            // lblRevision
            // 
            this.lblRevision.AutoSize = true;
            this.lblRevision.Location = new System.Drawing.Point(291, 18);
            this.lblRevision.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblRevision.Name = "lblRevision";
            this.lblRevision.Size = new System.Drawing.Size(100, 16);
            this.lblRevision.TabIndex = 16;
            this.lblRevision.Text = "懒人包修订号:";
            // 
            // txtRevision
            // 
            this.txtRevision.Location = new System.Drawing.Point(405, 15);
            this.txtRevision.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtRevision.Name = "txtRevision";
            this.txtRevision.ReadOnly = true;
            this.txtRevision.Size = new System.Drawing.Size(20, 22);
            this.txtRevision.TabIndex = 17;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(215, 92);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(97, 16);
            this.label4.TabIndex = 4;
            this.label4.Text = "服务器设定：";
            // 
            // btnManageServer
            // 
            this.btnManageServer.Location = new System.Drawing.Point(320, 89);
            this.btnManageServer.Margin = new System.Windows.Forms.Padding(4);
            this.btnManageServer.Name = "btnManageServer";
            this.btnManageServer.Size = new System.Drawing.Size(73, 28);
            this.btnManageServer.TabIndex = 13;
            this.btnManageServer.Text = "设定";
            this.btnManageServer.UseVisualStyleBackColor = true;
            this.btnManageServer.Click += new System.EventHandler(this.btnManageServer_Click);
            // 
            // BootstrapForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1045, 671);
            this.Controls.Add(this.lblCurrentVersion);
            this.Controls.Add(this.txtCurrentVersion);
            this.Controls.Add(this.lblRevision);
            this.Controls.Add(this.txtRevision);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.groupBoxCompatLayer);
            this.Controls.Add(this.txtLogOutput);
            this.Controls.Add(this.lblLogOutput);
            this.Controls.Add(this.groupBoxTools);
            this.Controls.Add(this.groupBoxOptions);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.MaximizeBox = false;
            this.Name = "BootstrapForm";
            this.Text = "SDVX 懒人包 启动程序";
            this.groupBoxOptions.ResumeLayout(false);
            this.groupBoxOptions.PerformLayout();
            this.groupBoxTools.ResumeLayout(false);
            this.groupBoxCompatLayer.ResumeLayout(false);
            this.groupBoxCompatLayer.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxOptions;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.CheckBox chkUsePreconfig;
        private System.Windows.Forms.CheckBox chkNoAsphyxia;
        private System.Windows.Forms.CheckBox chkWindowed;
        private System.Windows.Forms.Button btnSwitchRotation;
        private System.Windows.Forms.ComboBox cmbRotation;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBoxTools;
        private System.Windows.Forms.Button btnInstallRuntime;
        private System.Windows.Forms.Button btnEditConfig;
        private System.Windows.Forms.Button btnOpenLog;
        private System.Windows.Forms.Button btnClearCache;
        private System.Windows.Forms.Button btnAddFirewallRule;
        private System.Windows.Forms.Label lblLogOutput;
        private System.Windows.Forms.RichTextBox txtLogOutput;
        private System.Windows.Forms.GroupBox groupBoxCompatLayer;
        private System.Windows.Forms.Label lblCompatStatus;
        private System.Windows.Forms.Button btnUnloadCompat;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.CheckBox chkNoRestoreRotation;
        private System.Windows.Forms.Button btnKillProcesses;
        private System.Windows.Forms.ToolTip toolTip0;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label lblCurrentVersion;
        private System.Windows.Forms.TextBox txtCurrentVersion;
        private System.Windows.Forms.Label lblRevision;
        private System.Windows.Forms.TextBox txtRevision;
        private System.Windows.Forms.ComboBox cmbCompatType;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ToolStripProgressBar statusProgress;
        // 新增字段声明
        private System.Windows.Forms.Button btnAdvancedOptions;
        private System.Windows.Forms.Button btnAudioPanel;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnManageServer;
    }
}