# LazyBootstrap Architecture Refactor Plan

## Design Principles

### 1. Single Window Application

整个程序仅保留一个主窗口：

```text
MainWindow (SukiWindow)
```

页面切换通过导航完成。

禁止：

```text
SettingsWindow
UpdateWindow
LaunchWindow
```

等独立业务窗口。

---

### 2. Feature First

项目按照功能组织。

禁止：

```text
Views
ViewModels
Services
Models
```

这种按类型分类。

统一改为：

```text
Features
├── Launch
├── Update
├── Settings
├── Environment
├── Display
├── Dashboard
└── About
```

每个功能拥有自己的：

```text
View
Presenter
State
Services
Models
```

---

### 3. No Full MVVM

本项目不采用完整 MVVM。

原因：

* 业务以操作驱动为主
* 不是数据录入系统
* 不存在大量 DataGrid
* 不存在复杂数据绑定

采用：

```text
View
 ↓
Presenter
 ↓
Service
```

模式。

---

### 4. UI Logic vs Business Logic

UI相关代码允许存在于：

```text
*.axaml.cs
```

例如：

* Dialog
* Toast
* Theme
* Focus
* Keyboard
* Navigation

禁止：

```csharp
LaunchGame();
InstallUpdate();
ScanEnvironment();
```

出现在 View 中。

---

# Final Directory Structure

```text
src
│
├── Shell
│   ├── MainWindow.axaml
│   ├── MainWindow.axaml.cs
│   ├── NavigationService.cs
│   └── AppShellState.cs
│
├── Features
│
│   ├── Dashboard
│   │
│   │   ├── Views
│   │   ├── Presenters
│   │   ├── State
│   │   ├── Services
│   │   └── Models
│   │
│   ├── Launch
│   │
│   │   ├── Views
│   │   ├── Presenters
│   │   ├── State
│   │   ├── Services
│   │   └── Models
│   │
│   ├── Settings
│   │
│   │   ├── Views
│   │   ├── Presenters
│   │   ├── State
│   │   ├── Services
│   │   └── Models
│   │
│   ├── Update
│   │
│   │   ├── Views
│   │   ├── Presenters
│   │   ├── State
│   │   ├── Services
│   │   └── Models
│   │
│   ├── Environment
│   ├── Display
│   └── About
│
├── Infrastructure
│
│   ├── FileSystem
│   ├── Logging
│   ├── Process
│   ├── Serialization
│   ├── Platform
│   ├── Networking
│   └── DependencyInjection
│
└── Shared
    ├── Controls
    ├── Converters
    ├── Behaviors
    ├── Extensions
    └── Resources
```

---

# Service Layer Rules

统一废弃以下命名：

```text
*Helper
*Manager
*WorkflowService
*Utility
*Provider
*Store
```

改用明确职责名称。

---

## Layer 1 - Orchestrator

负责业务流程。

示例：

```text
LaunchOrchestrator
UpdateOrchestrator
SettingsOrchestrator
```

职责：

```text
用户动作入口
业务流程协调
调用多个 Worker
调用多个 Client
```

示例：

```csharp
UpdateOrchestrator
 ├── GithubReleaseClient
 ├── PackageDownloader
 ├── PackageInstaller
 └── PackageVerifier
```

---

## Layer 2 - Client

负责外部系统。

示例：

```text
GithubReleaseClient
DiscordWebhookClient
OpenListClient
```

职责：

```text
HTTP
REST API
外部资源访问
```

禁止包含业务逻辑。

---

## Layer 3 - Worker

负责具体操作。

示例：

```text
ZipExtractor
GameLauncher
DisplayConfigurator
RegistryEditor
DefenderConfigurator
```

职责：

```text
单一职责
可复用
可测试
```

---

# Infrastructure Rules

所有跨功能公共能力统一下沉。

---

## FileSystem

```text
FileSystemService
DirectoryService
FileWatcher
```

---

## Process

```text
ProcessRunner
ProcessMonitor
```

---

## Serialization

```text
JsonSerializer
YamlSerializer
```

---

## Networking

```text
HttpClientFactory
DownloadClient
```

---

## Logging

```text
ILogger
SerilogConfiguration
```

---

## Platform

```text
RegistryAccess
WindowsDefenderAccess
ShellIntegration
```

---

# Presenter Rules

每个 Feature 一个 Presenter。

例如：

```text
LaunchPresenter
SettingsPresenter
UpdatePresenter
```

职责：

```text
接收 View 事件
调用 Orchestrator
更新 State
```

禁止：

```text
文件操作
网络请求
注册表修改
```

---

# State Rules

每个 Feature 一个 State。

例如：

```text
LaunchState
SettingsState
UpdateState
```

职责：

```text
当前UI状态
当前运行状态
当前结果
```

示例：

```csharp
public sealed class UpdateState
{
    public bool IsCheckingUpdate { get; set; }

    public string CurrentVersion { get; set; }

    public string LatestVersion { get; set; }
}
```

---

# View Rules

View仅负责：

```text
布局
动画
Dialog
Toast
Theme
Focus
Keyboard
Navigation
```

允许：

```csharp
private async void OnCheckUpdateClick(...)
{
    await _presenter.CheckUpdateAsync();
}
```

禁止：

```csharp
private async void OnCheckUpdateClick(...)
{
    await GithubReleaseClient.GetLatestAsync();
}
```

---

# MainWindow Rules

MainWindow仅负责：

```text
Navigation
Theme
Toast
Dialog
SideMenu
```

禁止：

```text
Launch Logic
Settings Logic
Update Logic
Display Logic
Environment Logic
```

所有业务代码必须迁移至 Feature。

---

# Dependency Injection Rules

所有服务必须通过 DI 注入。

禁止：

```csharp
new UpdateOrchestrator()
new GithubReleaseClient()
```

统一：

```csharp
services.AddSingleton<>();
services.AddTransient<>();
```

管理。

---

# Migration Order

按照风险最低顺序迁移：

### Phase 1

建立新目录结构。

实现：

```text
Shell
Infrastructure
Features
```

---

### Phase 2

迁移：

```text
Settings
```

原因：

* 风险最低
* 依赖最少

---

### Phase 3

迁移：

```text
Launch
Display
```

---

### Phase 4

迁移：

```text
Environment
```

---

### Phase 5

迁移：

```text
Update
```

原因：

* 最复杂
* 依赖最多

---

### Phase 6

删除：

```text
WorkflowService
Manager
Helper
Utility
```

遗留实现。

---

# Expected End State

最终项目应满足：

```text
MainWindow
    ↓
Feature View
    ↓
Presenter
    ↓
Orchestrator
    ↓
Client / Worker
    ↓
Infrastructure
```

并且：

* 不存在 God Object
* 不存在超大 MainWindow
* 不存在 WorkflowService 混用
* 不存在 Helper 泛滥
* 功能按 Feature 完整封装
* 新功能可直接复制 Feature 模板扩展

这是我认为最适合 LazyBootstrap 当前规模、SukiUI 单窗口界面以及长期演进需求的最终架构方案。
