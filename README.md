# Meta Cue

给现有 KDE 原生任务栏添加 Meta＋数字提示。按住 Meta 300 毫秒后，在图标上沿显示 1～9；松开或组成其他快捷键时消失。

数字提示触发后，松开 Meta 只隐藏数字，不弹出应用启动器。未达到显示延迟的短按 Meta 仍正常打开启动器。Meta＋反引号保持 KDE 原有快捷键行为，不再作为专用取消键。

![当前桌面预览](docs/taskbar-preview.png)

## 当前状态

首版已编译，手势单元测试及独立虚拟 KWin 输入集成测试通过。在当前真实桌面识别到 6 个图标，预览 1～6 的定位与自动消失已验证。

**当前桌面只安装了显示组件。输入插件尚未安装到系统，因此长按 Meta 暂时不会触发。** 还需下方安装步骤及真实按键、锁屏/休眠恢复验收。尚未启用登录自启。

## 设计

- 一个 KWin `InputEventSpy` 插件观察按键，不拦截按键事件，在提示触发时取消本次“仅修饰键”快捷键序列，不改写快捷键配置、不访问 `/dev/input`，不记录键值或输入文本。
- 插件仅通过会话 D-Bus 发送显示/隐藏布尔状态。显示期间每 100 ms 发送心跳；显示组件超过 350 ms 收不到心跳就隐藏。
- 不可见的独立 Plasma 桌面组件查找原生 `org.kde.plasma.icontasks`，在实际任务项上添加不接收鼠标的角标。没有复制任务模型，也没有改动系统任务栏文件。
- 角标处于原生面板场景中，沿用原生缩放、裁剪、遮挡和输出位置。读取当前快捷键，只标有对应 Meta+数字绑定的项目。
- 首版仅支持一个底部、单行的原生仅图标任务管理器。编辑、拖动、重排过渡或无法识别时暂停显示。

使用 KWin 和 Plasma 的内部 C++ / QML 接口，**升级 KWin、Qt 或 KGlobalAccelD 后需要重新编译**。KWin 的插件版本校验会拒绝不匹配版本，工具不应影响原有快捷键。

## 编译

本机需 Qt 6、KDE/Plasma/KWin/KGlobalAccelD 开发文件（含 Qt GUI 私有开发头文件），C++23 编译器、CMake、Ninja、ECM 和 Vulkan 头文件。

```sh
sudo pacman -S --needed base-devel cmake ninja extra-cmake-modules vulkan-headers
cmake -S . -B build -G Ninja
cmake --build build -j 4
ctest --test-dir build --output-on-failure
./scripts/test-integration.sh
```

本次构建因执行环境不能运行 sudo，使用临时目录中的官方 Vulkan-Headers v1.4.328：

```sh
cmake -S . -B build -G Ninja -DVulkan_INCLUDE_DIR=/tmp/Vulkan-Headers-1.4.328/include
```

临时目录被清理后，请正常安装 `vulkan-headers` 并重新配置构建。

## 安装与启动

**当前机器的显示组件已安装，直接执行下面两行：**

```sh
sudo ./scripts/install-input.sh
./scripts/meta-cue start
```

安装脚本只新增 `/usr/lib/qt6/plugins/kwin/plugins/meta_cue_input.so`，不覆盖系统已有组件。初次安装无需重启桌面。

全新安装时还需要先执行 `./scripts/meta-cue install-display`，将显示组件放入 `~/.local/share/plasma/plasmoids/local.meta-cue`，并在桌面添加一个无可见内容的伴随组件。添加前备份布局到 `~/.local/share/meta-cue/backups/`，不往任务栏插入组件。

```sh
./scripts/meta-cue status   # 输入插件版本、图标识别状态和角标数量
./scripts/meta-cue preview  # 手动显示两秒，用于核对位置，不切换应用
```

实际按键验收通过后启用自启动：

```sh
./scripts/meta-cue enable-autostart
```

自启动入口依赖当前项目路径，请不要移动本项目，或移动后重新执行该命令。升级已经加载过的二进制后，需注销并重新登录，避免 Qt 缓存旧库。

## 配置

可创建 `~/.config/meta-cuerc`：

```ini
[General]
Enabled=true
DelayMs=300
```

默认无需创建文件。延迟支持 100～2000 ms；每次新的 Meta 手势读取配置。提示触发前松开 Meta 保留原有启动器行为；提示触发后松开不弹出启动器。

## 停用与卸载

```sh
./scripts/meta-cue stop
./scripts/meta-cue disable-autostart
```

完整卸载：

```sh
./scripts/meta-cue uninstall
sudo ./scripts/install-input.sh --remove
```

卸载不恢复整份旧桌面布局，以免覆盖你后来进行的桌面修改；只移除 Meta Cue 自己的组件和入口。备份和可选配置文件保留。

## 验收记录

- 已通过：6 个手势单元场景（另含测试初始化/清理）。
- 已通过：独立 KWin 6.7.5 会话的插件加载/卸载、300 ms 阈值、快速点按、组合键取消、不重新触发、预按 Shift、右 Meta。
- 已通过：真实 Plasma 桌面发现 6 个原生任务项；显示 6 个对应角标并截图检查；两秒后隐藏。
- 待验证：当前桌面实际 Meta+数字切换、鼠标交互、热插拔/改变缩放、锁屏和休眠恢复、登录自启。

完整需求在 [docs/requirements.md](docs/requirements.md)。测试用输入驱动只用于独立虚拟会话，安装脚本不会安装它。

## 更新输入插件（0.1.2）

```sh
sudo ./scripts/install-input.sh
```

如果旧输入插件已经加载，安装后需注销并重新登录，再执行 `./scripts/meta-cue start`（已启用自启动则无需手动启动）。仅卸载再加载插件可能仍使用 Qt 缓存的旧二进制。

新增隔离测试覆盖：左右 Meta 长按触发提示后松开不打开菜单、下一次短按正常打开菜单、Meta＋反引号继续触发原有快捷键，以及卸载后长按恢复原启动器行为。
