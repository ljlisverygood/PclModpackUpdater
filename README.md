# PCL 整合包更新器

基于 **WinUI 3** 的《我的世界》整合包自动更新方案，包含：

- **客户端**（`src/`）：WinUI 3 桌面应用，一键从更新源拉取最新整合包；
- **后端**（`server/`）：ASP.NET Core 更新源服务，托管整合包与版本信息，提供管理接口发布新版本。

配合 [Plain Craft Launcher（PCL 启动器）](https://github.com/Hex-Dragon/PCL2) 的 `modpack.zip` 自动安装机制使用：**当启动器主程序（`Plain Craft Launcher.exe`）同级目录下存在 `modpack.zip` 时，启动 PCL 会自动识别并提示安装该整合包**。

客户端是独立应用，可安装在任意位置，无需放进 PCL 目录。

## 完整流程（一键更新）

1. 在服务器上部署后端并发布整合包（见下文「后端」）；
2. 客户端首次使用时选择 `Plain Craft Launcher.exe` 所在文件夹（只需一次，之后记住）；
3. 「更新源」填后端地址（如 `http://服务器:8080`）；
4. 点「**一键更新**」：检查版本 → 有新版自动下载（SHA256 校验、进度显示、可取消）→ 保存为 PCL 目录下的 `modpack.zip`；
5. 启动 PCL，自动提示安装最新整合包（也可在设置里开启「下载完成后自动启动 PCL」）。

## 客户端功能

- 独立安装，PCL 目录可自由选择并记忆
- 自动识别启动器：优先 `Plain Craft Launcher.exe` / `PCL.exe`，目录中有其他 exe 时也可使用
- 更新源支持两种写法：后端服务地址（裸地址自动展开为 API），或 zip 直链（可多镜像按顺序重试）
- 可选的 `version.json` 版本清单：只在远端版本变化时才下载整包
- SHA256 校验，防止下载损坏；`.part` 临时文件原子落盘
- 下载进度（大小 / 速度 / 百分比）、可取消
- WinUI 3 + Mica 材质，浅色 / 深色 / 跟随系统主题

## 后端：更新源服务

轻量 ASP.NET Core 服务（`server/PclModpackUpdater.Server`）：

| 接口 | 说明 |
| --- | --- |
| `GET /api/version` | 最新版本信息（version、sha256、sizeBytes、notes） |
| `GET /api/download` | 下载最新整合包（支持断点续传与 HEAD 探测） |
| `POST /api/admin/publish` | 上传 zip 发布新版本（multipart：file、version、notes） |
| `POST /api/admin/publish-from-url` | 服务端从指定 URL 拉取并发布（JSON：url、version、notes） |

管理接口需要请求头 `X-Admin-Token` 与服务端令牌一致（常数时间比较）；未配置令牌时管理接口整体禁用。

**SSRF 防护**：`publish-from-url` 仅允许 http/https，请求前校验主机并拒绝 localhost、环回、私有和保留地址（含 IPv4 映射 IPv6、ULA、CGNAT、链路本地等），且禁止重定向。

### 部署

```bash
# 方式一：直接运行（需要 .NET 8 Runtime）
PCLUPDATER_ADMIN_TOKEN=你的管理令牌 dotnet PclModpackUpdater.Server.dll --urls http://0.0.0.0:8080

# 方式二：Docker
docker build -t pcl-updater-server server/PclModpackUpdater.Server
docker run -d -p 8080:8080 -e PCLUPDATER_ADMIN_TOKEN=你的管理令牌 -v pcl-data:/data pcl-updater-server
```

发布新版本：

```bash
# 上传 zip
curl -X POST -H "X-Admin-Token: 令牌" \
  -F "file=@modpack.zip" -F "version=1.2.0" -F "notes=更新说明" \
  http://服务器:8080/api/admin/publish

# 或让服务端从 URL 拉取
curl -X POST -H "X-Admin-Token: 令牌" -H "Content-Type: application/json" \
  -d '{"url":"https://example.com/modpack.zip","version":"1.2.0"}' \
  http://服务器:8080/api/admin/publish-from-url
```

### 不想自己部署？

客户端也支持纯静态源：把整合包挂到任意直链（如本仓库 Releases：`https://github.com/ljlisverygood/PclModpackUpdater/releases/latest/download/modpack.zip`），配合可选的 `version.json` 即可，无需后端。

## version.json 格式（静态源可选）

```json
{
  "version": "1.2.0",
  "sha256": "整合包zip的SHA256",
  "sizeBytes": 123456789,
  "notes": "本次更新说明"
}
```

## 安装客户端

从 [Releases](https://github.com/ljlisverygood/PclModpackUpdater/releases) 下载：

- **`PclModpackUpdater-Setup-x.x.x.exe`**：Inno Setup 安装包（简体中文安装界面）；
- **`PclModpackUpdater-win-x64.zip`**：绿色版，解压即用。

> 安装目录下会有大量以语言代码命名的文件夹（`en-US`、`zh-CN`、`ja-JP` 等），这是 WinUI 运行库自带的多语言资源，请勿删除。

## 本地构建

需要 Windows 10 1809+ 与 .NET 8 SDK：

```bash
# 客户端
dotnet publish src/PclModpackUpdater/PclModpackUpdater.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64

# 后端
dotnet publish server/PclModpackUpdater.Server/PclModpackUpdater.Server.csproj -c Release
```

## 本地打安装包（Inno Setup 6）

1. 安装 [Inno Setup 6.5+](https://jrsoftware.org/isdl.php)；
2. 把客户端 publish 输出复制到仓库根目录的 `publish\` 文件夹；
3. 执行 `ISCC.exe installer\PclModpackUpdater.iss`，产物在 `installer\dist\`。

## 版本号规则与自动发布

- **版本号规则**：常规更新 +0.0.1（patch，如 1.0.0 → 1.0.1）；大功能更新 +0.1（minor，如 1.0.1 → 1.1.0）；
- **发新版**：运行 `powershell -File scripts\bump.ps1 patch`（大功能用 `minor`），把版本号变更提交并推送到 `main`；
- 推送到 `main` 后**自动发布 Release**：工作流读取 csproj 的 `<Version>`，该版本尚未发布时自动构建并创建 Release（Inno Setup 安装包 + 客户端绿色版 zip + 后端 zip），已发布过则自动跳过；
- 也可在 Actions 页面手动触发（workflow dispatch）。

## 许可证

[MIT](LICENSE)
