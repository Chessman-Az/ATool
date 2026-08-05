# A工具（ATool）交接文档

> 本会话结束时完整交接。读者：无上下文的后续 agent / 人类。所有关键事实已落字，可独立执行。当前最新提交：`e3508e9`（git log 可见完整历史，本会话共新增约 20 个提交，从 6.55 版迭代到 8 版）。

## 任务目标

用户原话级目标：一款 Windows 优先的 C# + Avalonia 桌面工具「A工具」，两个核心功能——① 灵活重复规则的本地提醒事项（日历视图、常驻托盘、休眠补发、**桌面提醒浮窗**）；② DeepSeek 多 API Key 余额监控（并发刷新、历史明细、趋势图、峰谷计价提示）。应用显示名「A工具」，命名空间 `ATool`，作者署名「小泽」，当前版本号 **8**（About 页，`src\ATool\Views\MainWindow.axaml` L143 附近）。

非目标：不做多用户/账号体系；不做 DeepSeek 调用次数统计；macOS/Linux 不承诺 Windows 专属功能（注册表自启/Toast 等降级）；不做声音/邮件等第三方通知渠道。

## 已完成

### 桌面提醒浮窗（本会话主战场，功能完整）
- `src\ATool\Views\FloatReminderWindow.axaml` / `.axaml.cs` — 无边框、透明背景（`TransparencyLevelHint="Transparent"`）、圆角白卡片（无方形外角）、不抢焦点（ShowActivated=False）、**任务栏隐藏**（Win32 WS_EX_TOOLWINDOW 兜底，`EnsureHiddenFromTaskbar()`，Avalonia 的 ShowInTaskbar=False 不可靠）。列表项 `FloatReminderItem(long Id, string Title, bool IsDone)`，圆圈按钮点击 → `CompleteRequested` 事件。已完成项：实心绿圈 + 文字删除线（`BoolToTextDecorationsConverter`，`src\ATool\Views\Converters.cs` 末尾）。
- `src\ATool\Services\FloatReminderService.cs` — 核心服务：400ms 轮询（前台窗口检测 + 鼠标热区检测）、展开/缩回位置插值动画（DispatcherTimer 16ms）、DPI 缩放适配（`Scale()` = `RenderScaling`，用户屏幕 150%）、热区 = 屏幕角落 32DIP ∪ 缩回细条区域（鼠标移到浮窗细条上即展开）。纯函数 `ComputeTarget` / `InHotZone` / `IsForegroundVisible` / `FilterScope` 已单测。
  - 显隐规则：前台是本进程窗口（主窗口/浮窗自身）、桌面（Progman/WorkerW）或任务栏（Shell_TrayWnd）→ 显示；其他软件 → 隐藏；主窗口最大化/全屏 → 隐藏。
  - 圆圈点击 = 切换完成状态（`SetStatus` toggle）；列表 60s 自动刷新。
- 设置项（`src\ATool\Services\SettingsService.cs` 末尾 + `src\ATool\ViewModels\SettingsViewModel.cs` + `src\ATool\Views\SettingsPanel.axaml`）：浮窗开关 `float_reminder_enabled`、位置 4 角 `float_reminder_corner`、透明度 `float_reminder_opacity`（0-100，**只作用于背景**，文字不透明，`ApplyBackgroundOpacity`）、展示范围 `float_reminder_scope`（0=仅未完成 1=全部）。保存即生效（`_floatReminder.Apply()`）。
- `src\ATool\Program.cs` — **单实例锁**（Mutex `Local\ATool_SingleInstance`，已有实例时新实例直接退出）。原因：A工具 关闭窗口只是隐藏到托盘、进程常驻，没有单实例锁时新旧版本进程并存，用户看到的一直是旧进程的浮窗（"改了好几遍没变化"的元凶之一）。
- `src\ATool\App.axaml.cs` — 启动时 `FloatReminderService.Apply()` + `SetMainWindow(desktop.MainWindow)`。

