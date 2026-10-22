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
- `ProjBobcatGameRuntime` 只负责聚合和生命周期转发；核心创建、版本目录、
  资源下载、启动和 Mod 下载分别位于独立适配器中。
- `App.xaml.cs` 只负责组合根与桌面生命周期；配置加载位于
  `LauncherRuntimeConfigurationFactory`，无界面命令位于
  `CommandLineExecutionService`。

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
- 加载器安装优先使用 Forge、Fabric、OptiFine 原生安装模型。
- ProjBobcat 2.1.2 没有可用的 LiteLoader 安装模型；LiteLoader 自动安装
  明确标记为暂不支持，但已有 LiteLoader 实例仍必须由 locator 识别并可启动。
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
- `FindJava()` 结果之后必须合并扫描 `RootPath/runtimes`，同一
  `java.exe` 以受管运行时分类覆盖系统探测结果。
- 1.21.x 及以下的 Vanilla/Fabric 可在只有 Java 25 时回退，并返回非阻断警告。
- 旧版 Forge/NeoForge 不允许用 Java 25 回退。
- 26.1+ 不允许使用低于 25 的 Java。
- 用户显式指定 Java 时仍执行兼容性检查，确认后可继续。
- 手动 Java 路径在保存前必须实际执行 `java.exe -version` 验证，并持久化到
  `launcher-data/java-settings.json`；后续安装和启动优先使用该路径。
- Java 页面必须按主版本展示 Minecraft 兼容区间；完整性失败的受管运行时不得
  参与自动选择。

## Java 自动下载来源

- 首选 Mojang 为目标版本提供的 Java 运行时。
- 回退 Adoptium Temurin 公开 API。
- 输出存入 `RootPath/runtimes/<runtime>/<major>/<arch>`。
- 下载必须校验 SHA256，安全解压并以原子目录替换发布。
- 所有来源失败时报告完整错误并调用手动安装。
- Mojang 运行时按其清单 SHA1 校验，并为缓存生成 SHA256 完整性清单；
  Adoptium 下载必须匹配公开 SHA256。
- Mojang 完整性元数据必须分别标记发布方官方 SHA1 与本地文件 SHA256；
  不得把本地 SHA256 宣称为 Mojang 远端 SHA256。Adoptium 记录发布方归档
  SHA256 与本地文件 SHA256。
- 重新校验只能读取受管运行时的完整性清单并异步计算本地哈希。
- 安全删除前必须确认可执行文件位于 `RootPath/runtimes` 内、运行时目录带有
  `.slc-ready` 与 `.integrity.json`；禁止删除系统 Java 或 runtimes 之外的文件。

## 下载源与进度

- `appsettings.json` 中的 `DownloadSources` 只保存 URI 根列表，不包含版本清单内容。
- 游戏资源集合依次传入 ProjBobcat 的 Asset、Library、Version 和 Logging resolver。
- 多线程、续传、源回退和 SHA1 校验由 `DefaultResourceCompleter` 执行。
- Progress 事件必须桥接为 `IProgress<OperationProgress>`，并传递 CancellationToken。
- 下载任务记录持久化到 `launcher-data/download-tasks.json`，包含版本、Java、
  游戏目录、目标文件、失败项、实际来源、尝试次数和状态。
- 暂停只能通过取消当前 `DefaultResourceCompleter` 实现；继续与“重试失败项”
  必须重新调用同一个 Completer，让 `checkLocalFiles` 复用现有部分文件并补全
  缺失文件，不得实现并行下载循环。
- 进程异常退出后遗留的 Running 任务必须恢复为 Paused。
- 下载源健康评分持久化到 `launcher-data/download-source-health.json`；失败
  降权、连续成功加权，并作为 `DownloadUriInfo.Weight` 传给 ProjBobcat。
- 不因健康评分为零而永久删除配置源；评分只影响权重，所有来源仍可回退。

## 安装器完整性

- Forge、NeoForge、OptiFine 等外部安装 JAR 在调用安装器前必须校验发布方哈希。
- 哈希探测顺序为 `.sha256` 伴生文件、`.sha1` 伴生文件、官方 API 校验字段。
- 跟随下载重定向后，应在最终 URL 上继续寻找伴生哈希。
- 哈希缺失或不匹配时停止自动安装并提供手动安装指引，不得以“下载成功”代替校验。
- Fabric loader JSON 先做结构验证，其引用库文件由 ResourceCompleter 执行下载与哈希校验。

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
- Mod 归因必须基于 ProjBobcat 报告引用的原始日志与实例 Mod 列表：只有文件名或
  Mod ID 出现在堆栈/加载失败证据行时才能列出具体 JAR；没有证据时必须显示
  “无法定位到单个 Mod”，禁止猜测。
