# Meta Cue for Windows

原生 WinForms 托盘程序，目标是 Windows 11 的原生 Explorer 底部单行任务栏。长按左或右 Win（默认 300 ms），在主任务栏前十个应用按钮上显示 1～9、0；第十个应用对应 Win+0，松开或按其他键隐藏。Win+数字仍使用 Windows 原有切换；新增可关闭的 Win+Shift+数字扩展切换功能，见下文。无主窗口，不修改系统任务栏文件或快捷键注册表，不自动设置登录启动。

## Win+Shift 扩展切换（试用）

已授权接管 Win+Shift+数字的原生“新开实例”行为。功能默认启用，可通过托盘取消勾选“Win+Shift+数字切换第 11～20 项”，立即恢复这组原生快捷键。

| 按键 | 目标 |
| --- | --- |
| Win+Shift+1～9 | 主任务栏第 11～19 个应用 |
| Win+Shift+0 | 主任务栏第 20 个应用 |

1. 按住 Win 和 Shift（先后顺序不限），识别完成后第 11～20 个应用会显示 1～9、0。
2. 点按数字，显示候选窗口的标题及序号。保持 Win+Shift 按住，再次点按同一数字轮选下一个窗口；到末尾后循环。按住数字造成的自动重复不会跳选。
3. 换一个数字会从新应用的第一个候选开始。候选列表在本次手势中固定，不随前台顺序变化而跳动。
4. **Win 和 Shift 都松开后确认**，激活当前候选；只有一个窗口也在松键时确认。Esc 取消；其余不相关按键取消选择。Ctrl/Alt 组合交还系统。
5. 目标位置不存在则不执行任何操作。任务栏重排、目标关闭/改变分组或识别超时则取消；识别尚未完成就松键也取消，避免延迟切换到意外窗口。

匹配优先使用窗口/进程的 AppUserModelID，再使用只读快捷方式属性及真实目标路径；MSI 快捷方式和短路径会解析。不会根据应用显示名猜测分组。同一可执行文件对应多个任务栏标识时，缺少窗口级标识的窗口不会被猜分到其中一组。

**后备行为：** 没有可靠匹配的窗口时，提示“无法可靠枚举窗口”，松键后调用该位置的原生任务栏按钮。它可能打开应用或显示系统的窗口缩略图；此时不能保证按数字轮选。这与匹配到窗口时的标题选择框不同，不会假装已经识别出窗口。

当前界面是标题/序号选择框，不是系统缩略图的完整复制。激活使用 Windows 前台窗口 API；被系统拒绝时会通知，不强制附加其他应用的输入线程。窗口标题仅在选择界面中使用，不写入诊断文件。

本次桌面只读验证：16 个任务栏应用中，第 11 项 Typora 匹配 1 个窗口，第 13/14 项 Capture/Allegro 各 1 个，第 15 项 SolidWorks 1 个，第 16 项 Excel 5 个。Notion 有两个指向同一程序的任务栏标识，使用后备方式。

**验证边界：** 手势、按键配对、轮选、明确分组/歧义拒绝及原生窗口枚举测试已通过。测试桌面没有可用前台窗口（`GetForegroundWindow()==0`），前台恢复/激活检查被明确跳过；真实 Win+Shift 连续按键、鼠标穿透、管理员窗口和跨虚拟桌面仍待验收。无需为此将程序常驻提权。

试用包可独立发布，避免覆盖运行中的旧程序：

```powershell
./windows/build.ps1 -OutputDirectory ./windows/artifacts/win-x64-extended
```

先从托盘退出旧版，再启动该目录中的 `MetaCue.exe`。不自动替换正在运行的进程。

## 构建与运行

安装 .NET 9 SDK 后，在仓库根目录执行：

```powershell
./windows/build.ps1
./windows/artifacts/win-x64/MetaCue.exe
```

默认产物需 .NET 9 Windows Desktop Runtime；使用 `./windows/build.ps1 -SelfContained` 可生成包含运行时的便携目录。复制整个 `windows/artifacts/win-x64` 目录，不要只复制 exe。无需管理员权限。不要同时运行两个版本；单实例锁会让后启动的实例退出。

托盘右键提供预览两秒、启用/停用、重新读取延迟配置和退出。双击托盘图标也可预览。退出后删除便携目录即可卸载。

**如何退出/更新：** 在任务栏右侧展开“隐藏的图标”，找到提示文字为 `Meta Cue — 长按 Win 显示编号` 的图标，右键选择“退出”。再启动新版 `MetaCue.exe`；单实例机制不会自动替换已运行的旧程序。如托盘无响应，可在任务管理器结束 `MetaCue.exe`。

