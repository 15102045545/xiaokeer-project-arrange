[![返回](./assets/home-back-orange.svg)](./README.md)

## 常见问题

### 1) 为什么提示 git/gh/gitleaks/python 不存在？
这些工具默认按以下顺序定位：
- 先看 `appsettings.user.json` 的 `Tools:{name}` 是否指定了绝对路径
- 否则走 PATH（以及少量内置探测）

建议在 Config 页写入 Tools 配置并保存。

### 2) `appsettings.user.json` 在哪里？
默认：
- `%AppData%/ProjectArrange/appsettings.user.json`

Config 页会显示 `UserConfigPath=...`，以实际为准。

### 3) GitHub token 存在哪？会不会写进配置文件？
不会。GitHub token 等敏感信息走 DPAPI 加密存储（CurrentUser scope），不写入 appsettings。

### 4) P2P 为什么发现得到但连不上？
P2P 连接使用 mTLS，并且只允许“已信任”的证书指纹连接：
- 先在 P2P 页生成 pairing code
- 在另一台机器导入 pairing code 完成信任
- 信任后才会自动连接/传输

### 5) P2P 自动同步（RepoRegistry）为什么不生效？
检查：
- `P2pRegistrySync.Enabled=true`
- 两台机器互相“信任”且在同一 LAN 可互相发现
- 两边都启动了桌面端（HostedService 会在 App 进程里运行）

### 6) UpdateFeedServer 是不是“自动更新”？
目前不是完整自动更新，只是最小版本信息服务：
- `GET /api/update/latest` 返回 `latestVersion/downloadUrl`
- 客户端可检查“是否有更新”
- “下载 + 安装 + 重启替换”后续再补（需要选定安装器/签名/替换策略）
