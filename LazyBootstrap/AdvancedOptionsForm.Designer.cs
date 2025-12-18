namespace LazyBootstrap
{
    partial class AdvancedOptionsForm
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

        private void InitializeComponent()
        {
            this.chkNetDump = new System.Windows.Forms.CheckBox();
            this.chkAsphyxiaDebug = new System.Windows.Forms.CheckBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.chkPCoreOptimization = new System.Windows.Forms.CheckBox();
            this.chkShowCursorTouchSim = new System.Windows.Forms.CheckBox();
            this.chkSubBorderless = new System.Windows.Forms.CheckBox();
            this.cmbWindowMode = new System.Windows.Forms.ComboBox();
            this.lblWindowMode = new System.Windows.Forms.Label();
            this.chkDisableSubDisplay = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // chkNetDump
            // 
            this.chkNetDump.AutoSize = true;
            this.chkNetDump.Location = new System.Drawing.Point(16, 29);
            this.chkNetDump.Name = "chkNetDump";
            this.chkNetDump.Size = new System.Drawing.Size(71, 17);
            this.chkNetDump.TabIndex = 0;
            this.chkNetDump.Text = "NetDump";
            this.chkNetDump.UseVisualStyleBackColor = true;
            // 
            // chkAsphyxiaDebug
            // 
            this.chkAsphyxiaDebug.AutoSize = true;
            this.chkAsphyxiaDebug.Location = new System.Drawing.Point(16, 52);
            this.chkAsphyxiaDebug.Name = "chkAsphyxiaDebug";
            this.chkAsphyxiaDebug.Size = new System.Drawing.Size(134, 17);
            this.chkAsphyxiaDebug.TabIndex = 1;
            this.chkAsphyxiaDebug.Text = "以调试模式启动氧无";
            this.chkAsphyxiaDebug.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(65, 315);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 2;
            this.btnOk.Text = "确定";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(146, 315);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.chkNetDump);
            this.groupBox1.Controls.Add(this.chkAsphyxiaDebug);
            this.groupBox1.Location = new System.Drawing.Point(15, 47);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(256, 88);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "调试选项";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ForeColor = System.Drawing.Color.Red;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(259, 26);
            this.label1.TabIndex = 5;
            this.label1.Text = "警告：\r\n在调整高级选项前请确认你了解每个选项的作用";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.chkPCoreOptimization);
            this.groupBox2.Controls.Add(this.chkShowCursorTouchSim);
            this.groupBox2.Controls.Add(this.chkSubBorderless);
            this.groupBox2.Controls.Add(this.cmbWindowMode);
            this.groupBox2.Controls.Add(this.lblWindowMode);
            this.groupBox2.Controls.Add(this.chkDisableSubDisplay);
            this.groupBox2.Location = new System.Drawing.Point(15, 150);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(256, 152);
            this.groupBox2.TabIndex = 6;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "高级选项";
            // 
            // chkPCoreOptimization
            // 
            this.chkPCoreOptimization.AutoSize = true;
            this.chkPCoreOptimization.Location = new System.Drawing.Point(16, 73);
            this.chkPCoreOptimization.Name = "chkPCoreOptimization";
            this.chkPCoreOptimization.Size = new System.Drawing.Size(86, 17);
            this.chkPCoreOptimization.TabIndex = 2;
            this.chkPCoreOptimization.Text = "大小核优化";
            this.chkPCoreOptimization.UseVisualStyleBackColor = true;
            // 
            // chkShowCursorTouchSim
            // 
            this.chkShowCursorTouchSim.AutoSize = true;
            this.chkShowCursorTouchSim.Location = new System.Drawing.Point(16, 119);
            this.chkShowCursorTouchSim.Name = "chkShowCursorTouchSim";
            this.chkShowCursorTouchSim.Size = new System.Drawing.Size(146, 17);
            this.chkShowCursorTouchSim.TabIndex = 4;
            this.chkShowCursorTouchSim.Text = "显示光标&启用触控模拟";
            this.chkShowCursorTouchSim.UseVisualStyleBackColor = true;
            // 
            // chkSubBorderless
            // 
            this.chkSubBorderless.AutoSize = true;
            this.chkSubBorderless.Location = new System.Drawing.Point(16, 96);
            this.chkSubBorderless.Name = "chkSubBorderless";
            this.chkSubBorderless.Size = new System.Drawing.Size(86, 17);
            this.chkSubBorderless.TabIndex = 3;
            this.chkSubBorderless.Text = "副屏无边框";
            this.chkSubBorderless.UseVisualStyleBackColor = true;
            // 
            // cmbWindowMode
            // 
            this.cmbWindowMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbWindowMode.FormattingEnabled = true;
            this.cmbWindowMode.Items.AddRange(new object[] {
            "默认",
            "无边框",
            "可变窗口"});
            this.cmbWindowMode.Location = new System.Drawing.Point(94, 44);
            this.cmbWindowMode.Name = "cmbWindowMode";
            this.cmbWindowMode.Size = new System.Drawing.Size(104, 21);
            this.cmbWindowMode.TabIndex = 1;
            // 
            // lblWindowMode
            // 
            this.lblWindowMode.AutoSize = true;
            this.lblWindowMode.Location = new System.Drawing.Point(14, 48);
            this.lblWindowMode.Name = "lblWindowMode";
            this.lblWindowMode.Size = new System.Drawing.Size(67, 13);
            this.lblWindowMode.TabIndex = 1;
            this.lblWindowMode.Text = "窗口化模式";
            // 
            // chkDisableSubDisplay
            // 
            this.chkDisableSubDisplay.AutoSize = true;
            this.chkDisableSubDisplay.Location = new System.Drawing.Point(16, 22);
            this.chkDisableSubDisplay.Name = "chkDisableSubDisplay";
            this.chkDisableSubDisplay.Size = new System.Drawing.Size(74, 17);
            this.chkDisableSubDisplay.TabIndex = 0;
            this.chkDisableSubDisplay.Text = "禁用副屏";
            this.chkDisableSubDisplay.UseVisualStyleBackColor = true;
            // 
            // AdvancedOptionsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(286, 366);
            this.ControlBox = false;
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AdvancedOptionsForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "高级选项";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.CheckBox chkNetDump;
        private System.Windows.Forms.CheckBox chkAsphyxiaDebug;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.CheckBox chkDisableSubDisplay;
        private System.Windows.Forms.Label lblWindowMode;
        private System.Windows.Forms.ComboBox cmbWindowMode;
        private System.Windows.Forms.CheckBox chkSubBorderless;
        private System.Windows.Forms.CheckBox chkShowCursorTouchSim;
        private System.Windows.Forms.CheckBox chkPCoreOptimization;
    }
}
