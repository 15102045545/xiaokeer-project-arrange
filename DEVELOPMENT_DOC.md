[![返回](./assets/home-back-orange.svg)](./README.md)

## 开发者文档（环境配置 / 启动 / 调试 / 目录结构）

### 1) 运行环境要求
- Windows 10/11
- .NET SDK 8.x

建议安装（提升开发体验）：
- Visual Studio 2022（WPF 调试更方便）或 Rider
- PowerShell 5.1+（或 PowerShell 7）

可选外部工具（缺失时对应页面会提示）：
- git
- gh
- gitleaks
- python

### 2) 项目结构（关键目录）
- `src/ProjectArrange.App`：WPF 桌面端（测试台）
- `src/ProjectArrange.Cli`：CLI 工具（脚本化入口）
- `src/ProjectArrange.Core`：领域模型与抽象（interfaces/models/result）
- `src/ProjectArrange.Infrastructure`：实现层（git/gh/github/p2p/sqlite/updates/openSourceify 等）
- `tools/ProjectArrange.UpdateFeedServer`：更新源服务（最小 HTTP 接口）
- `tests/ProjectArrange.Tests`：单元测试
- `scripts`：一键构建/发布/打包/图标生成脚本

### 3) 配置体系（配置文件与优先级）
应用启动时会加载（后加载覆盖前加载）：
1. `AppContext.BaseDirectory/appsettings.json`
2. `AppContext.BaseDirectory/appsettings.{ENV}.json`
3. `%AppData%/ProjectArrange/appsettings.user.json`

桌面端与 CLI 都支持 user 覆盖层，路径会在 Config 页显示为 `UserConfigPath=...`。

### 4) 机密信息（Secrets）与配置边界
- GitHub token 等敏感数据：走 DPAPI 本地加密存储（不写入 appsettings）
- 工具路径/功能开关/AI endpoint 等：走 `appsettings.user.json`

### 5) 常用命令（脚本）

生成图标（也会在 build/publish 时自动调用）：

```powershell
.\scripts\01_generate_icons.ps1
```

一键构建（含 tests）：

```powershell
.\scripts\02_build.ps1
```

启动桌面端：

```powershell
.\scripts\03_run_app.ps1
```

启动更新源服务：

```powershell
.\scripts\04_run_update_feed_server.ps1
```

### 6) 工具路径配置（Tools）
如果你的 git/gh/gitleaks/python 不在 PATH，或你想固定某个版本，可以在 `appsettings.user.json` 配置：

```json
{
  "Tools": {
    "git": "C:\\Program Files\\Git\\bin\\git.exe",
    "gh": "C:\\Program Files\\GitHub CLI\\gh.exe",
    "gitleaks": "D:\\tools\\gitleaks.exe",
    "python": "C:\\Python312\\python.exe"
  }
}
```

### 7) P2P 自动同步开关（RepoRegistry Sync v0）

```json
{
  "P2pRegistrySync": {
    "Enabled": true,
    "IntervalSeconds": 15
  }
}
```

说明：
- 仅对“已信任 + 可发现”的 peer 生效
- 机制是 digest -> snapshot：先发摘要，不一致才发快照；收到快照会自动 Import 合并
