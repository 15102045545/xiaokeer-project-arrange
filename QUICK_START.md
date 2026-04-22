[![返回](./assets/home-back-orange.svg)](./README.md)

## 快速开始

### 0) 你会得到什么
- 一个 Windows WPF 测试台：包含 Config / Tasks / Repos / P2P / Secret Scan / Update / AI 等页，能直接跑通“仓库注册表、批处理任务、LAN P2P、开源化 dry-run”等闭环。
- 一个 CLI：用于脚本化执行（例如 update-check / p2p-send 等）。
- 一个 UpdateFeedServer：提供 `GET /api/update/latest` 的最小更新源接口（当前只负责返回最新版本信息）。

### 1) 环境依赖（最小）
- Windows 10/11
- .NET SDK 8.x

可选工具（不装也能启动，但对应功能会提示缺失）：
- git（用于 Git 状态/操作）
- gh（用于 GitHub 登录态与部分操作）
- gitleaks（用于敏感信息扫描）
- python（用于 AI 混合架构本地 server）

### 2) 一键构建（推荐）

在项目根目录打开 PowerShell：

```powershell
.\scripts\02_build.ps1
```

### 3) 启动桌面端

```powershell
.\scripts\03_run_app.ps1
```

启动后建议先做两件事：
- 打开 Config 页，确认 `UserConfigPath=...appsettings.user.json`，并按需写入 Tools/Ai/P2P 配置后保存
- 打开 Repos 页：Load Registry（或 Discover）后选择 repo，跑一轮 Refresh Git / Scan Secrets / OpenSourceify DryRun

### 4) 启动更新源服务（本机）

```powershell
.\scripts\04_run_update_feed_server.ps1
```

桌面端默认读取 `UpdateFeed:BaseUrl=http://localhost:5123`，Update 页或 CLI 可进行 update-check。
