using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using SukiUI.Controls;

namespace LazyBootstrap
{
    public partial class MainWindow
    {
        private Button ToggleLaunchLogButton => LaunchPageView.GetControl<Button>("ToggleLaunchLogButton");
        private Grid LaunchLogContainer => LaunchPageView.GetControl<Grid>("LaunchLogContainer");
        private ScrollViewer LaunchLogScrollViewer => LaunchPageView.GetControl<ScrollViewer>("LaunchLogScrollViewer");
        private TextBlock LogOutputTextBlock => LaunchPageView.GetControl<TextBlock>("LogOutputTextBlock");
        private Button GotoGameSettingsButton => LaunchPageView.GetControl<Button>("GotoGameSettingsButton");
        private Button OpenLogButton => LaunchPageView.GetControl<Button>("OpenLogButton");
        private Button KillProcessesButton => LaunchPageView.GetControl<Button>("KillProcessesButton");
        private SplitButton StartButton => LaunchPageView.GetControl<SplitButton>("StartButton");
        private MenuItem StartAsphyxiaDevMenuItem => LaunchPageView.GetControl<MenuItem>("StartAsphyxiaDevMenuItem");

        private BusyArea SettingsBusyArea => SettingsPageView.GetControl<BusyArea>("SettingsBusyArea");
        private SettingsLayout GameSettingsLayout => SettingsPageView.GetControl<SettingsLayout>("GameSettingsLayout");
        private Button EditConfigButton => SettingsPageView.GetControl<Button>("EditConfigButton");
        private Button ImportRecommendedSpiceConfigButton => SettingsPageView.GetControl<Button>("ImportRecommendedSpiceConfigButton");
        private ToggleSwitch NoAsphyxiaToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("NoAsphyxiaToggleSwitch");
        private ToggleSwitch CompatLayerToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("CompatLayerToggleSwitch");
        private BusyArea CompatRenderModeBusyArea => SettingsPageView.GetControl<BusyArea>("CompatRenderModeBusyArea");
        private RadioButton CompatDx9on12RadioButton => SettingsPageView.GetControl<RadioButton>("CompatDx9on12RadioButton");
        private RadioButton CompatDx9on12ExternalRadioButton => SettingsPageView.GetControl<RadioButton>("CompatDx9on12ExternalRadioButton");
        private RadioButton CompatDxvkRadioButton => SettingsPageView.GetControl<RadioButton>("CompatDxvkRadioButton");
        private TextBlock CompatStatusTextBlock => SettingsPageView.GetControl<TextBlock>("CompatStatusTextBlock");
        private ComboBox ServerPresetComboBox => SettingsPageView.GetControl<ComboBox>("ServerPresetComboBox");
        private Button AddServerPresetButton => SettingsPageView.GetControl<Button>("AddServerPresetButton");
        private Button DeleteServerPresetButton => SettingsPageView.GetControl<Button>("DeleteServerPresetButton");
        private TextBox ServerAddressTextBox => SettingsPageView.GetControl<TextBox>("ServerAddressTextBox");
        private TextBox PcbIdTextBox => SettingsPageView.GetControl<TextBox>("PcbIdTextBox");
        private ToggleSwitch AdvDisableSubDisplayToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("AdvDisableSubDisplayToggleSwitch");
        private ToggleSwitch AdvNetDumpToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("AdvNetDumpToggleSwitch");
        private ToggleSwitch AdvPCoreOptimizationToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("AdvPCoreOptimizationToggleSwitch");
        private ToggleSwitch AdvShowCursorTouchSimToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("AdvShowCursorTouchSimToggleSwitch");
        private ToggleSwitch WindowedToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("WindowedToggleSwitch");
        private ComboBox AdvWindowModeComboBox => SettingsPageView.GetControl<ComboBox>("AdvWindowModeComboBox");
        private ToggleSwitch AdvSubBorderlessToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("AdvSubBorderlessToggleSwitch");
        private ToggleSwitch AdvWindowTopMostToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("AdvWindowTopMostToggleSwitch");
        private TextBox AdvWindowSizeTextBox => SettingsPageView.GetControl<TextBox>("AdvWindowSizeTextBox");
        private ToggleSwitch AdvSingleAdapterToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("AdvSingleAdapterToggleSwitch");
        private ToggleSwitch AdvSubWindowTopMostToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("AdvSubWindowTopMostToggleSwitch");
        private ToggleSwitch AdvSubForceRenderToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("AdvSubForceRenderToggleSwitch");
        private ToggleSwitch AdvNativeTouchToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("AdvNativeTouchToggleSwitch");
        private TextBox AdvAsioDriverTextBox => SettingsPageView.GetControl<TextBox>("AdvAsioDriverTextBox");
        private ToggleSwitch AdvCardIoToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("AdvCardIoToggleSwitch");
        private ToggleSwitch AdvHidSmartCardToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("AdvHidSmartCardToggleSwitch");
        private ToggleSwitch PortableModeToggleSwitch => SettingsPageView.GetControl<ToggleSwitch>("PortableModeToggleSwitch");
        private Button LoadCompatButton => SettingsPageView.GetControl<Button>("LoadCompatButton");
        private Button UnloadCompatButton => SettingsPageView.GetControl<Button>("UnloadCompatButton");
        private ComboBox CompatTypeComboBox => SettingsPageView.GetControl<ComboBox>("CompatTypeComboBox");

