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
            this.lblLogOutput = new System.Windows.Forms.Label();
            this.txtLogOutput = new System.Windows.Forms.TextBox();
            this.groupBoxCompatLayer = new System.Windows.Forms.GroupBox();
            this.lblCompatStatus = new System.Windows.Forms.Label();
            this.btnUnloadCompat = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolTip0 = new System.Windows.Forms.ToolTip(this.components);
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
            this.groupBoxOptions.Location = new System.Drawing.Point(15, 34);
            this.groupBoxOptions.Name = "groupBoxOptions";
            this.groupBoxOptions.Size = new System.Drawing.Size(368, 195);
            this.groupBoxOptions.TabIndex = 8;
            this.groupBoxOptions.TabStop = false;
            this.groupBoxOptions.Text = "启动选项";
            // 
            // chkNoRestoreRotation
            // 
            this.chkNoRestoreRotation.AutoSize = true;
            this.chkNoRestoreRotation.Location = new System.Drawing.Point(163, 167);
            this.chkNoRestoreRotation.Name = "chkNoRestoreRotation";
            this.chkNoRestoreRotation.Size = new System.Drawing.Size(120, 16);
            this.chkNoRestoreRotation.TabIndex = 7;
            this.chkNoRestoreRotation.Text = "退出时不还原屏幕";
            this.toolTip0.SetToolTip(this.chkNoRestoreRotation, "退出游戏进程时保持当前的旋转方向");
            this.chkNoRestoreRotation.UseVisualStyleBackColor = true;
            // 
            // btnEditConfig
            // 
            this.btnEditConfig.Location = new System.Drawing.Point(19, 137);
            this.btnEditConfig.Name = "btnEditConfig";
            this.btnEditConfig.Size = new System.Drawing.Size(125, 43);
            this.btnEditConfig.TabIndex = 1;
            this.btnEditConfig.Text = "编辑 spicecfg";
            this.toolTip0.SetToolTip(this.btnEditConfig, "如果你勾选了“使用预配置文件”，则会编辑预配置文件");
            this.btnEditConfig.UseVisualStyleBackColor = true;
            this.btnEditConfig.Click += new System.EventHandler(this.btnEditConfig_Click);
            // 
            // btnSwitchRotation
            // 
            this.btnSwitchRotation.Location = new System.Drawing.Point(295, 137);
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
            this.cmbRotation.Location = new System.Drawing.Point(163, 140);
            this.cmbRotation.Name = "cmbRotation";
            this.cmbRotation.Size = new System.Drawing.Size(120, 20);
            this.cmbRotation.TabIndex = 5;
            this.toolTip0.SetToolTip(this.cmbRotation, "选择你希望屏幕旋转的角度（逆时针）");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(161, 75);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(65, 12);
            this.label2.TabIndex = 4;
            this.label2.Text = "调试选项：";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(161, 125);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(65, 12);
            this.label1.TabIndex = 4;
            this.label1.Text = "屏幕旋转：";
            // 
            // chkUsePreconfig
            // 
            this.chkUsePreconfig.AutoSize = true;
            this.chkUsePreconfig.Checked = true;
            this.chkUsePreconfig.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkUsePreconfig.Location = new System.Drawing.Point(19, 67);
            this.chkUsePreconfig.Name = "chkUsePreconfig";
            this.chkUsePreconfig.Size = new System.Drawing.Size(108, 16);
            this.chkUsePreconfig.TabIndex = 9;
            this.chkUsePreconfig.Text = "使用预配置文件";
            this.toolTip0.SetToolTip(this.chkUsePreconfig, "启用时将使用预先配置好的最优配置来启动游戏，以防止因错误的配置造成的干扰\n两者互相独立，取消勾选以使用系统内建配置");
            this.chkUsePreconfig.UseVisualStyleBackColor = true;
            this.chkUsePreconfig.CheckedChanged += new System.EventHandler(this.chkUsePreconfig_CheckedChanged);
            // 
            // chkPCoreOptimization
            // 
            this.chkPCoreOptimization.AutoSize = true;
            this.chkPCoreOptimization.Location = new System.Drawing.Point(253, 23);
            this.chkPCoreOptimization.Name = "chkPCoreOptimization";
            this.chkPCoreOptimization.Size = new System.Drawing.Size(84, 16);
            this.chkPCoreOptimization.TabIndex = 10;
            this.chkPCoreOptimization.Text = "大小核优化";
            this.toolTip0.SetToolTip(this.chkPCoreOptimization, "勾选此功能后，会将游戏限制在大核上运行");
            this.chkPCoreOptimization.UseVisualStyleBackColor = true;
            // 
            // chkAsphyxiaDebug
            // 
            this.chkAsphyxiaDebug.AutoSize = true;
            this.chkAsphyxiaDebug.Location = new System.Drawing.Point(253, 95);
            this.chkAsphyxiaDebug.Name = "chkAsphyxiaDebug";
            this.chkAsphyxiaDebug.Size = new System.Drawing.Size(96, 16);
            this.chkAsphyxiaDebug.TabIndex = 11;
            this.chkAsphyxiaDebug.Text = "调试启动氧无";
            this.toolTip0.SetToolTip(this.chkAsphyxiaDebug, "以调试模式启动 Asphyxia Core，用于输出错误日志");
            this.chkAsphyxiaDebug.UseVisualStyleBackColor = true;
            // 
            // chkNetDump
            // 
            this.chkNetDump.AutoSize = true;
            this.chkNetDump.Location = new System.Drawing.Point(163, 95);
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
            this.toolTip0.SetToolTip(this.chkNoAsphyxia, "启动时不启动氧无，连接在线服时可勾选");
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
            this.toolTip0.SetToolTip(this.chkWindowed, "以窗口化模式运行游戏");
            this.chkWindowed.UseVisualStyleBackColor = true;
            // 
            // btnStart
            // 
            this.btnStart.Font = new System.Drawing.Font("宋体", 9F);
            this.btnStart.Location = new System.Drawing.Point(17, 20);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(125, 41);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "启动";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnKillProcesses
            // 
            this.btnKillProcesses.Font = new System.Drawing.Font("宋体", 9F);
            this.btnKillProcesses.Location = new System.Drawing.Point(34, 296);
            this.btnKillProcesses.Name = "btnKillProcesses";
            this.btnKillProcesses.Size = new System.Drawing.Size(330, 27);
            this.btnKillProcesses.TabIndex = 8;
            this.btnKillProcesses.Text = "结束所有进程";
            this.toolTip0.SetToolTip(this.btnKillProcesses, "强制结束所有游戏相关进程");
            this.btnKillProcesses.UseVisualStyleBackColor = true;
            this.btnKillProcesses.Click += new System.EventHandler(this.btnKillProcesses_Click);
            // 
            // groupBoxTools
            // 
            this.groupBoxTools.Controls.Add(this.btnInstallRuntime);
            this.groupBoxTools.Controls.Add(this.btnClearCache);
            this.groupBoxTools.Location = new System.Drawing.Point(15, 235);
            this.groupBoxTools.Name = "groupBoxTools";
            this.groupBoxTools.Size = new System.Drawing.Size(368, 100);
            this.groupBoxTools.TabIndex = 9;
            this.groupBoxTools.TabStop = false;
            this.groupBoxTools.Text = "工具";
            // 
            // btnInstallRuntime
            // 
            this.btnInstallRuntime.Location = new System.Drawing.Point(192, 27);
            this.btnInstallRuntime.Name = "btnInstallRuntime";
            this.btnInstallRuntime.Size = new System.Drawing.Size(157, 28);
            this.btnInstallRuntime.TabIndex = 2;
            this.btnInstallRuntime.Text = "安装运行库";
            this.toolTip0.SetToolTip(this.btnInstallRuntime, "安装必要的游戏运行库");
            this.btnInstallRuntime.UseVisualStyleBackColor = true;
            this.btnInstallRuntime.Click += new System.EventHandler(this.btnInstallRuntime_Click);
            // 
            // btnClearCache
            // 
            this.btnClearCache.Location = new System.Drawing.Point(17, 27);
            this.btnClearCache.Name = "btnClearCache";
            this.btnClearCache.Size = new System.Drawing.Size(159, 28);
            this.btnClearCache.TabIndex = 0;
            this.btnClearCache.Text = "清除 data_mods 缓存";
            this.toolTip0.SetToolTip(this.btnClearCache, "一键清除data_mods的缓存文件\r\n更新游戏后需清除，以保证歌曲数据库正确读取");
            this.btnClearCache.UseVisualStyleBackColor = true;
            this.btnClearCache.Click += new System.EventHandler(this.btnClearCache_Click);
            // 
            // lblLogOutput
            // 
            this.lblLogOutput.AutoSize = true;
            this.lblLogOutput.Location = new System.Drawing.Point(392, 34);
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
            this.txtLogOutput.Location = new System.Drawing.Point(394, 54);
            this.txtLogOutput.Multiline = true;
            this.txtLogOutput.Name = "txtLogOutput";
            this.txtLogOutput.ReadOnly = true;
            this.txtLogOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLogOutput.Size = new System.Drawing.Size(378, 376);
            this.txtLogOutput.TabIndex = 12;
            // 
            // groupBoxCompatLayer
            // 
            this.groupBoxCompatLayer.Controls.Add(this.lblCompatStatus);
            this.groupBoxCompatLayer.Controls.Add(this.btnUnloadCompat);
            this.groupBoxCompatLayer.Controls.Add(this.button1);
            this.groupBoxCompatLayer.Location = new System.Drawing.Point(15, 341);
            this.groupBoxCompatLayer.Name = "groupBoxCompatLayer";
            this.groupBoxCompatLayer.Size = new System.Drawing.Size(368, 90);
            this.groupBoxCompatLayer.TabIndex = 10;
            this.groupBoxCompatLayer.TabStop = false;
            this.groupBoxCompatLayer.Text = "AMD/Intel 兼容层";
            this.toolTip0.SetToolTip(this.groupBoxCompatLayer, "载入AMD/Intel显卡的兼容层使其正确运行");
            this.groupBoxCompatLayer.Enter += new System.EventHandler(this.groupBoxCompatLayer_Enter);
            // 
            // lblCompatStatus
            // 
            this.lblCompatStatus.AutoSize = true;
            this.lblCompatStatus.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold);
            this.lblCompatStatus.ForeColor = System.Drawing.Color.Red;
            this.lblCompatStatus.Location = new System.Drawing.Point(17, 65);
            this.lblCompatStatus.Name = "lblCompatStatus";
            this.lblCompatStatus.Size = new System.Drawing.Size(64, 12);
            this.lblCompatStatus.TabIndex = 2;
            this.lblCompatStatus.Text = "● 未载入";
            // 
            // btnUnloadCompat
            // 
            this.btnUnloadCompat.Location = new System.Drawing.Point(192, 20);
            this.btnUnloadCompat.Name = "btnUnloadCompat";
            this.btnUnloadCompat.Size = new System.Drawing.Size(157, 33);
            this.btnUnloadCompat.TabIndex = 1;
            this.btnUnloadCompat.Text = "卸载";
            this.btnUnloadCompat.UseVisualStyleBackColor = true;
            this.btnUnloadCompat.Click += new System.EventHandler(this.btnUnloadCompat_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(19, 20);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(157, 33);
            this.button1.TabIndex = 0;
            this.button1.Text = "载入";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.btnLoadCompat_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip1.Location = new System.Drawing.Point(0, 442);
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
            // BootstrapForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 464);
            this.Controls.Add(this.btnKillProcesses);
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
        private System.Windows.Forms.Label lblLogOutput;
        private System.Windows.Forms.TextBox txtLogOutput;
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
    }
}