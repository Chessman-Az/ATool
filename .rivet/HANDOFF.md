# A工具（ATool）交接文档

> 本会话结束时完整交接。读者：无上下文的后续 agent / 人类。所有关键事实已落字，可独立执行。当前最新提交：`7d26007`（main 分支，git log 可见完整历史；本会话从版本 9 迭代到版本 12，新增时间大师/机哥/充值明细三大功能）。工作区 git 状态干净（仅 `.rivet.md`、`AGENTS.md` 两个历史遗留 untracked，勿提交）。

## 任务目标

用户原话级目标：一款 Windows 优先的 C# + Avalonia 桌面工具「A工具」——提醒事项（到点弹窗、日历、浮窗）+ DeepSeek 多 Key 余额监控 + 中控台（今日提醒/余额总览）+ 时间大师（软件/游戏/网站使用时长统计）+ 机哥工具舱（硬件信息 + 7 个内置便携工具）+ 充值明细（自动识别余额增加 + 手动补录 + 佣金/实际金额）。全界面科幻深色风格（Fluent Dark），作者署名「小泽」，当前版本号 **12**（About 页 `src\ATool\Views\MainWindow.axaml` 关于页区，L160 附近）。

非目标：不做多用户/账号体系；不做 URL 级网页追踪（时间大师按浏览器窗口标题聚合）；不做时长提醒/限额；不做跨设备同步；第三方工具二进制不进 Git 仓库（tools/ 已 gitignore，随发布产物分发）。

## 已完成（按提交倒序，全部已验证）

### 充值明细窗口初始筛选跟随当前 Key（3555117）
- 用户反馈"选岳代伟打开充值明细还显示 AZ 记录"：根因是窗口默认筛选第一个别名（AZ）。`BalanceDetailPanel.OpenRecharge` 现在把当前选中 Key 的别名（`BalanceDetailViewModel.CurrentKeyAlias`）通过 `RechargeViewModel.SelectAlias` 带入——从余额页选哪个别名，充值明细窗口就只显示哪个别名的记录。测试 `SelectAlias_从余额页带入_初始筛选跟随当前Key`。
- **用户仍在运行旧版**（publish\ATool.exe 13:09，进程锁文件）：最新版在 `publish-new\ATool.exe`（13:51，含 3555117），需用户托盘完全退出后复制替换 publish\ATool.exe。

### 手动添加别名跟随筛选 + 旧数据归 AZ（aa7e06a + 数据迁移）
- `RechargeViewModel.OnSelectedAliasChanged`：顶部筛选切到某别名时手动添加别名自动跟随（在哪个别名视图下添加默认归哪个别名，仍可手动改）；「全部」不改动手动别名。测试 `切换筛选别名_手动添加别名跟随筛选`。
- **用户真实数据迁移**（2026-08-07，已备份 `%APPDATA%\ATool\data\atool.db.bak-20260807`）：recharge_details 加 alias 列；7 条旧手动记录（无归属）全部归 `AZ`（用户确认"添加的全部内容都是AZ的"）。岳代伟名下原无任何充值记录，旧版 bug（总充值含全部手动记录）导致显示虚高——0658d1b 按别名过滤后归 0。
- **新版 exe 已发布到 `publish-new\ATool.exe`**（100.8MB，13:46）：旧进程（PID 31096）锁着 `publish\ATool.exe` 无法覆盖——用户托盘退出旧版后，把 `publish-new\ATool.exe` 替换 `publish\ATool.exe`（或直接运行 publish-new 下 exe）即生效。数据迁移已提前落库，新版启动即可见正确归属。

### 余额明细按别名独立统计（0658d1b）
- `RechargeRepository.GetManualTotal(string? alias = null)`：别名过滤手动补录合计（旧记录无别名不计入任何别名）；`BalanceDetailViewModel.Load()` 选中 Key 时手动补录按该 Key 别名归属——每个别名余额变动明细下的总充值/总消费完全独立（自动识别部分原已按 Key 过滤，本轮补上手动补录归属）。
- 测试：`GetManualTotal_按别名过滤_各别名单独统计`（RED→GREEN）；全量 218 通过。
- 注：手动添加选别名（RechargeWindow 别名下拉）为 815d546 已实现，本轮核实无缺。

