import os
import time
from pathlib import Path

# __file__ 代表当前脚本文件的路径
# .resolve() 获取绝对路径
# .parent 代表当前脚本所在的文件夹
# .parent.parent 代表上一级文件夹
current_script = Path(__file__).resolve()
build_dir = current_script.parent.parent / "build"

file_path = build_dir / "config.toml"

print(f"正在尝试锁定文件: {file_path}")

if os.name == 'nt':  # Windows 平台
    import msvcrt
    # 以读写模式打开文件
    f = open(file_path, 'r+')
    # 锁定前 1000 个字节（通常足够覆盖整个配置文件）
    msvcrt.locking(f.fileno(), msvcrt.LK_NBLCK, 1000)
else:  # Linux / macOS 平台
    import fcntl
    f = open(file_path, 'r+')
    # LOCK_EX 表示独占锁，LOCK_NB 表示非阻塞（如果已被锁直接报错）
    fcntl.flock(f.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)

try:
    input("文件已成功独占锁定！按回车键释放锁并退出...")
finally:
    f.close()  # 关闭文件句柄会自动释放锁
    print("锁已释放。")