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
            this.btnEditConfig = new System.Windows.Forms.Button();
            this.btnSwitchRotation = new System.Windows.Forms.Button();
            this.cmbRotation = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.chkUsePreconfig = new System.Windows.Forms.CheckBox();
            this.chkPCoreOptimization = new System.Windows.Forms.CheckBox();
            this.chkAsphyxiaDebug = new System.Windows.Forms.CheckBox();
            this.chkNetDump = new System.Windows.Forms.CheckBox();
            this.chkNoAsphyxia = new System.Windows.Forms.CheckBox();
            this.chkWindowed = new System.Windows.Forms.CheckBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnKillProcesses = new System.Windows.Forms.Button();
            this.groupBoxTools = new System.Windows.Forms.GroupBox();
            this.btnInstallRuntime = new System.Windows.Forms.Button();
            this.btnClearCache = new System.Windows.Forms.Button();
            this.btnAddFirewallRule = new System.Windows.Forms.Button();
            this.lblLogOutput = new System.Windows.Forms.Label();
            this.txtLogOutput = new System.Windows.Forms.RichTextBox();
            this.groupBoxCompatLayer = new System.Windows.Forms.GroupBox();
            this.lblCompatStatus = new System.Windows.Forms.Label();
            this.btnUnloadCompat = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolTip0 = new System.Windows.Forms.ToolTip(this.components);
            this.lblCurrentVersion = new System.Windows.Forms.Label();
            this.txtCurrentVersion = new System.Windows.Forms.TextBox();
            this.lblRevision = new System.Windows.Forms.Label();
            this.txtRevision = new System.Windows.Forms.TextBox();
            this.groupBoxOptions.SuspendLayout();
            this.groupBoxTools.SuspendLayout();
            this.groupBoxCompatLayer.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxOptions
            // 
            this.groupBoxOptions.Controls.Add(this.chkNoRestoreRotation);
            this.groupBoxOptions.Controls.Add(this.btnEditConfig);
            this.groupBoxOptions.Controls.Add(this.btnSwitchRotation);
            this.groupBoxOptions.Controls.Add(this.cmbRotation);
            this.groupBoxOptions.Controls.Add(this.label2);
            this.groupBoxOptions.Controls.Add(this.label1);
            this.groupBoxOptions.Controls.Add(this.chkUsePreconfig);
            this.groupBoxOptions.Controls.Add(this.chkPCoreOptimization);
            this.groupBoxOptions.Controls.Add(this.chkAsphyxiaDebug);
            this.groupBoxOptions.Controls.Add(this.chkNetDump);
            this.groupBoxOptions.Controls.Add(this.chkNoAsphyxia);
            this.groupBoxOptions.Controls.Add(this.chkWindowed);
            this.groupBoxOptions.Controls.Add(this.btnStart);
            this.groupBoxOptions.Location = new System.Drawing.Point(15, 37);
            this.groupBoxOptions.Name = "groupBoxOptions";
            this.groupBoxOptions.Size = new System.Drawing.Size(368, 211);
            this.groupBoxOptions.TabIndex = 8;
            this.groupBoxOptions.TabStop = false;
            this.groupBoxOptions.Text = "启动选项";
            // 
            // chkNoRestoreRotation
            // 
            this.chkNoRestoreRotation.AutoSize = true;
            this.chkNoRestoreRotation.Location = new System.Drawing.Point(163, 181);
            this.chkNoRestoreRotation.Name = "chkNoRestoreRotation";
            this.chkNoRestoreRotation.Size = new System.Drawing.Size(122, 17);
            this.chkNoRestoreRotation.TabIndex = 7;
            this.chkNoRestoreRotation.Text = "退出时不还原屏幕";
            this.toolTip0.SetToolTip(this.chkNoRestoreRotation, "退出游戏进程时保持当前的旋转方向");
            this.chkNoRestoreRotation.UseVisualStyleBackColor = true;
            // 
            // btnEditConfig
            // 
            this.btnEditConfig.Location = new System.Drawing.Point(17, 152);
            this.btnEditConfig.Name = "btnEditConfig";
            this.btnEditConfig.Size = new System.Drawing.Size(125, 47);
            this.btnEditConfig.TabIndex = 1;
            this.btnEditConfig.Text = "编辑 spicecfg";
            this.toolTip0.SetToolTip(this.btnEditConfig, "如果你勾选了“使用预配置文件”，则会编辑预配置文件");
            this.btnEditConfig.UseVisualStyleBackColor = true;
            this.btnEditConfig.Click += new System.EventHandler(this.btnEditConfig_Click);
            // 
            // btnSwitchRotation
            // 
            this.btnSwitchRotation.Location = new System.Drawing.Point(295, 148);
            this.btnSwitchRotation.Name = "btnSwitchRotation";
            this.btnSwitchRotation.Size = new System.Drawing.Size(54, 25);
            this.btnSwitchRotation.TabIndex = 6;
            this.btnSwitchRotation.Text = "切换";
            this.btnSwitchRotation.UseVisualStyleBackColor = true;
            this.btnSwitchRotation.Click += new System.EventHandler(this.btnSwitchRotation_Click);
            // 
            // cmbRotation
            // 
            this.cmbRotation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRotation.FormattingEnabled = true;
            this.cmbRotation.Location = new System.Drawing.Point(163, 152);
            this.cmbRotation.Name = "cmbRotation";
            this.cmbRotation.Size = new System.Drawing.Size(120, 21);
            this.cmbRotation.TabIndex = 5;
            this.toolTip0.SetToolTip(this.cmbRotation, "选择你希望屏幕旋转的角度（逆时针）");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(161, 81);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(67, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "调试选项：";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(161, 135);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(67, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "屏幕旋转：";
            // 
            // chkUsePreconfig
            // 
            this.chkUsePreconfig.AutoSize = true;
            this.chkUsePreconfig.Checked = true;
            this.chkUsePreconfig.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkUsePreconfig.Location = new System.Drawing.Point(19, 103);
            this.chkUsePreconfig.Name = "chkUsePreconfig";
            this.chkUsePreconfig.Size = new System.Drawing.Size(110, 17);
            this.chkUsePreconfig.TabIndex = 9;
            this.chkUsePreconfig.Text = "使用预配置文件";
            this.toolTip0.SetToolTip(this.chkUsePreconfig, "使用预先配置好的最优配置文件来启动游戏，以防止因错误的系统内建配置造成的干扰\r\n两者互相独立，取消勾选以使用系统内建配置");
            this.chkUsePreconfig.UseVisualStyleBackColor = true;
            this.chkUsePreconfig.CheckedChanged += new System.EventHandler(this.chkUsePreconfig_CheckedChanged);
            // 
            // chkPCoreOptimization
            // 
            this.chkPCoreOptimization.AutoSize = true;
            this.chkPCoreOptimization.Location = new System.Drawing.Point(253, 25);
            this.chkPCoreOptimization.Name = "chkPCoreOptimization";
            this.chkPCoreOptimization.Size = new System.Drawing.Size(86, 17);
            this.chkPCoreOptimization.TabIndex = 10;
            this.chkPCoreOptimization.Text = "大小核优化";
            this.toolTip0.SetToolTip(this.chkPCoreOptimization, "勾选后，会限制游戏在性能核心上运行");
            this.chkPCoreOptimization.UseVisualStyleBackColor = true;
            // 
            // chkAsphyxiaDebug
            // 
            this.chkAsphyxiaDebug.AutoSize = true;
            this.chkAsphyxiaDebug.Location = new System.Drawing.Point(253, 103);
            this.chkAsphyxiaDebug.Name = "chkAsphyxiaDebug";
            this.chkAsphyxiaDebug.Size = new System.Drawing.Size(98, 17);
            this.chkAsphyxiaDebug.TabIndex = 11;
            this.chkAsphyxiaDebug.Text = "调试启动氧无";
            this.toolTip0.SetToolTip(this.chkAsphyxiaDebug, "以调试模式启动 Asphyxia Core，可输出详细错误日志");
            this.chkAsphyxiaDebug.UseVisualStyleBackColor = true;
            // 
            // chkNetDump
            // 
            this.chkNetDump.AutoSize = true;
            this.chkNetDump.Location = new System.Drawing.Point(163, 103);
            this.chkNetDump.Name = "chkNetDump";
            this.chkNetDump.Size = new System.Drawing.Size(71, 17);
            this.chkNetDump.TabIndex = 3;
            this.chkNetDump.Text = "NetDump";
            this.chkNetDump.UseVisualStyleBackColor = true;
            // 
            // chkNoAsphyxia
            // 
            this.chkNoAsphyxia.AutoSize = true;
            this.chkNoAsphyxia.Location = new System.Drawing.Point(163, 49);
            this.chkNoAsphyxia.Name = "chkNoAsphyxia";
            this.chkNoAsphyxia.Size = new System.Drawing.Size(86, 17);
            this.chkNoAsphyxia.TabIndex = 2;
            this.chkNoAsphyxia.Text = "不启动氧无";
            this.toolTip0.SetToolTip(this.chkNoAsphyxia, "启动时不启动氧无，连接在线服时可勾选");
            this.chkNoAsphyxia.UseVisualStyleBackColor = true;
            // 
            // chkWindowed
            // 
            this.chkWindowed.AutoSize = true;
            this.chkWindowed.Location = new System.Drawing.Point(163, 25);
            this.chkWindowed.Name = "chkWindowed";
            this.chkWindowed.Size = new System.Drawing.Size(86, 17);
            this.chkWindowed.TabIndex = 1;
            this.chkWindowed.Text = "窗口化启动";
            this.toolTip0.SetToolTip(this.chkWindowed, "以窗口化模式运行游戏");
            this.chkWindowed.UseVisualStyleBackColor = true;
            // 
            // btnStart
            // 
            this.btnStart.Font = new System.Drawing.Font("宋体", 10F);
            this.btnStart.Location = new System.Drawing.Point(17, 22);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(125, 73);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "启动游戏";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnKillProcesses
            // 
            this.btnKillProcesses.Font = new System.Drawing.Font("宋体", 9F);
            this.btnKillProcesses.Location = new System.Drawing.Point(19, 98);
            this.btnKillProcesses.Name = "btnKillProcesses";
            this.btnKillProcesses.Size = new System.Drawing.Size(334, 32);
            this.btnKillProcesses.TabIndex = 8;
            this.btnKillProcesses.Text = "结束所有进程";
            this.toolTip0.SetToolTip(this.btnKillProcesses, "强制结束所有游戏相关进程");
            this.btnKillProcesses.UseVisualStyleBackColor = true;
            this.btnKillProcesses.Click += new System.EventHandler(this.btnKillProcesses_Click);
            // 
            // groupBoxTools
            // 
            this.groupBoxTools.Controls.Add(this.btnKillProcesses);
            this.groupBoxTools.Controls.Add(this.btnInstallRuntime);
            this.groupBoxTools.Controls.Add(this.btnClearCache);
            this.groupBoxTools.Controls.Add(this.btnAddFirewallRule);
            this.groupBoxTools.Location = new System.Drawing.Point(15, 255);
            this.groupBoxTools.Name = "groupBoxTools";
            this.groupBoxTools.Size = new System.Drawing.Size(368, 152);
            this.groupBoxTools.TabIndex = 9;
            this.groupBoxTools.TabStop = false;
            this.groupBoxTools.Text = "工具";
            // 
            // btnInstallRuntime
            // 
            this.btnInstallRuntime.Location = new System.Drawing.Point(19, 60);
            this.btnInstallRuntime.Name = "btnInstallRuntime";
            this.btnInstallRuntime.Size = new System.Drawing.Size(334, 32);
            this.btnInstallRuntime.TabIndex = 1;
            this.btnInstallRuntime.Text = "安装运行库";
            this.toolTip0.SetToolTip(this.btnInstallRuntime, "安装必要的游戏运行库");
            this.btnInstallRuntime.UseVisualStyleBackColor = true;
            this.btnInstallRuntime.Click += new System.EventHandler(this.btnInstallRuntime_Click);
            // 
            // btnClearCache
            // 
            this.btnClearCache.Location = new System.Drawing.Point(19, 22);
            this.btnClearCache.Name = "btnClearCache";
            this.btnClearCache.Size = new System.Drawing.Size(157, 32);
            this.btnClearCache.TabIndex = 0;
            this.btnClearCache.Text = "清除 data_mods 缓存";
            this.toolTip0.SetToolTip(this.btnClearCache, "清除data_mods缓存文件\r\n更新游戏后需清除，以重建歌曲类模组数据库");
            this.btnClearCache.UseVisualStyleBackColor = true;
            this.btnClearCache.Click += new System.EventHandler(this.btnClearCache_Click);
            // 
            // btnAddFirewallRule
            // 
            this.btnAddFirewallRule.Location = new System.Drawing.Point(192, 22);
            this.btnAddFirewallRule.Name = "btnAddFirewallRule";
            this.btnAddFirewallRule.Size = new System.Drawing.Size(161, 32);
            this.btnAddFirewallRule.TabIndex = 2;
            this.btnAddFirewallRule.Text = "添加防火墙规则";
            this.toolTip0.SetToolTip(this.btnAddFirewallRule, "添加允许游戏通过防火墙的规则");
            this.btnAddFirewallRule.UseVisualStyleBackColor = true;
            this.btnAddFirewallRule.Click += new System.EventHandler(this.btnAddFirewallRule_Click);
            // 
            // lblLogOutput
            // 
            this.lblLogOutput.AutoSize = true;
            this.lblLogOutput.Location = new System.Drawing.Point(392, 37);
            this.lblLogOutput.Name = "lblLogOutput";
            this.lblLogOutput.Size = new System.Drawing.Size(67, 13);
            this.lblLogOutput.TabIndex = 11;
            this.lblLogOutput.Text = "日志输出：";
            // 
            // txtLogOutput
            // 
            this.txtLogOutput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLogOutput.Location = new System.Drawing.Point(394, 59);
            this.txtLogOutput.Name = "txtLogOutput";
            this.txtLogOutput.ReadOnly = true;
            this.txtLogOutput.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.txtLogOutput.Size = new System.Drawing.Size(378, 449);
            this.txtLogOutput.TabIndex = 12;
            this.txtLogOutput.Text = "";
            // 
            // groupBoxCompatLayer
            // 
            this.groupBoxCompatLayer.Controls.Add(this.lblCompatStatus);
            this.groupBoxCompatLayer.Controls.Add(this.btnUnloadCompat);
            this.groupBoxCompatLayer.Controls.Add(this.button1);
            this.groupBoxCompatLayer.Location = new System.Drawing.Point(15, 413);
            this.groupBoxCompatLayer.Name = "groupBoxCompatLayer";
            this.groupBoxCompatLayer.Size = new System.Drawing.Size(368, 98);
            this.groupBoxCompatLayer.TabIndex = 10;
            this.groupBoxCompatLayer.TabStop = false;
            this.groupBoxCompatLayer.Text = "AMD/Intel显卡兼容层";
            this.toolTip0.SetToolTip(this.groupBoxCompatLayer, "载入AMD/Intel显卡的兼容层");
            // 
            // lblCompatStatus
            // 
            this.lblCompatStatus.AutoSize = true;
            this.lblCompatStatus.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold);
            this.lblCompatStatus.ForeColor = System.Drawing.Color.Red;
            this.lblCompatStatus.Location = new System.Drawing.Point(17, 70);
            this.lblCompatStatus.Name = "lblCompatStatus";
            this.lblCompatStatus.Size = new System.Drawing.Size(64, 12);
            this.lblCompatStatus.TabIndex = 2;
            this.lblCompatStatus.Text = "● 未启用";
            // 
            // btnUnloadCompat
            // 
            this.btnUnloadCompat.Location = new System.Drawing.Point(192, 22);
            this.btnUnloadCompat.Name = "btnUnloadCompat";
            this.btnUnloadCompat.Size = new System.Drawing.Size(157, 36);
            this.btnUnloadCompat.TabIndex = 1;
            this.btnUnloadCompat.Text = "关闭";
            this.btnUnloadCompat.UseVisualStyleBackColor = true;
            this.btnUnloadCompat.Click += new System.EventHandler(this.btnUnloadCompat_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(19, 22);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(157, 36);
            this.button1.TabIndex = 0;
            this.button1.Text = "启用";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.btnLoadCompat_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip1.Location = new System.Drawing.Point(0, 523);
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
            // lblCurrentVersion
            // 
            this.lblCurrentVersion.AutoSize = true;
            this.lblCurrentVersion.Location = new System.Drawing.Point(15, 15);
            this.lblCurrentVersion.Name = "lblCurrentVersion";
            this.lblCurrentVersion.Size = new System.Drawing.Size(58, 13);
            this.lblCurrentVersion.TabIndex = 14;
            this.lblCurrentVersion.Text = "当前版本:";
            // 
            // txtCurrentVersion
            // 
            this.txtCurrentVersion.Location = new System.Drawing.Point(79, 12);
            this.txtCurrentVersion.Name = "txtCurrentVersion";
            this.txtCurrentVersion.ReadOnly = true;
            this.txtCurrentVersion.Size = new System.Drawing.Size(78, 20);
            this.txtCurrentVersion.TabIndex = 15;
            this.txtCurrentVersion.Text = "读取中...";
            // 
            // lblRevision
            // 
            this.lblRevision.AutoSize = true;
            this.lblRevision.Location = new System.Drawing.Point(182, 15);
            this.lblRevision.Name = "lblRevision";
            this.lblRevision.Size = new System.Drawing.Size(82, 13);
            this.lblRevision.TabIndex = 16;
            this.lblRevision.Text = "懒人包修订号:";
            // 
            // txtRevision
            // 
            this.txtRevision.Location = new System.Drawing.Point(268, 12);
            this.txtRevision.Name = "txtRevision";
            this.txtRevision.ReadOnly = true;
            this.txtRevision.Size = new System.Drawing.Size(54, 20);
            this.txtRevision.TabIndex = 17;
            this.txtRevision.Text = "读取中...";
            // 
            // BootstrapForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 545);
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
        private System.Windows.Forms.CheckBox chkPCoreOptimization;
        private System.Windows.Forms.CheckBox chkAsphyxiaDebug;
        private System.Windows.Forms.CheckBox chkNetDump;
        private System.Windows.Forms.CheckBox chkNoAsphyxia;
        private System.Windows.Forms.CheckBox chkWindowed;
        private System.Windows.Forms.Button btnSwitchRotation;
        private System.Windows.Forms.ComboBox cmbRotation;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBoxTools;
        private System.Windows.Forms.Button btnInstallRuntime;
        private System.Windows.Forms.Button btnEditConfig;
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
    }
}