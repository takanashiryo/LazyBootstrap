# 导入推荐Spice2x配置

在设定页面，基础分类里添加一个“导入推荐spice2x配置”的功能。

## 功能逻辑

导入最佳spice2x配置的功能即修改spicetools.xml。将下述字段覆盖到options标签里，并清除原有的任何options

```xml
<option name="k" value="ifs_hook.dll"/>
<option name="sp2x-nvprofile" value="/ENABLED"/>
<option name="sp2x-lowlatencysharedaudio" value="/ENABLED"/>
<option name="sp2x-dx9on12" value="0"/>
<option name="url" value="http://localhost:8083"/>
<option name="sp2x-sdvxsubredraw" value="/ENABLED"/>
```

首先在%AppData%处检测spicetools.xml，如果存在则再检查里面是否有Sound Voltex子项，如果有，则按照现在处理option的方式，清除options里的所有内容，然后将option放置。

如果没有spicetools.xml，则直接拒绝导入，提示用户启动spicecfg重建配置文件再进行导入。便携模式如果启用则不显示这个功能

## 执行逻辑

当用户点击执行时，通过SukiUI NotificationType Warning，询问用户是否要执行，并提示“导入推荐spice2x配置会清除以下页面的现有配置并导入新配置：\n\n Options\nAdvanced\nDevelopment\n\n 你确定要执行吗？”，如果用户点击确认，则执行上述功能逻辑，完成后通过Toast提示。取消则返回