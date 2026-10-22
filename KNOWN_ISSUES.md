# Known Issues

更新时间：2026-10-22

P1 采用“代码完成即可合并，外部联网验收状态显式记录”的退出策略。本文件中的
未验收条目不会因 mock、单元测试或 CI 通过而关闭；只有在下述真实环境中完成
验收并更新本文件后，才能标记为已通过。

| ID | 状态 | 类型 |
| --- | --- | --- |
| KI-P1-NEOFORGE-LIVE-NETWORK | 未完成真实联网验收 | 外部网络阻塞 |
| KI-P1-CURSEFORGE-LIVE-NETWORK | 未完成真实联网验收 | 需要有效 API Key |

## KI-P1-NEOFORGE-LIVE-NETWORK

### 当前状态

- NeoForge 安装代码已实现，并保留 UI 安装入口。
- 安装器下载通过统一下载器完成，并在执行前校验发布方 `.sha256` /
  `.sha1` 哈希。
- 当前环境访问 NeoForge Maven 端点超时，因此尚未完成从 UI 安装并启动
  真实 NeoForge 实例的验收。
- 自动化 mock 和哈希单元测试不能替代本项真实联网验收。

### 可复现验收步骤

1. 在可访问
   `https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml`
   的网络环境下启动发布版启动器。
2. 在“下载与安装”页选择与 NeoForge 版本匹配的 Minecraft 原版版本。
3. 将加载器选择为 NeoForge，获取版本列表并通过 UI 完成一次安装。建议同时
   指定一个空实例 ID，以验证占位实例被正确填充。
4. 保留以下产物：
   - 安装器：
     `.minecraft/downloads/loaders/neoforge/<loader>/neoforge-<loader>-installer.jar`
   - 合并后的 stdout/stderr：
     `.minecraft/downloads/loaders/neoforge/<loader>/neoforge-<loader>-installer.jar.install.log`
   - 最终版本 JSON：`.minecraft/versions/<instance>/<instance>.json`
   - 如果失败：`.minecraft/launcher-data/loader-install-result.json`
5. 从主页或版本页启动该 NeoForge 实例，确认进程启动后保持运行，并检查
   ProjBobcat 日志没有加载器致命错误。
6. 将结果、日期、启动器 commit 和日志位置补充到本条目，然后将状态改为
   “已通过”。

### 失败时的预期行为

- UI 必须继续显示 NeoForge 入口。
- 状态栏必须显示本地化的 NeoForge 安装失败提示，并指向本文件。
- 原始错误详情和安装器输出路径必须保留，不得吞掉错误。

## KI-P1-CURSEFORGE-LIVE-NETWORK

### 当前状态

- 无 CurseForge API Key 的路径已验收通过：搜索返回 Modrinth 结果，UI 显示
  `Mods.CurseForgeKeyMissing` 本地化引导，不抛出异常。
- 当前环境没有有效 CurseForge API Key，因此真实 CurseForge 搜索和下载尚未
  完成验收。
- 自动化 mock 和“Key 缺失”测试不能替代本项真实联网验收。

### 可复现验收步骤

1. 在设置页输入有效 CurseForge API Key；密钥只应保存为当前 Windows 用户的
   DPAPI 密文。
2. 在 Mod 管理页搜索一个已知 CurseForge 项目，确认结果包含 CurseForge 来源。
3. 下载一个与当前 Minecraft/加载器版本兼容的文件。
4. 检查下载文件通过 CurseForge SHA1 或页面显示的“无法校验”警告路径；确认
   JAR 元数据读取成功，无效文件会被删除。
5. 重启启动器，确认已保存 Key 可复用；随后清除 Key，再次搜索，确认降级为
   Modrinth 且不会崩溃。
6. 将结果、日期、启动器 commit、项目 ID 和文件 ID 补充到本条目，然后将状态
   改为“已通过”。不要在本文件中记录 API Key。

### Key 缺失路径验收命令

```powershell
$env:CURSEFORGE_API_KEY = $null
dotnet run --project .\src\SlcMinecraftLaunch.Wpf\SlcMinecraftLaunch.Wpf.csproj -- --search-mods --query sodium --version 1.21.4 --loader Fabric
```

预期结果：退出码为 `0`，结果包含 Modrinth 项，并包含
`Mods.CurseForgeKeyMissing` 警告。

## P1 可离线验证状态

- `dotnet build`：通过。
- 核心单元测试：通过。
- WPF ViewModel 测试：通过。
- CurseForge Key 缺失 UI 状态：通过。
- NeoForge 安装器哈希匹配/不匹配逻辑：通过。
