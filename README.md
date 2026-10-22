# SlcMinecraftLaunch

基于 .NET 10、WPF、MVVM 与 ProjBobcat 2.1.2 的 Minecraft 启动器。

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

## 待补充图标

`Assets\launcher.ico` 尚未提供，因此项目文件中的 `ApplicationIcon`
配置暂时注释。后续加入图标后取消注释并重新发布即可。

