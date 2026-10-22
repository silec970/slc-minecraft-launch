# SlcMinecraftLaunch

基于 .NET 10、WPF、MVVM 与 ProjBobcat 2.1.2 的 Minecraft 启动器。

外部联网验收状态和复现步骤见 [KNOWN_ISSUES.md](KNOWN_ISSUES.md)。

## 构建

```powershell
dotnet build SlcMinecraftLaunch.slnx
```

## 单文件发布

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o ./publish
```

发布产物位于 `./publish`，目标为可直接运行的 Windows x64 自包含 EXE。
所有官方端点均内置为代码默认值，因此仅复制 EXE 也能启动；外部
`appsettings.json` 只用于覆盖默认配置。

## 品牌资产

`Assets\launcher.ico` 是 `tools/generate-icon.ps1` 生成的原创几何占位图标，包含
16、32、48 和 256 多尺寸帧。正式品牌图标与产品信息仍待项目所有者确认。

## 安装包

使用 Inno Setup 6 编译 `installer\SlcMinecraftLaunch.iss`。发布前需先生成
`publish\SlcMinecraftLaunch.Wpf.exe`；安装包、快捷方式、卸载程序和校验文件输出
到 `release\`。

