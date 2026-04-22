[![返回](./assets/home-back-orange.svg)](./README.md)

## 版本发布指南（发布新版本 / 更新源 / 安装包）

本项目当前提供两种“可交付产物”：
- 便携版：`dotnet publish` 输出（可打 zip），适合先跑闭环与内部迭代
- 安装包：Inno Setup（生成 setup.exe），用于更贴近真实安装体验

### 1) 设定版本号

版本号统一放在仓库根目录的 `Directory.Build.props`。

修改版本号（示例：0.2.0）：

```powershell
.\scripts\05_set_version.ps1 -Version 0.2.0
```

### 2) 构建与测试

```powershell
.\scripts\02_build.ps1
```

### 3) 生成发布产物（publish）

默认发布为 win-x64，自包含、单文件：

```powershell
.\scripts\06_publish.ps1 -Runtime win-x64
```

产物输出目录：
- `dist/{version}/app-win-x64`
- `dist/{version}/cli-win-x64`
- `dist/{version}/updatefeedserver-win-x64`

### 4) 生成便携版 zip

```powershell
.\scripts\07_make_zip.ps1 -Runtime win-x64
```

输出：
- `dist/{version}/ProjectArrange-{version}-win-x64.zip`

### 5) 生成 EXE 安装包（setup.exe）

依赖：安装 Inno Setup 6（安装后应存在 `ISCC.exe`）。

```powershell
.\scripts\08_build_inno_installer.ps1 -Runtime win-x64
```

输出目录：
- `dist/{version}/installer`

### 6) 发布更新源（UpdateFeedServer）

UpdateFeedServer 目前是“最小更新源服务”，只负责返回最新版本信息：

`GET /api/update/latest` -> `{ latestVersion, downloadUrl }`

你需要把以下两项改成实际最新版本与下载地址：
- `UpdateFeed:LatestVersion`
- `UpdateFeed:DownloadUrl`

文件位置：
- `tools/ProjectArrange.UpdateFeedServer/appsettings.json`

部署 UpdateFeedServer（示例：本机运行）：

```powershell
.\scripts\04_run_update_feed_server.ps1
```

客户端侧（桌面端/CLI）通过配置 `UpdateFeed:BaseUrl` 指向该服务。

说明：
- 当前客户端只做“检查更新信息”，没有做“自动下载/安装/热更新”
- 如果要形成完整自更新闭环，下一步建议选定“安装器方案 + 签名方案 + 下载/安装触发策略”
