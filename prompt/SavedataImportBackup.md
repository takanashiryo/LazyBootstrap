# 存档备份与导入

在工具页面里添加一个新功能，可以备份与导入存档数据。点击执行以后弹出一个新窗口来处理内容。

## 功能

在新窗口里左侧是备份按钮，右侧是导入按钮，用户点击对应的按钮后弹出窗口让用户选择文件

备份：

当选择备份功能时，调用LazyBootstrap.exe（不是Launcher）旁的7z文件，备份下列文件夹/文件到savedata_backup文件夹

- asphyxia/savedata
- asphyxia/config.ini
- contents/card0.txt
- contents/card1.txt  

没有的内容跳过即可

恢复：

首先检测上面备份里说明的几个文件夹是否存在，如果不存在就直接导入。如果存在，则通过SukiUI Dialog NotificationType Warning提示用户是否要直接覆盖已有文件

恢复时按照原始的样子原样恢复，不能嵌套多余的文件夹，也不能少