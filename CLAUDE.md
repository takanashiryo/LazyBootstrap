# LazyBootstrap
LazyBootstrap是一个适用于街机游戏SOUND VOLTEX的辅助启动器，其目的旨在辅助第三方开源启动器spice2x，而不是替代。  
启动器大量依赖spice2x的功能，仅有小部分为独立功能

## 项目框架
项目基于C#语言开发，使用了SukiUI主题库（其基于Avalonia UI搭建） ，项目虽然使用跨平台框架，但是本应用仅Windows平台可用  
主项目使用单文件自分发，包含runtime，其他的分支项目使用Native AOT编译

## 项目结构
采用 Feature-First 架构 + code-behind（不使用完整 MVVM）：
- `Shell/`：单窗口外壳（MainWindow、AppShellState、导航、Dialog/Toast）
- `Features/<功能>/{Views,Services,State,Models}`：功能自包含；View 为 UserControl 经 DI 构造注入依赖，业务流程在 `<功能>Orchestrator`
- `Infrastructure/`：跨功能基础设施（Paths/Processes/FileSystem/Platform/Serialization）
- `Shared/`：共享控件与扩展（Controls/Extensions）
- 所有服务经 DI 容器 `Services/ServiceRegistration.cs` 注册注入

## 库
- SukiUI，通过nuget引入，不使用本地包
- NAudio.Asio
- Serilog

## 指南
SukiUI：https://github.com/kikipoulet/SukiUI
- 可随时查看源代码学习框架
- 由于SukiUI的文档较为简陋，新特性跟进很慢，建议直接查看源代码进行开发
- SukiUI 为单窗口 Shell：主窗口（`Shell/MainWindow.axaml`）仅作外壳，各功能页拆为独立 UserControl（`Features/<功能>/Views/<功能>View.axaml`），由 `ContentControl` PageHost 托管；页面逻辑写在对应 View 的 code-behind，避免过长单文件
- SukiUI自己本身管理一套系统，请避免使用AvaloniaUI的系统，以免导致冲突。
- 源代码在“SukiUI”文件夹下，仅供开发时参考

## 规则
1. 不创建任何单元测试模块
2. 回复内容时请使用使用中文
3. 代码文件内部包括注释全部使用英文，展示给用户的UI界面需要使用中文
4. 在Windows上，优先使用pwsh而不是powershell进行命令行操作
5. 原子化搭建项目与开发