### 时间大师兜底 + 充值明细按别名分离（815d546）
- `src\ATool\Services\AppUsageCategorizer.cs`：新增 `TitleLooksLikeBrowser`（标题后缀判浏览器）/`ExtractAppName`（标题→应用名兜底，浏览器标题取浏览器名）；`ExtractSiteName` 支持进程名为空时按标题自身去后缀。
- `src\ATool\Services\UsageTrackerService.cs`：分类加 `CategorizeWithFallback`——进程名解析失败（提权/受保护进程）时按标题后缀兜底识别浏览器。
- `src\ATool\Services\UsageAggregator.cs`：`Summarize` 应用名进程名为空时用标题兜底（不再全部聚合成「未知」导致排行只显示一条）；分类对标题带浏览器后缀的历史记录重判为 browser（旧数据浏览器时长/网站明细复活）。
- 充值明细按别名分开：`Db.cs` recharge_details 加 `alias` 列（幂等 ALTER + 重建迁移同步含 alias）；`RechargeRepository` 查询别名用 `CASE WHEN r.alias 空 THEN COALESCE(k.alias,'手动记录')`、`InsertManual` 加 alias 参数、新增 `GetAliases`；`RechargeViewModel` 加别名筛选（默认第一个别名，可切「全部」）+ 手动添加必须选别名 + 汇总按筛选计算；`RechargeWindow.axaml` 加别名下拉两处。
- 测试：AppUsageCategorizerTests +11、UsageAggregatorTests +2、RechargeRepositoryTests +3、新增 RechargeViewModelTests（5 个）；全量 217 通过。

### 机哥三修（7dd5bb9）
- **运行时间/构建号/逻辑处理器不显示根因**：`JiGePanel.axaml` 三处 `Text="文字 {Binding X}"` 混写非法——Avalonia 属性值不以 `{` 开头按字面文本处理，绑定不生效。改为 TextBlock 内 `<Run>` 拼接（Avalonia 11 支持 Inlines）。
- **内存条数/品牌**：`HardwareInfoService` 加 WMI `Win32_PhysicalMemory` 查询（容量/频率/品牌/型号/插槽）→ `MemoryModules` + `MemoryModuleCount`；`JiGeViewModel` 暴露 `MemoryModules`/`MemoryCountText`；内存卡片显示总量 + 共 N 根 + 每根明细。
- **工具框加大**：磁盘/其他工具卡片 150×160 → 180×190，图标 58→66、图片 42→48，字号 10→11/12→13，按钮 Padding 加大。

### 总消费实时公式（7d26007）
- `src\ATool\ViewModels\BalanceDetailViewModel.cs` L60-65：总消费 = 总充值（余额相邻差 + 手动补录）− 当前实时余额（下限 0）；`_keys.PropertyChanged` 监听 `SelectedKey` 与 `TotalBalance`，余额刷新即重算。
- 验证：`dotnet test ATool.sln` 190/190 通过；发布冒烟进程存活。

### 充值明细窗口三修（e3d83cd）
- `src\ATool\Views\RechargeWindow.axaml`：窗口 760×760、列表改 Grid 星行（RowDefinitions="Auto,Auto,Auto,Auto,*"）撑满全屏；添加区 WrapPanel 防缩小溢出；实际/佣金输入 `ShowButtonSpinner="False"`（去箭头）。
- `src\ATool\Data\RechargeRepository.cs`：新增 `GetManualTotal()`（只统计 history_id IS NULL 的手动行）；`src\ATool\Views\BalanceDetailPanel.axaml.cs` 窗口 Closed 时刷新余额页总充值。

### 差值公式更正（c21fadd）
- `src\ATool\Services\RechargeService.cs`：`Diff = TotalDelta - TotalActual + TotalCommission`（用户纠正，原为减佣金）。测试同步。

