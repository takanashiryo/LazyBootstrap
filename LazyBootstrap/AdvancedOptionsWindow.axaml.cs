using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LazyBootstrap
{
    public partial class AdvancedOptionsWindow : Window
    {
        // 用于传递结果的标志
        public bool Confirmed { get; private set; } = false;

        public bool NetDumpEnabled
        {
            get { return chkNetDump.IsChecked == true; }
            set { chkNetDump.IsChecked = value; }
        }

        public bool AsphyxiaDebugEnabled
        {
            get { return chkAsphyxiaDebug.IsChecked == true; }
            set { chkAsphyxiaDebug.IsChecked = value; }
        }

        public bool PCoreOptimizationEnabled
        {
            get { return chkPCoreOptimization.IsChecked == true; }
            set { chkPCoreOptimization.IsChecked = value; }
        }

        public bool DisableSubDisplay
        {
            get { return chkDisableSubDisplay.IsChecked == true; }
            set { chkDisableSubDisplay.IsChecked = value; }
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
            get { return chkSubBorderless.IsChecked == true; }
            set { chkSubBorderless.IsChecked = value; }
        }

        public bool ShowCursorAndTouchSim
        {
            get { return chkShowCursorTouchSim.IsChecked == true; }
            set { chkShowCursorTouchSim.IsChecked = value; }
        }

        public AdvancedOptionsWindow()
        {
            InitializeComponent();
            if (cmbWindowMode.SelectedIndex < 0)
            {
                cmbWindowMode.SelectedIndex = 0;
            }
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