### 其余本轮迭代
- 主窗口：内容区包 ScrollViewer（窗口缩小时不错位）；图表美化（主色蓝折线+渐变填充+坐标轴浅灰，`src\ATool\ViewModels\BalanceChartViewModel.cs`）；折线图根因修复——`Series` 必须带通知（`[ObservableProperty]` + `[NotifyPropertyChangedFor(nameof(HasData))]`），且 `HasData` 属性必须存在（XAML 绑定了它，缺失时静默失败→提示文字永远显示）。
- 余额页：别名改长方形卡片框（WrapPanel，`MainWindow.axaml` 全部余额区）；Key 列表改卡片网格不滚动（`src\ATool\Views\ApiKeyPanel.axaml`，ListBox+WrapPanel，保留 SelectedItem 联动图表/明细）；Reload 默认选中第一个 Key（图表默认有数据）。
- 日历美化：今天红色高亮/选中主色高亮/周末表头浅红底（`ReminderCalendarPanel.axaml` + `CalendarDayVm.IsToday` + `CalendarDayBrushConverter`）。
- 刷新按钮探针日志（见当前卡点）：`ApiKeyPanel.axaml.cs` `OnRefreshAllClick` + `ApiKeysViewModel.RefreshAsync` 入口。

### 验证（最后一条）
```bash
export PATH="/c/Users/A/.dotnet10:$PATH"
taskkill //F //IM ATool.exe 2>/dev/null
dotnet build ATool.sln        # 0 错误
dotnet test ATool.sln         # 103/103 通过（本会话从 71 增至 103）
dotnet publish src/ATool -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
./publish/ATool.exe & sleep 12 && tasklist //FI "IMAGENAME eq ATool.exe"   # 冒烟：进程存活
```
发布产物 `publish\ATool.exe`（单文件自包含，约 98MB）。最新发布 00:04（提交 e3508e9）。

### 提交速查（本会话关键提交，`git log --oneline`）
```
e3508e9 fix: 框间间隙由卡片 Margin 控制（8px）+ 撤销屏幕边缘间距（贴边）
daf0bf4 fix: 单实例锁（Mutex）
8e7d700 feat: 浮窗展开时与屏幕边缘留 12px 间距（后续已撤销）
6a3e398 / 4f244b7 / 190f7c4 / f0945a2 / 1073849 / 47089f9  — 浮窗间距调整系列（多数改错了属性，见坑 #1）
cc8ab0e feat: 已完成提醒文字加删除线
84604fa feat: 浮窗展示范围设置 + 圆圈切换完成状态
06682ea feat: 浮窗待办圆圈可点击标记完成
7445579 fix: 浮窗强制任务栏隐藏（WS_EX_TOOLWINDOW）
aa66657 feat: 浮窗透明度设置 + 去标题纯白背景 + 任务栏视为桌面环境
2cbfa3e feat: 浮窗窗口透明化 + 透明度 0-100
c5b1461 feat: 桌面提醒浮窗首版 + 窗口缩小可滚动 + 版本 8
```

## 当前卡点

1. **浮窗框间间隙（用户反复反馈，本轮已修对属性，待复测）**：用户要的是「悬浮窗里提醒事项框与框之间的间隙」= 卡片 Border 的 **Margin 上下值**（`FloatReminderWindow.axaml` L39 附近，当前 `Margin="4,4"` → 间隙 8px）。此前多轮误改卡片 **Padding**（内部留白，与框间间隙无关），且某轮把 Margin 归零后间隙恒为 0px——用户连续反馈"没变化"是正确的。已修（e3508e9）并发布 00:04 版，**待用户复测确认**。
2. **用户可能运行旧进程**：A工具 托盘常驻，更新后必须让用户托盘右键退出旧进程再开新版（单实例锁已防止双进程并存，但旧进程不退出则新版无法启动）。确认版本看主窗口标题栏时间戳（`A工具 vMM-dd HH:mm`）。
3. **「立即刷新全部」按钮点击无反应（未闭环）**：日志（`%APPDATA%\ATool\logs\atool-*.log`）显示最后刷新记录停在 22:51，用户 22:59 版点击后无新日志——Command 可能未触发（代码静态检查正常：DataContext、RelayCommand 生成、绑定均无误）。已埋两层探针日志（「立即刷新全部按钮被点击（探针）」+「RefreshAsyncCommand 已执行（探针）」），**待用户配合**：点按钮后把日志新增行发来，区分「点击没到」vs「Command 没触发」vs「服务卡死」。
4. **工作区状态**：会话结束时注入快照显示 `FloatReminderWindow.axaml`、`FloatReminderTests.cs` 为 modified（e3508e9 提交后可能又有未提交变动）——新会话第一步先 `git status` 核实，未提交的改动提交掉。

