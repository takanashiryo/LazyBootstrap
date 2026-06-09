# LazyBootstrap
LazyBootstrap是一个适用于街机游戏SOUND VOLTEX的辅助启动器，其目的旨在辅助第三方开源启动器spice2x，而不是替代。  
启动器大量依赖spice2x的功能，仅有小部分为独立功能

## 项目框架
项目基于C#语言开发，使用了SukiUI主题库（其基于Avalonia UI搭建） ，项目虽然使用跨平台框架，但是本应用仅Windows平台可用  
主项目使用自分发，包含runtime，其他的分支项目使用Native AOT编译

## 项目结构
使用code-behind编写，SukiUI通过nuget引入

## 库
- NAudio.Asio
- Serilog

## 指南
SukiUI：https://github.com/kikipoulet/SukiUI
- 可随时查看源代码学习框架
- 由于SukiUI的文档较为简陋，新特性跟进很慢，建议直接查看源代码进行开发
- SukiUI本身为单页面项目，不适合进行拆分，故UI需保持单axaml开发，页面逻辑按照页面分离，避免过长单文件
- SukiUI自己本身管理一套系统，请避免使用AvaloniaUI的系统，以免导致冲突。

## 规则
1. 不使用任何单元测试模块，如xUnit，MSTest等
2. 回复内容时使用中文，但是代码文件内部全部使用英文，包括注释
3. 在Windows上，优先使用pwsh而不是powershell进行命令行操作