# LazyBootstrap
LazyBootstrap是一个适用于街机游戏SOUND VOLTEX的辅助启动器，其目的旨在辅助第三方开源启动器spice2x，而不是替代。  
启动器大量依赖spice2x的功能，仅有小部分为独立功能

## 项目框架
项目基于C#语言开发，使用了Avalonia UI框架，采用实用型MVVM+部分code-behind设计模式，使用SukiUI主题库。虽然使用跨平台框架，但是仅Windows平台可用  
为了方便分发，最终使用Native AOT编译。

## 项目结构
- Models
- ViewModels
- Views
- Services
- Styles

## 库
- NAudio.Asio
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- Serilog

## 指南
AvaloniaUI Docs：https://docs.avaloniaui.net/
- 界面部分交给SukiUI管理，Avalinia文档仅作为规划和开发的参考，请勿直接使用AvaloniaUI的控件和样式
- 不用接入官方Dev Tools，因为不兼容
SukiUI Docs：https://kikipoulet.github.io/SukiUI/
- 可随时在SukiUI目录下查看源代码并纳入开发
- 由于SukiUI的文档较为简陋，新特性跟进很慢，建议直接查看源代码进行开发
- SukiUI本身为单页面项目，不适合进行拆分，故UI需保持单axaml开发，逻辑可拆分
- SukiUI自己本身管理一套系统，请避免使用AvaloniaUI的系统，以免导致冲突。

## 规则
1. 不使用任何单元测试模块，如xUnit，MSTest等
2. 回复内容时使用中文，但是代码文件内部全部使用英文，包括注释
3. 在Windows上，优先使用pwsh而不是powershell进行命令行操作