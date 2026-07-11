# BiliBiliToolPro

[![License](https://img.shields.io/github/license/kizunerwe/BiliBiliToolPro?style=flat-square)](LICENSE)

这是基于 [RayWangQvQ/BiliBiliToolPro](https://github.com/RayWangQvQ/BiliBiliToolPro) 继续维护的 fork，主要用于在青龙面板中运行 B 站自动任务。

当前优先维护青龙脚本、Console 任务链路、登录态持久化、每日任务和相关安全性。Web 仅做基础兼容；Docker、Podman、Helm、Krew 等上游遗留部署方式不作为主要支持范围。

## 为什么维护这个 fork

我自己仍有继续使用和调整这个项目的需求，但上游稳定分支已经较长时间没有发布新版本，一些实际使用中遇到的问题也需要有人处理，所以干脆 fork 后按照自己的使用情况继续维护。

目前没有复杂的路线图。人走茶凉，唯独我还在这里自娱自乐（？）。截至目前，这个 fork 还没有收到 Star，也没有收到其他用户正在使用的明确反馈，因此维护重心会优先放在我自己的实际需求上，暂时不会为了假想中的大众需求，专门投入大量时间做通用化和场景覆盖。

现阶段先保证青龙和 Console 能正常运行，遇到明确的问题就修；如果确实有人在用，有实际需求也可以提 Issue。B 站接口和风控经常变化，与其为了增加功能而增加功能，不如先把现有任务做稳、把账号行为做得更自然。至于以后还能增加什么，暂时没有定死，能稳定使用最重要。

## 接手后的主要维护

这个 fork 不只包含最近新增的功能。接手后已经围绕实际运行中遇到的问题做过这些调整：

- **青龙运行链路**：仓库脚本默认指向当前 fork；加固安装、更新、下载重试、并发锁和失败退出；单个账号失败后继续执行其他账号，并向青龙正确返回最终状态。
- **多账号与凭据**：改进 Cookie 备注和登录日志；支持按账号保存 `access_key`；对凭据日志进行脱敏，并使用原子写入降低状态文件损坏的概率。
- **投币选源**：整理配置 UP、特别关注、普通关注和排行榜等候选来源；增加失败回退、重复候选规避、排行榜缓存和中断后的进度恢复。
- **直播任务**：修复多账号直播 Cookie 回退、直播心跳兼容和天选时刻房间扫描等问题。
- **大会员积分**：修复剧集观看任务，补充 TV 登录、按账号使用 `access_key` 和相关请求流程。
- **测试与验证**：补充投币、登录、直播、大会员积分等行为测试，并增加青龙脚本专项验证流程。
- **UP 友好模式**：在完成投币任务前增加实际经过时间的播放会话，并提供点赞和独立收藏夹收藏开关，尽量减少只操作互动、不产生账号播放记录的情况。

这些功能依赖 B 站当前接口和服务端规则，不代表永久可用。接口相关修改会尽量以实际请求、HAR 或可复现实验为依据，不凭记忆直接调整定义。

## 风险提示

- 本项目仅供学习、研究和个人自用。自动化行为可能受到平台规则、接口变化和账号风控影响，使用者需自行承担风险。
- Cookie、`access_key` 等登录凭据会保存在本地配置或青龙环境变量中。请勿上传、公开或提交到 Git 仓库。
- 不建议高频运行、并发运行相同任务，或在不清楚影响时开启大量写操作。
- 与 B 站服务器交互的接口可能随时变化；未经实际验证的接口和字段不应凭经验修改。

## 快速开始

### 青龙面板（推荐）

请按照 [青龙部署教程](qinglong/README.md) 配置拉库、登录和定时任务。

仓库地址：

```text
https://github.com/kizunerwe/BiliBiliToolPro.git
```

首次使用时，运行青龙中的扫码登录任务。登录成功后，程序可以通过青龙 OpenAPI 将 Cookie 持久化到环境变量。

### 本地或服务器运行

本地调试和排障可参考 [本地运行说明](docs/runInLocal.md)。Console 也是青龙运行方式的基础入口。

### 其他部署方式

以下方式来自上游历史实现，当前未持续验证，不保证文档、镜像或安装脚本仍然可用：

- [Docker / Podman](docker/README.md)
- [Helm](helm/README.md)
- [Krew](krew/README.md)
- [Web](src/Ray.BiliBiliTool.Web)

## 当前重点功能

| 任务 | Code | 说明 |
| --- | --- | --- |
| 扫码登录 | `Login` | 初始化或更新 Cookie，并支持青龙环境变量持久化 |
| 每日任务 | `Daily` | 登录、观看、分享、投币等每日任务 |
| 登录检查 | `Test` | 只检查 Cookie 是否有效，不执行点赞、收藏或投币 |

### UP 友好模式

开启后，每个待投币视频会先按实际经过时间发送播放心跳，再执行收藏、投币和联动点赞：

- 短视频默认看到结束。
- 长视频默认观看 60 秒。
- 每约 15 秒上报一次播放进度。
- 不下载完整音视频媒体。
- 可按开关收藏到独立收藏夹，不移动或删除账号原有收藏。
- 观看失败时跳过当前视频；收藏失败时记录日志并继续投币。

青龙环境变量示例：

```bash
Ray_DailyTaskConfig__IsUpFriendlyMode=true
Ray_DailyTaskConfig__UpFriendlyWatchSeconds=60
Ray_DailyTaskConfig__SelectLike=true
Ray_DailyTaskConfig__SelectFavorite=false
Ray_DailyTaskConfig__FavoriteFolderName=BiliBiliToolPro-UP支持
```

该模式已经验证可以在账号观看历史中形成对应视频和播放进度，但无法保证 UP 后台采用完全相同的统计口径。

## 上游遗留功能

仓库仍保留直播、漫画、大会员权益、批量取关、充电等上游任务。这些功能不是当前主要维护范围，可能受接口变化影响；使用前请查看日志并进行小范围验证。

## 配置与多账号

完整配置说明见 [配置文档](docs/configuration.md)。

青龙账号环境变量从 `0` 开始编号：

```text
Ray_BiliBiliCookies__0
Ray_BiliBiliCookies__1
Ray_BiliBiliCookies__2
```

本地运行时通常使用 `cookies.json`：

```json
{
  "BiliBiliCookies": [
    "第一个账号的 Cookie",
    "第二个账号的 Cookie"
  ]
}
```

真实 Cookie 文件已被 Git 忽略，但仍应在提交前检查 `git status`，避免误上传凭据。

项目支持多种日志推送渠道，具体配置请查看 [配置文档](docs/configuration.md)。

## 验证与开发

常用验证命令：

```powershell
./scripts/ut.ps1
```

真实 B 站接口测试默认不进入普通验证。如需手动执行：

```powershell
./scripts/ut.ps1 -External
```

青龙 Shell 保护检查：

```bash
bash scripts/test-qinglong-base.sh
```

## 问题反馈

提交问题前请先：

1. 更新到最新 `main`。
2. 查看 [青龙文档](qinglong/README.md)、[配置文档](docs/configuration.md) 和 [常见问题](docs/questions.md)。
3. 确认问题可以稳定复现，并提供脱敏后的任务日志、运行方式和配置项。

请通过 [Issues](https://github.com/kizunerwe/BiliBiliToolPro/issues) 反馈。青龙和 Console 相关问题优先处理，其他部署方式视时间和可复现条件处理。

## 贡献

- 基于 `main` 创建功能分支。
- 修改功能时补充或更新测试。
- 不提交 Cookie、`access_key`、日志中的完整凭据或个人数据。
- 与 B 站接口相关的修改必须基于实际请求或可复现实验，不凭记忆调整 URL、DTO 或请求字段。
- 完成后向 `main` 提交 Pull Request。

文档修正同样欢迎提交。

## 上游、参考与许可证

- 上游项目：[RayWangQvQ/BiliBiliToolPro](https://github.com/RayWangQvQ/BiliBiliToolPro)
- B 站 API 资料：[SocialSisterYi/bilibili-API-collect](https://github.com/SocialSisterYi/bilibili-API-collect)
- 许可证：[GPL-3.0](LICENSE)
