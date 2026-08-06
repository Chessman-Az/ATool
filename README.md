# A工具（ATool）

一款 Windows 桌面效率小工具：**本地提醒事项** + **DeepSeek 多 API Key 余额监控**，全界面科幻深色风格。

作者：小泽 ｜ 当前版本：v10 ｜ 技术栈：C# / .NET 8 / Avalonia UI 11

---

## ✨ 功能特性

### 📌 提醒事项
- **三种重复规则**：单次 / 每日 / 自定义每周几（每个选中的日子可设独立时间）
- **日历视图**：直观查看当月提醒，点击日期筛选当天事项
- **到点弹窗提醒**：可勾选「是否提醒」，触发时间精确到时/分/秒（下拉选择，默认当前时间），到点弹出置顶窗口，支持一键完成或延迟 5/15/30/60 分钟
- **桌面提醒浮窗**：鼠标移到屏幕角落弹出待办列表，位置（四角）、透明度、显示范围（全部/未完成）均可配置
- **中控台**：今日提醒一览（点击圆圈快速完成）+ DeepSeek 余额总览，科技感仪表盘
- 完成状态圆圈点击切换，已完成事项显示删除线

### 🪙 DeepSeek 余额监控
- **多 API Key 管理**：添加多个 Key 别名，密钥经 Windows DPAPI 加密存储
- **一键刷新**：并发刷新全部余额，支持单个 Key 单独刷新
- **余额趋势图**：历史余额折线图，纵轴从 0 开始
- **历史明细**：每次余额变动记录，增加绿色 / 减少红色
- **峰谷计价提示**：DeepSeek 高峰/低谷时段表，当前状态一目了然
- **自动刷新**：可配置刷新间隔，切页自动刷新（60 秒节流）

### 🖥️ 系统
- **开机自启**：注册表自启，仅驻留系统托盘不弹界面
- **单实例运行**：关闭窗口最小化到托盘，托盘菜单可显隐/刷新/退出
- **数据完全本地**：SQLite 存储，不上传任何数据

---

## 🛠️ 技术栈

| 类别 | 选型 |
|------|------|
| 语言/运行时 | C# / .NET 8 |
| UI 框架 | Avalonia UI 11（Fluent Dark 主题） |
| MVVM | CommunityToolkit.Mvvm |
| 数据存储 | SQLite（Dapper） |
| 图表 | LiveChartsCore 2 |
| 日志 | Serilog |

---

## 🚀 构建与运行

```bash
# 构建
dotnet build ATool.sln

# 测试（123 个用例）
dotnet test ATool.sln

# 发布单文件自包含 exe（win-x64）
dotnet publish src/ATool -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

运行 `publish\ATool.exe` 即可。更新版本前请先在托盘退出旧进程（单实例锁）。

> 构建环境要求 .NET 8 SDK；Windows 专属功能（注册表自启 / DPAPI / 托盘）在非 Windows 平台自动降级。

---

## 📂 数据位置

所有用户数据（提醒事项、API Key、设置、日志）保存在 `%APPDATA%\ATool\` 目录下，卸载软件后删除该目录即可彻底清除。

## 📄 许可证

本项目为个人工具项目，源码仅作学习参考。