角标使用一个复用的透明分层窗口：所有数字及背景先离屏绘制，再通过一次 `UpdateLayeredWindow` 呈现，避免逐个黑框出现。图标位置查询提前到长按阈值之前，默认 300 ms 的长按门槛保持不变。可运行 `dotnet run --project windows/MetaCue.RenderTests -c Release` 检查十个角标在单窗口中的呈现、隐藏及复用；该测试会短暂显示合成角标，不代表真实任务栏验收。

可在 `%LOCALAPPDATA%/MetaCue/delay-ms.txt` 写入一个 100～2000 的整数，然后通过托盘重新读取配置；文件缺失或无效时默认 300。程序不创建启动项，不记录按键或窗口标题。

## 平台适配及边界

- 通过 UI Automation 读取原生任务栏应用按钮的真实屏幕矩形，不复制任务栏。仅识别已知应用按钮类，不猜测 Start、搜索或托盘按钮的编号。
- 目前仅主任务栏、底部单行布局；副屏任务栏、第三方任务栏、Windows 10、溢出和取消合并布局没有通过兼容性验收，不宣称支持。建议使用合并应用按钮且无溢出的布局。
- Windows 并没有这里使用的 KWin 私有“取消仅修饰键序列”接口。因此在首次显示前以 `SendInput` 发送未分配虚拟键 `0xE8` 的按下/松开，抑制本次开始菜单。**与 Linux 原版不同，这里存在合成键事件**；不吞掉 Win 的按下/松开，不改写 Win+数字绑定。受 UIPI 限制无法发送时取消显示，管理员窗口和安全桌面行为待实测。参考 [SendInput](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput)。
- 角标不激活窗口，使用透明 layered 窗口穿透鼠标；参考 [Windows layered window behavior](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)。开启 PerMonitorV2，但混合缩放下对齐仍需实机验收。
- 仅手势/预览期间启动计时器，显示时每 200 ms 检查位置。识别失败、布局变化、UIA 查询超过 600 ms、会话/电源/显示配置变化时隐藏；后台 UIA 查询不阻塞键盘钩子。Explorer 无响应时最多保留一个查询，不堆积线程。
- 短按保留开始菜单；普通数字提示遇到其他按键取消，扩展 Win+Shift 模式按上文独立处理。注入事件不会驱动手势。

## 验证状态

2026-11-22：Release 编译通过（0 警告、0 错误）；纯手势测试通过，包括左右 Win、重复按键、释放、组合键、预先按住 Shift、双 Win 和重置。当前命令执行会话的任务栏探测返回 `Explorer taskbar not found.`，因此真实桌面图标识别、显示位置、开始菜单抑制、Win+数字和鼠标穿透**尚未实测**。

可在实际桌面的终端中获取不含应用标题的诊断文件：

```powershell
Start-Process ./windows/artifacts/win-x64/MetaCue.exe -ArgumentList '--diagnose taskbar-diagnostic.txt' -Wait
Get-Content ./taskbar-diagnostic.txt
```

人工验收：

1. 预览核对 1～9、0 与 Win+数字对应的应用顺序及角标位置；第十个显示 0，搜索/开始按钮不应编号。
2. 分别短按、长按左右 Win，验证短按打开开始菜单、长按出现提示且松开不打开菜单。
3. 测试 Win+1、Win+R、Win+反引号及先按 Shift 再按 Win，确认原快捷键不受影响，不会在组合键释放后重新出现提示。
4. 显示期间点击/悬停/拖动任务栏图标，核对鼠标穿透及重排后取消；测试自动隐藏和溢出状态。
5. 测试任务栏增删图标、Explorer 重启、多屏缩放、锁屏/解锁和休眠恢复；恢复后应需新一次手势。
6. 托盘退出后再次长按 Win，确认系统原行为恢复。

完成上述验收前，将本分支视为可编译的 Windows 初版，而非已验证的全面等价移植。

扩展功能测试命令（原生测试需在实际交互桌面的终端运行，会短暂创建两个测试窗口并尝试恢复原前台）：

```powershell
dotnet run --project windows/MetaCue.Tests -c Release
dotnet run --project windows/MetaCue.RenderTests -c Release
dotnet run --project windows/MetaCue.NativeTests -c Release
```

只读分组诊断：`MetaCue.exe --diagnose-groups <输出文件路径>`。输出应用标识、程序路径、窗口句柄及匹配数量，不包含窗口标题或按键记录。诊断文件可能含本机用户名和路径，分享前检查。