### 充值明细功能（a32b0cc，13 文件 +693 行）
- `src\ATool\Data\Db.cs`：新增 `recharge_details` 表（history_id 可空 REFERENCES balance_history + delta_amount/actual_amount/commission_amount/manual_time）+ 幂等迁移（补列 + 表重建加 manual_time）。
- `src\ATool\Services\RechargeService.cs`：`DetectRecharges`（按 Key 分组、相邻余额差 >0 识别充值）+ `Summarize`（充值/实际/佣金/差值）。
- `src\ATool\Data\RechargeRepository.cs`：`EnsureAndGetAll`（INSERT OR IGNORE 幂等建行，UNIQUE 索引 idx_recharge_history 保证）/`InsertManual`/`UpdateActual`/`UpdateCommission`/`GetManualTotal`。
- `src\ATool\ViewModels\RechargeViewModel.cs` + `src\ATool\Views\RechargeWindow.axaml`：顶部汇总四卡 + 明细列表（实际/佣金可编辑）+ 手动添加区 + 保存。
- `src\ATool\Views\BalanceDetailPanel.axaml`：标题行「充值明细」按钮（Click=OpenRecharge）+ 总充值/总消费行；`BalanceHistoryRepository.cs` 加 `GetTotals`。
- 测试：`tests\ATool.Tests\RechargeServiceTests.cs`（5 个）、`RechargeRepositoryTests.cs`（5 个，含外键需先插 api_keys）。

### 机哥工具舱（d5c6745 + e6492e3 + e299a11）
- `src\ATool\Views\MainWindow.axaml`：导航「时间大师」下方新增「机哥」（NavIndex=4，设置 5、关于 6 顺延）；`MainWindowViewModel.cs` 加 `JiGe` 属性（构造注入）。
- `src\ATool\Services\HardwareInfoService.cs`：机型/系统（构建号 ≥22000 判 Win11，微软 ProductName 兼容性陷阱）/CPU/内存（GlobalMemoryStatusEx）/WMI 显卡/硬盘/显示器/网卡；`JiGePanel.axaml` 6 分类卡片 + 工具正方形网格（150×160 + exe 提取图标）。
- `src\ATool\Services\ToolCatalog.cs`：7 个工具配置（CrystalDiskInfo 9.9.2 / HD Tune 2.55 / DiskGenius 免费版 / SpaceSniffer / Geek Uninstaller / Rufus 4.15p / Everything），`tools\` 目录检测 + 启动 + 官网按钮。二进制在 `publish\tools\`（约 40MB，随发布分发）。

### 时间大师（c05bac2 + edf0f01 + fa3132a + 7c0d44b）
- `src\ATool\Services\UsageTrackerService.cs`：5s 轮询前台窗口（GetForegroundWindow + GetWindowText + QueryFullProcessImageName 兜底进程名——**提权进程 GetProcessById 抛异常曾致不写库**），分段写 `usage_log` 表（Db.cs 建表 + idx_usage_log_start），60s flush，90 天清理。
- `src\ATool\Services\UsageAggregator.cs`：`RangeOf`（本周=自然周周一00:00→下周一00:00、本月=自然月、CustomDate=整天）+ `Summarize`；`ReminderScheduler.cs` 加 `TriggersToday`/`HasTriggerOnDate`。
- `src\ATool\ViewModels\TimeMasterViewModel.cs` + `TimeMasterPanel.axaml`：今日/本周/本月/指定日期范围、总时长/办公/浏览器/游戏（统一分钟显示）、近 7 天柱状图（今日隐藏）、应用/网站明细、实时采样状态条。
- 诊断日志：每 60 tick（5 分钟）记录前台窗口状态（pid/proc/title/class/system）到 `%APPDATA%\ATool\logs\atool-YYYYMMDD.log`。

### 中控台独立页（0764207 + e99c4d7）
- 导航最上方「中控台」（NavIndex=0），今日提醒（HasTriggerOnDate 判定 + 圆圈完成）+ DeepSeek 余额总览深色卡片；提醒页恢复原布局。
- 修复：`MainWindowViewModel.OnNavIndexChanged` 余额刷新条件 `value==1→2`（插入中控台后的错位）。

### 早前（版本 9-10 时代，供参考）
- 提醒触发时间时/分/秒三下拉（1a2fec1，默认当前时间）；到点弹窗跨线程修复（ee67130，Dispatcher.UIThread.Post）；科幻深色换肤（75730bc，App.axaml Fluent Dark + 电光青色板）；设置页左右布局（9b07b06）；峰谷按钮图标调整（52564b5/d625bc3）；版本 12（99554a1）。

## 当前卡点

1. **时间大师真实环境采样待用户复测**：进程名解析失败场景已加标题兜底（815d546）——进程名空时按窗口标题显示应用名、按标题后缀统计浏览器，不再全部聚合「未知」。但用户机器上前台窗口进程名是否大面积拿不到（诊断日志每 5 分钟一行「时间大师采样诊断」可确认 proc 是否为空）仍需用户复测确认修复生效。
2. **机哥 WMI 实机显示待确认**：Win32_PhysicalMemory（内存条数/品牌）与显卡/硬盘/显示器/网卡在你的真实机器上的返回值未知，需用户打开机哥页确认内存条明细是否列出。
3. **GitHub 相关**：本地已到 7dd5bb9（4 个新提交 815d546/7dd5bb9 未推送）；v12 Release 已发布；旧 token 已多次出现在命令历史，**应提示用户吊销重发**。

## 下一步（按优先级，可立即执行）

1. 用户复测时间大师/机哥页后按反馈收尾（如仍有问题读 `%APPDATA%\ATool\logs\atool-YYYYMMDD.log` 诊断行）。
2. 推送新提交到 GitHub（token 需用户提供新值）：`git push https://<token>@github.com/Chessman-Az/ATool.git main`。
3. 若需发 v13 Release：`dotnet publish` 后把 exe + tools zip 传到 Releases（API 见历史模式）。
4. 手动冒烟：退出托盘旧进程 → `publish\ATool.exe`（标题 `A工具 vMM-dd HH:mm` 确认版本）。

