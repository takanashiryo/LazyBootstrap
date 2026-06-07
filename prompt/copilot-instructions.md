# Copilot Instructions for LazyBootstrap

## 大图景架构（先看这几处）
- 双可执行：`LazyBootstrap.Launcher/Program.cs` 启动 `launcher/LazyBootstrap.exe`，主程序在 `LazyBootstrap/`（Avalonia + SukiUI）。
- 根目录来源有优先级：`--basedir=` > `LAZYBOOTSTRAP_BASEDIR` > `AppDomain.CurrentDomain.BaseDirectory`，统一由 `Services/MainWindow/AppPathResolver.cs` 解析。
- 主程序入口 `LazyBootstrap/Program.cs` 会先做管理员权限检查并自提权重启；涉及进程重启/参数处理时必须保证参数透传。

## UI 与职责边界（项目特有）
- 当前不是 MVVM；`MainWindow` 采用 partial code-behind 分拆：UI 在 `UI/Main/MainWindow.axaml(.cs)`，业务逻辑在 `Services/MainWindow/*.cs`。
- 新功能优先就近放入既有 partial：
	- 启动链路：`Services/MainWindow/Launch.cs`
	- Spice XML 同步：`Services/MainWindow/SpiceConfig.cs`
	- TOML 设置：`Services/MainWindow/Settings.cs`
	- 服务器预设：`Services/MainWindow/ServerPresets.cs`
- 交互反馈统一使用 SukiUI Toast/Dialog（`ShowInfoToast`/`ShowErrorToast` + `_dialogManager`），不要引入第二套通知范式。

## 配置与数据流（高频改动区）
- `config.toml` 通过 `ConfigHandler` 做“文本级”读写与迁移，避免改成 JSON/强类型序列化。
- 启动初始化入口是 `AppConfigBootstrapper.InitializeAndMigrate(...)`：迁移 `Settings -> Setting`，并将显示相关键迁到 `[Display]`。
- 服务器预设持久化结构是 `[[Server.Presets]]` + `[Server].activepreset`，由 `ServerPresetStore` 统一维护。
- UI 与配置同步顺序：`LoadSettings()`（TOML）-> `LoadSpiceConfig()`（XML）；窗口 `Opened` 事件先 `RunEnvironmentScanAsync()` 后读 Spice 配置。

## Spice XML 写入约定（必须遵守）
- 生效配置位于 `spicetools.xml` 的 `Sound Voltex/options`。
- 写入必须复用 `UpdateSpiceConfig`、`OptionUpdate`、`TryGetSpiceOptionsContext`、`NormalizeSelfClosingTags`。
- 保持原始缩进/换行推断（`LoadOptions.PreserveWhitespace`）；不要引入新的 XML 序列化风格。
- `option@name` 是协议字段，禁止改名（示例：`url`、`p`、`w`、`sp2x-dx9on12`）。

## 启动/退出关键流程
- 启动顺序（`Launch.cs`）：校验 `spice64.exe`/可选 `asphyxia-core-x64.exe` -> 可选应用显示器设置 -> 可选启动 Asphyxia -> `UpdateSpiceConfig()` -> 启动游戏。
- 便携模式会附加 `-cfgpath/-patchcfgpath/-modules` 参数（由 `portablemode` 控制）。
- 退出处理：监控 `spice64` 重启、异常退出弹日志、结束 `asphyxia-core-x64`，并按保存状态恢复显示设置。

## 兼容层机制（高风险）
- 兼容层不是布尔标志，而是文件操作：`contents/lazy/stubs` 复制到 `contents/modules`。
- 基础文件：`nvcuda.dll`、`nvcuvid.dll`、`nvEncodeAPI64.dll`；`dxvk`/`dx9on12_external` 还会生成 `d3d9.dll`。
- 状态判定使用“文件存在 + config.toml 的 `compatlayerenabled`”双信号（见 `UpdateCompatLayerStatus` / `IsCompatLayerEffectivelyEnabled`）。

## 构建、发布、调试
- 本地构建：`dotnet build LazyBootstrap.sln`。
- 发布脚本：`build.bat` 分别 `dotnet publish` 两个 csproj 到临时目录，再重排产物到 `build/` 与 `build/launcher/`。
- 两个 csproj 都是 Release 条件启用 `PublishAot=true` + `PublishTrimmed=true`；新增依赖时注意 AOT/Trim 兼容。

## 平台与外部集成
- Windows-only 项目：注册表环境检测、显示器配置、`taskkill`、UAC 提权都在主流程里。
- 运行库安装依赖固定路径：`runtime/directx/DXSETUP.exe` 与 `runtime/vcredist/VisualCppRedist_AIO_x86_x64.exe`（`Tools.cs`）。
- UI 依赖来自 `Libs/SukiUI`，业务改动优先在 `LazyBootstrap` 与 `LazyBootstrap.Launcher`，避免改第三方库目录。
