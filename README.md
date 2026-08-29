# PCL 整合包更新器

基于 **WinUI 3** 的《我的世界》整合包自动更新工具，配合 [Plain Craft Launcher（PCL 启动器）](https://github.com/Hex-Dragon/PCL2) 的 `modpack.zip` 自动安装机制使用。

**本程序是独立应用，可安装在任意位置，无需放进 PCL 目录**——首次使用时选择 `PCL.exe` 所在文件夹即可。

## 工作原理

PCL 启动器有一个内置钩子：**当启动器主程序（`Plain Craft Launcher.exe`）同级目录下存在 `modpack.zip` 时，启动 PCL 会自动识别并提示安装该整合包**。

本程序围绕这个机制工作：

1. 安装并启动本程序，选择 `Plain Craft Launcher.exe` 所在文件夹（只需选一次，之后会记住）；
2. 填写整合包的下载直链（支持多镜像，每行一个，按顺序尝试）；
3. 点击「检查更新」→「下载整合包」，程序会把最新整合包下载为 PCL 目录下的 `modpack.zip`（先写 `.part` 临时文件，校验通过后再落盘）；
4. 启动 PCL，即可自动提示安装最新整合包。

## 安装

从 [Releases](https://github.com/ljlisverygood/PclModpackUpdater/releases) 下载：

- **`PclModpackUpdater-Setup-x.x.x.exe`**：Inno Setup 安装包（简体中文安装界面），双击安装，可选创建桌面图标；
- **`PclModpackUpdater-win-x64.zip`**：绿色版，解压即用。

> 安装目录下会有大量以语言代码命名的文件夹（`en-US`、`zh-CN`、`ja-JP` 等），这是 WinUI 运行库自带的多语言资源，请勿删除。卸载时安装程序会一并清理。

## 功能

- 独立安装，PCL 目录可自由选择并记忆
- 自动识别启动器：优先 `Plain Craft Launcher.exe` / `PCL.exe`，目录中有其他 exe 时也可使用
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
dotnet publish src/PclModpackUpdater/PclModpackUpdater.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64
```

产物位于 `src/PclModpackUpdater/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/publish/`。

## 本地打安装包（Inno Setup 6）

1. 安装 [Inno Setup 6.5+](https://jrsoftware.org/isdl.php)；
2. 把 publish 输出复制到仓库根目录的 `publish\` 文件夹；
3. 执行：

```bash
ISCC.exe installer\PclModpackUpdater.iss
```

生成的安装包在 `installer\dist\PclModpackUpdater-Setup-*.exe`。

## CI / 自动发布

- 推送到 `main`：GitHub Actions 自动构建并上传构建产物；
- 推送 `v*` 标签（如 `v1.0.0`）：自动 publish → 打包 Inno Setup 安装包 → 连同绿色版 zip 一起发布到 Releases。

## 许可证

[MIT](LICENSE)