        private ToggleSwitch DisplayConfigEnabledToggleSwitch => DisplayPageView.GetControl<ToggleSwitch>("DisplayConfigEnabledToggleSwitch");
        private ComboBox DisplayModeComboBox => DisplayPageView.GetControl<ComboBox>("DisplayModeComboBox");
        private BusyArea DisplayConfigDisabledMask => DisplayPageView.GetControl<BusyArea>("DisplayConfigDisabledMask");
        private Ellipse DotMainGlow => DisplayPageView.GetControl<Ellipse>("DotMainGlow");
        private Ellipse DotMainCore => DisplayPageView.GetControl<Ellipse>("DotMainCore");
        private Ellipse DotMainSelectedRing => DisplayPageView.GetControl<Ellipse>("DotMainSelectedRing");
        private Ellipse DotSubGlow => DisplayPageView.GetControl<Ellipse>("DotSubGlow");
        private Ellipse DotSubCore => DisplayPageView.GetControl<Ellipse>("DotSubCore");
        private Ellipse DotSubSelectedRing => DisplayPageView.GetControl<Ellipse>("DotSubSelectedRing");
        private Button SelectMainScreenAreaButton => DisplayPageView.GetControl<Button>("SelectMainScreenAreaButton");
        private Button SelectSubScreenAreaButton => DisplayPageView.GetControl<Button>("SelectSubScreenAreaButton");
        private ToggleSwitch ExitRestoreToggleSwitch => DisplayPageView.GetControl<ToggleSwitch>("ExitRestoreToggleSwitch");
        private Button TouchPanelButton => DisplayPageView.GetControl<Button>("TouchPanelButton");
        private StackPanel PanelNoScreenSelected => DisplayPageView.GetControl<StackPanel>("PanelNoScreenSelected");
        private StackPanel PanelMainScreenConfig => DisplayPageView.GetControl<StackPanel>("PanelMainScreenConfig");
        private StackPanel PanelSubScreenConfig => DisplayPageView.GetControl<StackPanel>("PanelSubScreenConfig");
        private TextBlock MainOutputInfoTextBlock => DisplayPageView.GetControl<TextBlock>("MainOutputInfoTextBlock");
        private ComboBox MainScreenComboBox => DisplayPageView.GetControl<ComboBox>("MainScreenComboBox");
        private ComboBox RotationComboBox => DisplayPageView.GetControl<ComboBox>("RotationComboBox");
        private ComboBox MainResolutionComboBox => DisplayPageView.GetControl<ComboBox>("MainResolutionComboBox");
        private ComboBox MainRefreshRateComboBox => DisplayPageView.GetControl<ComboBox>("MainRefreshRateComboBox");
        private TextBlock MainStartupInfoTextBlock => DisplayPageView.GetControl<TextBlock>("MainStartupInfoTextBlock");
        private TextBlock SubOutputInfoTextBlock => DisplayPageView.GetControl<TextBlock>("SubOutputInfoTextBlock");
        private ComboBox SubScreenComboBox => DisplayPageView.GetControl<ComboBox>("SubScreenComboBox");
        private ComboBox SubRotationComboBox => DisplayPageView.GetControl<ComboBox>("SubRotationComboBox");
        private ComboBox SubResolutionComboBox => DisplayPageView.GetControl<ComboBox>("SubResolutionComboBox");
        private ComboBox SubRefreshRateComboBox => DisplayPageView.GetControl<ComboBox>("SubRefreshRateComboBox");
        private TextBlock SubStartupInfoTextBlock => DisplayPageView.GetControl<TextBlock>("SubStartupInfoTextBlock");
        private Button PreviewDisplaySettingsButton => DisplayPageView.GetControl<Button>("PreviewDisplaySettingsButton");

        private Button ClearCacheButton => ToolsPageView.GetControl<Button>("ClearCacheButton");
        private Button AddFirewallRuleButton => ToolsPageView.GetControl<Button>("AddFirewallRuleButton");
        private Button AudioPanelButton => ToolsPageView.GetControl<Button>("AudioPanelButton");
        private Button InstallRuntimeButton => ToolsPageView.GetControl<Button>("InstallRuntimeButton");
        private Button SavedataBackupImportButton => ToolsPageView.GetControl<Button>("SavedataBackupImportButton");

        private TextBox CurrentVersionTextBox => InfoPageView.GetControl<TextBox>("CurrentVersionTextBox");
        private TextBox RevisionTextBox => InfoPageView.GetControl<TextBox>("RevisionTextBox");
        private TextBox LauncherVersionTextBox => InfoPageView.GetControl<TextBox>("LauncherVersionTextBox");
        private StackPanel PanelEnvScanResults => InfoPageView.GetControl<StackPanel>("PanelEnvScanResults");
    }
}
