# 导入更新

请在侧边栏开一个新栏目，叫做导入更新。界面与其他的差不多，左上角是标题，然后正中央是一个按钮，点击后提示用户选取压缩包。
如果你不知道怎么设计，请参考Libs/SukiUI/SukiUI.Demo的案例

具体功能如下：

1. 获取压缩包以后，检测标题里是否有"*-to-*"，"* to *", "*_to_*"，这个格式中，*都是日期，通常前面为一串完整日期，to的后面也是完整日期，前面代表当前所需版本，后面代表将更新至的版本。你获取以后需要通过SukiUI Dialog NotificationType Warning来提示用户这些信息，并且说明是否需要进行更新，并且用红字表明更新的风险
2. 获取到用户的提示后，调用我放在一起的7zip程序（我放置在SevenZip文件夹里，请publish的时候将复制并和启动器本体放在一起，帮我做一下），首先检测剩余空间是否够用，然后将压缩包解压到一个update_tmp文件夹里
3. 比对更新包内的文件夹和现有的文件夹，应当是一一对应的。

更新包应当会分为如下格式

- contents/data,contents/prop,contents/modules

或者干脆没有contents文件夹

- data, prop, modules

游戏文件夹内会是如下的：
├─asphyxia 
├─contents *必要
│  ├─data *必要
│  ├─data_mods
│  ├─dev
│  ├─lazy
│  ├─modules *必要
│  ├─patches
│  └─prop *必要
├─launcher
└─runtime

更新包内的文件夹是一定与当前游戏文件夹标注必要的一一对应的，如果给予的更新包内没有，则立刻终止更新，并使用SukiUI Dialog NotificationType Error弹出提示

4. 比对完成，确认没问题后，执行备份功能。需要备份以下内容

- asphyxia/savedata
- contents/card0.txt

以下内容备份需要在更新时由用户手动开关，你需要提示用户勾选这几项内容后备份内容占用空间会非常大

- 更新包内与当前游戏文件夹相同的文件
- asphyxia/plugins

4.1 手动开启备份这两项内容后，更新之后可以撤回更新，如果未开启则不能撤回

撤回时需要恢复以下内容：

- 删除更新包更新进去的所有内容，然后恢复备份的游戏文件
- 删除asphyxia/plugins，然后恢复asphyxia/plugins备份


5. 更新

检测上面更新包与游戏内容比对的结果，如果结果说没有异常，则执行更新，将文件复制到游戏文件夹并覆盖。其他未标记必要的都是可选内容，如果更新包内提供，则也需要复制到游戏文件夹中进行更新

然后是更新asphyxia，asphyxia是一个使用node.js开发的程序，用于游戏的本地服务器。如果检测到更新包内有asphyxia文件夹，则表示asphyxia进行了更新，需要进行重新获取游戏数据的步骤。他在WebUI里会调用update webui assets.pug和updateResources.js来执行步骤，我这里搞不太懂，请帮我研究一下能否在不访问WebUI，只启动后端服务器后直接进行更新，来去掉用户更新后需要手动进入WebUI更新的步骤，我已经提供文件了

6. 更新完成后

更新完成后，通过Toast说明更新已经完成，如果有异常则MessageBox通知