## 下一步（按优先级，可立即执行）

1. 等用户复测 00:04 版浮窗框间隙（8px）。若仍不对：只改 `FloatReminderWindow.axaml` 卡片 Border 的 `Margin="4,4"` 上下值（间隙 = 上下和），不要碰 Padding。
2. 用户反馈刷新按钮问题时：查日志探针行定位断点（见卡点 3），按断点位置修绑定或服务。
3. `git status` 核实并提交遗留 modified 文件（卡点 4）。
4. 若用户要求浮窗更多定制（宽高/字号），入口在 `FloatReminderService.cs` 常量区（WindowW/WindowH 等，DIP 值）与 `FloatReminderWindow.axaml`。
5. 建议补 `.gitignore` 条目 `publish/`（当前 publish 目录 untracked，历史遗留）。

## 坑（绝对不要再踩）

1. **浮窗框间间隙 = 卡片 Border 的 Margin 上下值，不是 Padding**。Padding 是文字到边框的内部留白——改 Padding 用户永远看到"没变化"。（本次已交学费 6 个提交）
2. **Avalonia `ShowInTaskbar="False"` 在 Windows 不可靠**，必须 Win32 `WS_EX_TOOLWINDOW` 兜底（`FloatReminderWindow.EnsureHiddenFromTaskbar`）。删了它浮窗会出现在任务栏。
3. **DPI 缩放**：浮窗所有位置/热区计算必须 ×`RenderScaling`（用户屏幕 150%）。不乘 → "浮窗只显示一半"、热区不命中。`Scale()`/`Phys()` 在 FloatReminderService，勿删。
4. **单文件发布下 `Assembly.Location` 为空** → 启动崩溃。取可执行路径用 `Environment.ProcessPath`（MainWindow.axaml.cs L20-24）。
5. **Avalonia 声明式 Animation 不能动画 RenderTransform**（"No animator registered"）→ 用 DispatcherTimer 驱动角度（MainWindow.axaml.cs 有现成范例）。
6. **A工具 关闭窗口 = 隐藏到托盘，进程常驻**。更新版本必须托盘退出旧进程，否则看到的永远是旧 UI。单实例锁（Program.cs Mutex）已加，但旧进程不退出时新实例会静默退出——用户会觉得"双击没反应"。
7. **LiveCharts 2.0.5 + Avalonia 11.3.18 锁定**（升 Avalonia 12 会 MissingFieldException）。折线图 `Series` 必须是带通知的属性（`[ObservableProperty]` + NotifyPropertyChangedFor(HasData)），且 `HasData` 属性必须存在于 VM——XAML 绑定缺失属性是静默失败，图表永远不更新/提示永远显示。
8. **xunit 吞 Console.WriteLine** → 用文件探针（AppendAllText %TEMP%）或断言消息输出。
9. **Avalonia Color.ToString() 是 #RRGGBBAA**（非 WPF 顺序）→ 颜色断言用 RGB 分量比较。
10. **Dapper 不映射下划线列名**（Db.cs `MatchNamesWithUnderscores = true` 勿删，否则余额恒 0.00）；SQLite BLOB→byte[] 映射返回空数组 → 走手动 SqliteDataReader（ApiKeyRepository.GetAll）。
11. **XAML StringFormat 以 `{` 开头必须 `{}` 转义**（如 `StringFormat='{}{0}%'`），否则报 "Unable to resolve type 0"。
12. **运行中的 ATool.exe 锁 bin/publish 输出文件** → 构建/发布前先 `taskkill //F //IM ATool.exe`（MSB3021/设备忙）。
13. **ScrollViewer 是单 Content 控件**——塞多个子元素报 Content 多重赋值，必须包一层 Grid。
14. **write_file 大内容成功后，消息历史里的 `[file written to …]` 是指针不是内容**——回传会被拦截，重写必须写完整真实内容。
15. **多会话共享工作区**：仓库可能有并发 agent 会话改动同一批文件，改前先 `git status`/`git diff` 确认归属，别覆盖别人的改动。
