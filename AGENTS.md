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
- 冒烟认证只允许 `OfflineAuthenticator` 与 `SmokeTestBot`，不得用于正式登录。
- 启动后通过 ProjBobcat 事件确认存活至少 5 秒。
- `26.3` 仅作为可用时的原版兼容抽检，不绑定加载器。

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

