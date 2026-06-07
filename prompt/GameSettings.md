# 游戏设定页面重构

游戏设定页面需要使用SukiUI：PropertyGrid进行重构，并将页面名称改为“设定”。图标改成齿轮或者扳手

## 要求

1. 页面最顶部为“编辑 spicecfg”按钮，使用Flat Button，按下后按钮进入Busy状态，无法被点击，等待spicecfg进程退出后恢复。Busy期间下方所有选项也无法被修改，被Busy Area覆盖。spicecfg退出后，恢复可操作状态并重新读取一次spicetools.xml(根据“使用预配置文件”功能的开启与否选择spicetools.xml所在的位置)与config.toml文件。如果检测不到上述的文件与程序，请使用SukiUI：Error Toast弹出提示
2. 将“使用预配置文件”放置在“编辑 spicecfg”按钮的下方，使用Toggle Switch
3. 将“不启动氧无”放置在“使用预配置文件”按钮的下方，使用Toggle Switch
4. 下方开始按照下述内容使用PropertyGrid布局
    - 服务器设定：从上往下依次是预设（下拉框选择，需要拥有可以新建自定义预设的功能，写入config.toml，格式为serverurl = "", pcbid = ""，以数组的方式保存，用户新建时弹出窗口，获取输入的预设名，服务器地址，与pcbid），服务器地址, PCBID。默认为无，不做任何更改，会生成一个默认预设，叫氧无，服务器地址为"http://localhost:8083",pcbid为空
    - 游戏：从上往下依次是禁用副屏（Toggle Switch），网络抓包NetDump（Toggle Switch），大小核优化（Toggle Switch），显示光标&触控模拟（Toggle Switch）
    - 图形：从上往下依次是窗口化启动（Toggle Switch），窗口化模式，窗口置顶（Toggle Switch），窗口大小（使用输入框，给与一个按下述格式填写的Watermark：1080,1920），仅使用一个显示适配器（Toggle Switch）
    - 图形（副屏）：从上往下依次是副屏无边框窗口化（Toggle Switch），窗口置顶（Toggle Switch），强制进行渲染（Toggle Switch），原生触控输入（Toggle Switch）
    - 音频：从上往下依次是ASIO驱动（使用输入框）
    - 杂项：从上往下依次是CardIO HID Reader支持（Toggle Switch），HID SmartCard支持（Toggle Switch）
5. AMD/Intel 显卡兼容层：使用Toggle Switch，去除启用与未启用的那个提示字。保留渲染模式的选择，但是从下拉栏换成使用SukiUI：Complex GigaChips。开启时无法选择渲染模式，关闭后可选。开启与关闭时使用Toast弹出提示，出现错误时使用Error Toast弹出提示，移除调用Log System输出日志
6. 在最下方写一行小字：更多功能请前往spicecfg调整


## 其他

出现现有的功能请直接迁移，如果出现全新的功能，请先完成UI，然后文字后面添加（待开发），不进行逻辑编写，我之后再让你写

Loading圈样式为Glow

