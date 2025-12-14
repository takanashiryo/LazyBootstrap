using System;
using System.Windows.Forms;

namespace LazyBootstrap
{
    public partial class AdvancedOptionsForm : Form
    {
        public bool NetDumpEnabled
        {
            get { return chkNetDump.Checked; }
            set { chkNetDump.Checked = value; }
        }

        public bool AsphyxiaDebugEnabled
        {
            get { return chkAsphyxiaDebug.Checked; }
            set { chkAsphyxiaDebug.Checked = value; }
        }

        public bool PCoreOptimizationEnabled
        {
            get { return chkPCoreOptimization.Checked; }
            set { chkPCoreOptimization.Checked = value; }
        }

        // 高级选项属性：与 Designer 中的控件同步
        public bool DisableSubDisplay
        {
            get { return chkDisableSubDisplay.Checked; }
            set { chkDisableSubDisplay.Checked = value; }
        }

        // 0: 默认, 1: 无边框, 2: 可变窗口
        public int WindowModeIndex
        {
            get { return cmbWindowMode.SelectedIndex; }
            set
            {
                if (value < 0 || value > 2) value = 0;
                cmbWindowMode.SelectedIndex = value;
            }
        }

        public bool SubBorderless
        {
            get { return chkSubBorderless.Checked; }
            set { chkSubBorderless.Checked = value; }
        }

        public bool ShowCursorAndTouchSim
        {
            get { return chkShowCursorTouchSim.Checked; }
            set { chkShowCursorTouchSim.Checked = value; }
        }

        public AdvancedOptionsForm()
        {
            InitializeComponent();
            // 初始化下拉默认索引，避免 -1 导致读取异常
            if (cmbWindowMode.SelectedIndex < 0)
            {
                cmbWindowMode.SelectedIndex = 0;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

    }
}
