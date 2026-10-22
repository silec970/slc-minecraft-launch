# SlcMinecraftLaunch 工程约定

## 环境要求

- Windows 10 版本 2004 或更高，推荐 Windows 11。
- .NET SDK 固定为 `10.0.402`，由根目录 `global.json` 约束。
- 桌面项目目标框架固定为 `net10.0-windows`；非 UI 项目目标框架为 `net10.0`。
- ProjBobcat 固定为 NuGet 稳定版 `2.1.2`，不使用 vendored 源码。
- 每次代码修改后必须依次完成构建、相关测试、中文提交和推送。

## 架构边界

- `SlcMinecraftLaunch.Wpf` 只包含 View、XAML 资源和组合根。
- `SlcMinecraftLaunch.Application` 负责用例编排、取消、进度和状态。
- `SlcMinecraftLaunch.Infrastructure` 独占 ProjBobcat、HTTP、文件系统和持久化适配。
- `SlcMinecraftLaunch.Domain` 只包含与框架无关的模型和跨层契约。
- ViewModel 不得直接引用 ProjBobcat；ProjBobcat 类型不得穿过 Infrastructure。
- View 不得包含业务逻辑，业务操作不得同步阻塞 Dispatcher。

## ProjBobcat 2.1.2 契约

- `App.xaml.cs` 在创建核心和发起下载前设置
  `ServicePointManager.DefaultConnectionLimit = 512`。
- 核心直接实例化 `DefaultGameCore`，版本扫描使用
  `DefaultVersionLocator(string, Guid)`，账户和 profile 分别使用
  `DefaultLauncherAccountParser` 与 `DefaultLauncherProfileParser`。
- 版本继承、版本隔离、资源下载、classpath、认证和启动参数全部由 ProjBobcat
  公开 API 处理。
- 下载进度来自 `DefaultResourceCompleter` 事件，并使用
  `CheckAndDownloadTaskAsync(..., CancellationToken)`。
- 游戏日志来自 `GameLogEventDelegate` 和 `GameExitEventDelegate`；
  崩溃归因来自 `DefaultLogAnalyzer` 和 `IAnalysisReport`。
- 加载器安装优先使用 Forge、Fabric、LiteLoader、OptiFine 原生安装模型。
- NeoForge 2.1.2 没有原生安装器，只允许通过其官方 installer CLI
  `--installClient` 适配；不得复用 Forge 安装模型。
- 所有直接使用 ProjBobcat 契约的位置必须带
  `// PROJBOBCAT:` 注释。

## 本地目录

- 默认游戏根目录位于可执行文件旁的 `.minecraft`。
- `--game-root`、`--version`、`--java` 可覆盖自动配置。
- 不得把本机绝对路径、用户令牌、下载的运行时或 Minecraft 用户文件提交到 Git。
- 版本隔离生效时，只能使用 locator 提供的实例路径。

## Java 选择策略

| Minecraft 版本 | 所需 Java |
| --- | --- |
| 1.12–1.16.x | 8 |
| 1.17 | 16 |
| 1.18–1.20.4 | 17 |
| 1.20.5–1.21.x | 21 |
| 26.1+ | 25 |

- 先调用 `SystemInfoHelper.FindJava()`，精确主版本优先。
- 1.21.x 及以下的 Vanilla/Fabric 可在只有 Java 25 时回退，并返回非阻断警告。
- 旧版 Forge/NeoForge 不允许用 Java 25 回退。
- 26.1+ 不允许使用低于 25 的 Java。
- 用户显式指定 Java 时仍执行兼容性检查，确认后可继续。

## Java 自动下载来源

- 首选 Mojang 为目标版本提供的 Java 运行时。
- 回退 Adoptium Temurin 公开 API。
- 输出存入 `RootPath/runtimes/<runtime>/<major>/<arch>`。
- 下载必须校验 SHA256，安全解压并以原子目录替换发布。
- 所有来源失败时报告完整错误并调用手动安装。
- Mojang 运行时按其清单 SHA1 校验，并为缓存生成 SHA256 完整性清单；
  Adoptium 下载必须匹配公开 SHA256。

## 下载源与进度

- `appsettings.json` 中的 `DownloadSources` 只保存 URI 根列表，不包含版本清单内容。
- 游戏资源集合依次传入 ProjBobcat 的 Asset、Library、Version 和 Logging resolver。
- 多线程、续传、源回退和 SHA1 校验由 `DefaultResourceCompleter` 执行。
- Progress 事件必须桥接为 `IProgress<OperationProgress>`，并传递 CancellationToken。

## 冒烟测试