## 坑（绝对不要再踩）

1. **MVVM 命令去掉 Async 后缀**：`RefreshAsync()` 生成 `RefreshCommand` 不是 `RefreshAsyncCommand`——XAML 绑定错名静默失败，按钮无反应。
2. **提权进程 GetProcessById 抛异常**：进程名解析必须 Win32 `QueryFullProcessImageName` 兜底，否则采样静默 Skip 永不写库（时间大师曾长期 0 行）。
3. **Avalonia `Border.Effects`/`DropShadowEffect.ShadowDepth` 是 WPF 属性名**：Avalonia 11 解析失败，用 `BoxShadow` 字符串属性；此类 XAML 错误被增量构建掩盖，全量 build/发布才暴露（曾致发布版启动崩溃 "No precompiled XAML found"）。
4. **`??` 不能用于非 nullable 值类型**：`decimal ??: 0` 报 CS0019，先确认类型再写。
5. **StackPanel 不会拉伸子元素**：列表要撑满窗口必须 Grid 星行（`RowDefinitions="...,*"`）+ ScrollViewer 在星行；且 StackPanel→Grid 改造时开头和结尾标签都要改（曾漏改 `</StackPanel>` 报 AVLN1001）。
6. **旧进程锁单实例**：更新版本必须先托盘退出旧进程，否则双击新 exe 静默无反应（单实例 Mutex `Local\ATool_SingleInstance`）。
7. **XAML StringFormat 以 `{` 开头必须 `{}` 转义**（`StringFormat='{}{0}%'`）。
8. **IsVisible 反转陷阱**：条件可见性用专用 bool + `BoolConverters.Not`，别直接绑反条件。
9. **Grid 覆盖层加 RowSpan**：遮罩/动画在改 Grid 布局后默认只盖第 0 行。
10. **xunit 吞 Console.WriteLine**：调试用文件探针（%TEMP% 或 Serilog 日志）或断言消息。
11. **Dapper 下划线列名**：`MatchNamesWithUnderscores=true` 在 Db 构造已开，勿删；SQLite BLOB→byte[] 手动 SqliteDataReader。
12. **构建前 taskkill ATool.exe**：运行中的 exe 锁 bin/publish，构建/发布报错先杀进程。
13. **write_file 大内容**：成功后历史显示指针不是占位符，重写会触发拦截；覆盖已有文件前先 read_file（门禁）。
14. **CRLF 警告无害**：Windows 下 git 提示 "CRLF will be replaced by LF" 是正常警告，不影响提交。
15. **SQLite 外键**：balance_history 引用 api_keys，测试插历史记录必须先插 key，否则 FOREIGN KEY constraint failed。
16. **`INSERT OR IGNORE` 需要 UNIQUE 索引**：幂等建行依赖唯一约束（idx_recharge_history），否则重复行。
17. **Windows 路径**：Git Bash 用正斜杠；cmd 调 exe 用 `cmd //c`（`./xxx.exe` 会 Permission denied）。
18. **计划工具 plan close 需 checkbox 任务块**：纯编号列表计划无法 close，用 updateClosure 或直接交付。
