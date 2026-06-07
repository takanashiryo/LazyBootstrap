# 改进更新功能

创建一个新的子项目LazyBootstrap.MediaUpdater，将bat的前置步骤以及清理的部分全部交给这个Updater处理，使用wpf，最基本的
GUI，上方是进度条，下方是文字，没有其他内容。直接打开此程序将没有任何用，需要通过启动器唤起。

bat改名为sync.bat，内部仅有robocopy用来复制，并且bat直接启动会提示并直接结束来防止误操作。