- 分析报告存入 `launcher-data/crash-insights/`，包含唯一报告 ID、原始日志路径
  和历史时间，不得提交用户日志。
- 历史记录支持按版本读取、删除和打开原始日志，并自动删除超过 30 天的报告。
- 后台分析异常必须通过 `IAppLogger` 上报；P2 将用 Serilog 替换 P1 的调试实现。
- `--analyze-crash --version <id>` 可用于无界面验证。

## Mod 管理

- 搜索必须使用 ProjBobcat `IModrinthApiService` 和 `ICurseForgeApiService`。
- CurseForge API key 由设置页输入，只以
  `DataProtectionScope.CurrentUser` 的 DPAPI 密文保存在
  `launcher-data/curseforge-credential.json`，不得写入仓库、日志或明文配置。
- 保存、替换或清除 CurseForge key 后，HTTP 适配器必须即时使用最新状态，
  不允许要求重启启动器。
- Mod 文件下载通过自定义 `IResourceInfoResolver` 交给 ResourceCompleter。
- 本地 JAR 必须解析 Fabric、Quilt 或 Forge/NeoForge 元数据后才能导入。
- 版本隔离时 Mod 写入 locator 对应的 `versions/<id>/mods`，否则写入根 `mods`。
- 冲突规则至少覆盖重复 Mod ID、缺失必需依赖和已配置的已知不兼容。
- `--search-mods`、`--install-mod`、`--scan-mods` 用于无界面模块验证。
- 远程文件名必须先经过安全净化，下载目标必须验证位于实例 `mods`
  目录边界内。
- 远程下载完成后必须复用本地 Mod 元数据读取器验证 JAR；无效文件应
  删除本次下载并返回失败。
- 缺少发布方 SHA1 时显示“无法校验”警告，禁止伪造哈希。
- 依赖版本范围支持 Maven 精确版本、比较运算、区间、`||` 与 JSON
  数组 OR 语义；可选依赖不阻断启动。
- 远程安装来源元数据保存到 `mod-sources.json`。只有能够从该元数据
  确定 provider、project 与兼容文件时才允许自动安装缺失依赖，否则
  必须引导手动安装。
- 菱形依赖必须检测多个约束是否存在交集，无交集时报告
  `DiamondDependencyConflict`。

## 安全与提交

- CurseForge API key 只能从当前 Windows 用户的 DPAPI 加密设置读取。
- CurseForge key 缺失、损坏或无法解密时只能降级为 Modrinth，不得阻塞主页。
- `bin/`、`obj/`、`.vs/`、`launcher_profiles.json`、`launcher_accounts.json`
  和 `.minecraft/` 不得提交。
- `main` 受保护，不直接提交；每个工作流使用独立 `feat/<步骤>-<描述>` 分支。
- 提交信息格式为 `<类型>: <简述>`，提交正文说明改了什么、为什么改及如何验证。
- 推送失败时立即报告并停止后续工作流。

## 外部联网验收

- NeoForge 与 CurseForge 的真实联网验收不阻塞 P1 代码合并，但其状态必须在
  `KNOWN_ISSUES.md` 中显式记录为“未完成”。
- mock、单元测试或 CI 只能验证代码路径，不得用于关闭真实联网未验收条目。
- NeoForge 安装器必须保留 stdout/stderr 到 `.install.log`；失败 UI 必须指向
  `KNOWN_ISSUES.md`，且不得隐藏安装入口。
- CurseForge Key 缺失路径必须实际返回清晰引导；有 Key 的搜索、下载和文件
  完整性验收需要在可访问外网且具备有效 Key 的环境中单独完成。
- 关闭 `KNOWN_ISSUES.md` 条目时必须附日期、commit、产物/日志位置和可复核的
  验收结果。

## 日志与诊断

- `IAppLogger` 的生产实现使用 Serilog 文件 Sink，输出到
  `launcher-data/logs/slc-<date>.log`。
- 文件按天和 10 MiB 滚动，保留最近 7 个文件并启用 `shared: true`。
- 认证载荷、访问/刷新/客户端令牌、Authorization 和 CurseForge API Key
  必须经过 `LogRedactor`，禁止写入日志或诊断包。
- 诊断包只包含最近日志、实例 `instance.json` 和 Java 运行时摘要；不得包含
  `curseforge-credential.json`、`launcher_accounts.json` 或令牌文件。
- 设置页提供打开日志目录和生成诊断包入口。

## CI 与冒烟

