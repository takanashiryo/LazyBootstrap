# 游戏设定界面调整2

## 程序逻辑

重写当前的检测逻辑，首先检测“使用预配置文件”选项是否开启，如果开启，则读取contents\lazy\spicetools.xml修改；如果没有开启，则读取%AppData%\spicetools.xml修改。默认关闭

开启“使用预配置文件”需要检测是否存在lazy\spicetools.xml，若不存在，则无法开启，按钮自动回弹，并Error Toast弹窗。

## 编写待开发的功能

请按照现有的XML修改逻辑，编写下列功能
- 窗口置顶（图形设定）：option name="sp2x-windowalwaysontop"，value为""为关闭，"/ENABLED"为开启
- 窗口大小：option name="sp2x-windowsize"，value为""为关闭，输入内容为开启
- 仅使用一个显示适配器：option name="graphics-force-single-adapter"，value为""为关闭，"/ENABLED"为开启。修改此项在UI显示的名字为“使用单一显示适配器”
- 窗口置顶（图形（副屏））：option name="sdvxwsubtop"，value为""为关闭，"/ENABLED"为开启
- 强制进行渲染：option name="sp2x-sdvxsubredraw"，value为""为关闭，"/ENABLED"为开启
- 原生触控输入：option name="sdvxnativetouch"，value为""为关闭，"/ENABLED"为开启
- ASIO驱动：option name="sp2x-sdvxasio"，value为""为关闭，输入内容为开启
- CardIO HID Reader支持：option name="cardio"，value为""为关闭，"/ENABLED"为开启
- HID SmartCard支持：option name="scard"，value为""为关闭，"/ENABLED"为开启
- 网络抓包（NetDump）：将此功能迁移到修改XML，option name="netdump"，value为""为关闭，"/ENABLED"为开启

修改下列功能：
- 兼容层：默认关闭，渲染模式在现有的基础上再增加一个dx9on12 (External)，描述为“外置版，可能比内置的好”，若选择此模式开启兼容层，需在contents\lazy\stubs目录，获取nvcuda.dll，nvcuvid.dll，nvEncodeAPI64.dll，d3d9.dll.dx9on12，复制4个文件并移动到contents\modules，然后将d3d9.dll.dx9on12重命名为d3d9.dll，如果存在则覆盖，并修改spicetools.xml(需根据预配置文件开关决定修改哪个文件)，option name="sp2x-dx9on12"，value为"0"，关闭兼容层时删除modules内上述4个文件。若选择dx9on12，则在contents\lazy\stubs目录，获取nvcuda.dll，nvcuvid.dll，nvEncodeAPI64.dll，复制3个文件并移动到contents\modules，如果存在则覆盖，并修改spicetools.xml(需根据预配置文件开关决定修改哪个文件)，option name="sp2x-dx9on12"，value为"1"，关闭兼容层时删除modules内上述3个文件，dx9on12选项的描述修改为“spice2x内置，最常用的模式”。若选择dxvk，则在contents\lazy\stubs目录，获取nvcuda.dll，nvcuvid.dll，nvEncodeAPI64.dll，d3d9.dll.dxvk，复制4个文件并移动到contents\modules，然后将d3d9.dll.dxvk重命名为d3d9.dll，如果存在则覆盖，并修改spicetools.xml(需根据预配置文件开关决定修改哪个文件)，option name="sp2x-dx9on12"，value为"0"。config.toml里需要记录当前选择的模式与开关状态

## 界面优化

使用Header文字做分类（应当是上方是分类的标题，下方一个分割线，在往下是内容），例如服务器设定，游戏设定，图形设定，图形（副屏），音频，杂项，调试

## 功能提示

在部分功能的下方添加小字，说明功能
- 窗口化启动：开启时副屏也会窗口化
- 大小核优化：针对 Intel第12代酷睿 及之后的处理器
- 使用单一显示适配器：针对笔记本等独显核显混合输出的设备，开启后仅连接主屏所属的显卡
- 强制进行渲染：如果遇到副屏黑屏，闪烁，一帧一帧显示等问题，可尝试开启