# PCL 整合包更新器

基于 **WinUI 3** 的《我的世界》整合包自动更新工具，配合 [Plain Craft Launcher（PCL 启动器）](https://github.com/Hex-Dragon/PCL2) 的 `modpack.zip` 自动安装机制使用。

## 工作原理

PCL 启动器有一个内置钩子：**当 `PCL.exe` 同级目录下存在 `modpack.zip` 时，启动 PCL 会自动识别并提示安装该整合包**。

本程序就是围绕这个机制工作：

1. 把本程序 `PclModpackUpdater.exe` 放到 `PCL.exe` 同级目录（或启动后在设置里指定 PCL 目录）；
2. 填写整合包的下载直链（支持多镜像，每行一个，按顺序尝试）；
3. 点击「检查更新」→「下载整合包」，程序会把最新整合包下载为 PCL 目录下的 `modpack.zip`（先写 `.part` 临时文件，校验通过后再落盘）；
4. 启动 PCL，即可自动提示安装最新整合包。

## 功能

- 多镜像下载直链，自动按顺序重试
- 可选的 `version.json` 版本清单：只在远端版本变化时才下载整包，避免重复下载
- SHA256 校验，防止下载损坏
- 下载进度显示（大小 / 速度 / 百分比），可取消
- 本地状态一目了然：modpack.zip 是否存在、上次下载的版本、是否检测到 PCL
- WinUI 3 + Mica 材质，浅色 / 深色 / 跟随系统主题
- 可选：下载完成后自动启动 PCL

## version.json 格式（可选）

把清单放在任意可访问的位置（如 GitHub 仓库文件或对象存储）：

```json
{
  "version": "1.2.0",
  "sha256": "整合包zip的SHA256（大写或小写均可）",
  "sizeBytes": 123456789,
  "notes": "本次更新说明"
}
```

填写清单地址后，程序会先比对 `sha256` / `version` / 大小，只有远端有变化才会下载；未填写时回退用 HTTP 头（ETag / Last-Modified / Content-Length）判断。

> 提示：可以把整合包直接挂到本仓库的 GitHub Releases，直链形如
> `https://github.com/<用户>/<仓库>/releases/latest/download/modpack.zip`。

## 本地构建

需要 Windows 10 1809+ 与 .NET 8 SDK：

```bash
dotnet build src/PclModpackUpdater/PclModpackUpdater.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64
```

产物位于 `src/PclModpackUpdater/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/`。

推送 `v*` 标签时，GitHub Actions 会自动构建并发布 win-x64 压缩包到 Releases。

## 许可证

[MIT](LICENSE)