- 默认目标版本为 `1.21.4`，可由 `--version` 覆盖。
- 若版本条目不存在，先使用 Mojang 版本清单物化原始版本 JSON 与 client JAR。
- 物化只创建版本条目，不解析 `inheritsFrom`、不拼 classpath、不执行认证交换。
- 物化成功后必须重新调用 locator，再由 `DefaultResourceCompleter` 补全资源。
- 本项目仅支持离线认证。正式启动使用设置页玩家名并通过
  `OfflineAuthenticator` 构造离线身份，这是产品决策而非未完成功能。
- 启动后通过 ProjBobcat 事件确认存活至少 5 秒。
- `26.3` 仅作为可用时的原版兼容抽检，不绑定加载器。

## 崩溃分析

- 游戏退出通过 `GameExitEventDelegate` 转发为领域事件，正式 UI 路径由
  `CrashAnalysisCoordinator` 做防抖、互斥和后台分析。
- 日志解析必须使用 `DefaultLogAnalyzer` 的 `GenerateReport()`。
- `CrashCauses` 只映射为分类、严重度和资源键；ViewModel 不保存中文建议。
- 分析报告存入 `launcher-data/crash-insights/`，不得提交用户日志。
- `--analyze-crash --version <id>` 可用于无界面验证。

## Mod 管理

- 搜索必须使用 ProjBobcat `IModrinthApiService` 和 `ICurseForgeApiService`。
- CurseForge API key 只来自环境变量 `CURSEFORGE_API_KEY`，不得写入仓库。
- Mod 文件下载通过自定义 `IResourceInfoResolver` 交给 ResourceCompleter。
- 本地 JAR 必须解析 Fabric、Quilt 或 Forge/NeoForge 元数据后才能导入。
- 版本隔离时 Mod 写入 locator 对应的 `versions/<id>/mods`，否则写入根 `mods`。
- 冲突规则至少覆盖重复 Mod ID、缺失必需依赖和已配置的已知不兼容。
- `--search-mods`、`--install-mod`、`--scan-mods` 用于无界面模块验证。

## 安全与提交

- CurseForge API key 只能从环境变量或未跟踪的用户配置读取。
- `bin/`、`obj/`、`.vs/`、`launcher_profiles.json`、`launcher_accounts.json`
  和 `.minecraft/` 不得提交。
- `main` 受保护，不直接提交；每个工作流使用独立 `feat/<步骤>-<描述>` 分支。
- 提交信息格式为 `<类型>: <简述>`，提交正文说明改了什么、为什么改及如何验证。
- 推送失败时立即报告并停止后续工作流。

## 本地化

- 用户可见字符串必须放入资源字典。
- ViewModel 不得硬编码中文文案，状态和错误通过资源键暴露。
- 默认语言为简体中文，同时保留 en-US 资源结构。

## WPF UI

- 主导航与七个模块使用独立 View/ViewModel：Home、Versions、DownloadLoader、
  Java、Mods、Crash、Settings。
- View 只保留 XAML、绑定和纯交互代码；拖拽事件只把文件路径交给 ViewModel。
- 异步命令使用 MVVM Toolkit 的 CancelCommand，禁止并发重入和 UI 线程阻塞。
- 共享颜色、按钮和卡片样式位于 `Resources/Styles.xaml`。
- 新增用户文案必须同时添加 zh-CN 与 en-US 资源。
- 离线模式没有登录、切换账户或退出登录语义；玩家名是配置项，不是
  账户状态。任何人不得提议或补回微软登录、设备代码流、令牌刷新或
  Azure Client ID。

## 主题与体验

- UI 设置持久化到 `launcher-data/ui-settings.json`。
- 语言切换通过替换 zh-CN/en-US 资源字典完成，资源键必须保持一一对应。
- 主题通过 Themes.Light/Dark 资源字典完成，切换后重建主窗口以刷新静态资源。
- 背景使用原创渐变与形状，不复制 PCL2 的商标或素材。
- 导航动效为 180ms 淡入和位移动画，可由“启用轻量动效”关闭。
- 背景音乐只播放 `launcher-data/music` 中的用户本地音频；无文件时安静降级。

## 发布

- 发布目标为 Windows x64 自包含单文件，不使用 Native AOT，也不启用裁剪。
- 发布命令：

  ```powershell
  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o ./publish
  ```

- 产物路径：`./publish/SlcMinecraftLaunch.Wpf.exe`。
- 预期体积约为 70–100 MB；压缩后具体大小会随 SDK 和依赖变化。
- `publish/` 为本地分发产物，不提交到 Git。
- 当前缺少 `Assets\launcher.ico`，`ApplicationIcon` 暂时注释，属于待补充图标。