- `.github/workflows/ci.yml` 在 `windows-latest` 使用 .NET 10.x 执行 Release
  build 和两个测试项目。
- `.github/workflows/smoke.yml` 仅在 `main` push 和手动触发时运行，发布
  Windows x64 单文件 EXE，上传 artifact，并执行 `--smoke-test`。
- 联网冒烟允许失败但必须输出摘要和保留结果；失败不能用于关闭
  `KNOWN_ISSUES.md` 中的真实联网验收条目。
- GitHub runner 不执行 WPF UI 自动化。

## 本地化

- 用户可见字符串必须放入资源字典。
- ViewModel 不得硬编码中文文案，状态和错误通过资源键暴露。
- 默认语言为简体中文，同时保留 en-US 资源结构。

## 离线模式与玩家名

- 本项目仅提供离线认证，这是产品决策，不是缺失功能。
- 不实现微软登录、设备代码流、账户切换、退出登录、令牌刷新或 Azure
  Client ID。
- 离线玩家名是唯一身份配置，保存在 `launcher-data/player-profile.json`。
  玩家名为空时禁止启动；保存前去除首尾空白，限制为 1–16 个 Unicode
  字符并拒绝控制字符。
- 离线 UUID 由玩家名决定，界面必须提示修改玩家名会影响存档归属与皮肤引用。
- 离线账户只保存在内存账户解析器中，启动器不得创建带 AccessToken 的
  `launcher_accounts.json`；发现旧文件时应移除其中的令牌字段。

## 原版安装

- 原版清单来自 Mojang `version_manifest_v2.json`，UI 支持 release/snapshot
  筛选，不依赖本机已有游戏版本。
- 安装顺序固定为：版本 JSON/client JAR 物化 → locator 重新解析 →
  兼容 Java 选择或准备 → ResourceCompleter 补全 libraries/assets。
- 原版安装服务不得调用冒烟测试入口，也不得自行解析版本继承链或下载资源。

## 实例管理

- 实例配置独立保存为版本目录内的 `instance.json`，不得回写或重新解释版本
  JSON 的继承链。
- 空实例只允许写入用户批准的最小占位 JSON：
  `id`、`type: release`、`libraries: []`、`downloads: {}`。禁止伪造客户端、
  库或 `inheritsFrom`，并必须标记为不可启动。
- 实例启动设置包含 Java 建议、内存、JVM/游戏参数、窗口尺寸和全屏；正式启动
  必须通过 `LaunchSettings` 与 `GameArguments` 应用这些值。
- 复制实例必须创建新的 locator 版本目录并重写版本 JSON 的 `id`；不得覆盖
  已存在目标。
- 导出强制包含 Mod、来源元数据、加载器声明、`instance.json` 和 `config/`；
  默认包含 options、资源包、光影、数据包、脚本和结构文件。
- 导出默认排除存档、截图、`servers.dat`、日志、JRE 和启动器本体；存档、
  截图、`servers.dat` 与日志可由用户显式选择。
- 完整备份必须包含用户可选的存档、截图、服务器列表和日志。
- 导入 ZIP 前必须校验安全路径、`instance.json` 与加载器声明，并确认当前
  locator 已安装匹配的 Minecraft/加载器版本；目标 ID 冲突时必须拒绝覆盖。
- 导入复制必须清理来源实例数据后再部署归档，失败时删除本次新建目标目录。

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
- `publish/` 与 `release/` 为本地分发产物，不提交到 Git。
- `Assets/launcher.ico` 由 `tools/generate-icon.ps1` 生成，必须包含
  16/32/48/256 四种尺寸。当前图标是原创几何占位资产，正式品牌仍待确认。
- 发布 EXE 的 `FileVersion` 为 `0.1.0.0`，`ProductVersion`/InformationalVersion
  为 `0.1.0`，Company 为 `SLC`。
- 安装包通过 `installer/SlcMinecraftLaunch.iss` 使用 Inno Setup 编译到
  `release/`，安装范围为当前用户，并创建开始菜单快捷方式和卸载程序。
- `release/SHA256SUMS.txt` 必须覆盖独立 EXE、安装包和随附文档。
- 第三方组件清单和许可证链接维护在 `THIRD-PARTY-NOTICES.md`。
- 所有强制配置键必须由 `LauncherEndpointDefaults` 提供代码默认值。
  配置覆盖顺序固定为：代码默认值 → 可选 `appsettings.json` → `SLC_` 环境变量。
- 发布验收必须仅复制 EXE 到空目录并成功进入主页，不能依赖同目录配置文件。
- 代码签名和自动 GitHub Release 仍为可选待办，不阻塞当前安装包交付。

