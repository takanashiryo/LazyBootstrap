# 配置文件调整

接下来请将当前的%AppData%/spicetools.xml认知为系统配置，在设定页面将导入最佳spice配置按钮改为“切换为系统配置”开关，仅当开启时会切换到这个路径。

否则，之后所有的编辑spicecfg按钮，启动游戏时的都替换为以下参数:
- 启动游戏：spice64.exe -cmdoverride -cfgpath lazy/spicetools.xml -patchcfgpath lazy/spicetools_patch_manager.json
- 编辑spicecfg：spice64.exe -cfg -cmdoverride -cfgpath lazy/spicetools.xml -patchcfgpath lazy/spicetools_patch_manager.json