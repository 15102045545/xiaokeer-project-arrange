# xiaokeer-project-arrange

## 项目介绍

xiaokeer项目生态中,整理其它项目的项目,可以让1个混乱的项目变成xiaokeer开发规范的项目，最终达到github可开源的程度
截至目前xiaokeer电脑中大大小小的有几十个项目，它们都不符合xiaokeer项目的开发范式，无法开源，无法共享，无法形成生态。
因此，此项目应运而生

当前阶段定位：以“基建 + 测试台/后台页”的方式，把核心闭环先跑通（注册表、任务中心、开源化 dry-run、LAN P2P、自动同步、AI 混合架构），再逐步收口成真正的业务产品流程。

## 核心功能

### 📚 文档导航

| 名称                                          | 
|---------------------------------------------|
| [📖 操作指南](./INSTRUCTIONS.md)                |
| [❓ 常见问题](./FAQ.md)                          |
| [🚀 快速开始](./QUICK_START.md)                 |
| [💻 开发者文档](./DEVELOPMENT_DOC.md)            |
| [📄 版本发布指南](./RELEASE.md)                   |
| [📄 许可证](./LICENSE.txt)                     |

### 🧰 常用脚本

```powershell
.\scripts\02_build.ps1
.\scripts\03_run_app.ps1
.\scripts\06_publish.ps1 -Runtime win-x64
.\scripts\08_build_inno_installer.ps1 -Runtime win-x64
```
