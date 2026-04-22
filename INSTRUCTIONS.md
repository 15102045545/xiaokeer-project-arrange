[![返回](./assets/home-back-orange.svg)](./README.md)

## 操作指南（测试台使用说明）

### Config
用于统一管理“全局配置 + AI 配置 + 工具路径 + P2P 同步开关”等。

- `Load User Config`：读取 `%AppData%/ProjectArrange/appsettings.user.json`（不存在会给空模板）
- `Save User Config`：保存 user config（JSON 必须合法）
- `Refresh Effective`：展示当前进程实际生效的关键配置摘要

### Tasks
任务中心页面，用于观察/调试所有批处理任务：
- 查看任务列表（State/Kind/Name/Target/Attempts）
- 选中任务后查看任务详情与日志（含 ctx.Log）
- 统一队列控制：并发、暂停、重试失败、清理完成、取消选中任务

说明：
- 去重：同一个 `dedupeKey` 的 queued/running 任务会复用同一个任务 id
- 串行：同一个 repo（groupKey=repoPath）任务会自动互斥执行，避免并发写同一 repo

### Repos
“产品后台式”的仓库管理测试页：
- `Discover`：从 Roots 扫描本地 git repo 并写入 RepoRegistry
- `Load Registry`：从 RepoRegistry 载入已记录仓库
- `Refresh Git`：批量入队 git status（结果会写入 registry snapshot）
- `Scan Secrets`：批量入队 gitleaks 扫描（结果会写入 registry snapshot）
- `OpenSourceify DryRun`：批量生成开源化 plan（EnsureFile / EnsureGitignore / DetectBadTrackedFiles）
- `Update GitHub`：批量更新 GitHub 仓库设置（描述、可见性）

右侧 Details 会展示选中 repo 的最新快照与 dry-run plan。

### AI
AI 混合架构最小闭环测试页：
- `Start Python Server`：按 `Ai:Endpoint` 启动内置 python server（echo 版本，用于打通链路）
- `Send`：C# 侧通过 `IAiService`（Python HTTP bridge）请求 `/v1/chat`

说明：当前 python server 只是回显（用于确认通路/配置/协议），后续可以替换为真实 LLM（本地/远程）。

### Update
更新机制测试页（当前是最小版）：
- 客户端通过 `UpdateFeed:BaseUrl/api/update/latest` 获取 `latestVersion/downloadUrl`
- UpdateFeedServer 只负责返回最新版本信息；“下载与安装”还未做成自动化（后续可以接入安装器或自更新器）
