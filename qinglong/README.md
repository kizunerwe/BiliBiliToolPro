# 在青龙中运行

原理是，利用青龙的拉库命令，拉取本仓库源码，自动添加cron定时任务，然后在青龙容器中安装`dotnet`环境或`bilitool`的二进制包，定时运行相应的Task。

开始前，请先确保你的青龙面板是运行正常的。

<!-- TOC depthFrom:2 -->

- [1. 步骤](#1-步骤)
- [2. UP友好模式（可选）](#2-up友好模式可选)
- [3. GitHub 加速](#3-github-加速)
- [4. 常见问题](#4-常见问题)

<!-- /TOC -->

## 1. 步骤

### 1.1. 登录青龙面板并修改配置
青龙面板，`配置文件`页。

修改 `RepoFileExtensions="js py"` 为 `RepoFileExtensions="js py sh"`

保存配置。

### 1.2. 在青龙面板中添加拉库定时任务

两种方式，任选其一即可：

#### 1.2.1. 方式一：订阅管理

```
名称：Bilibili
类型：公开仓库
链接：https://github.com/kizunerwe/BiliBiliToolPro.git
定时类型：crontab
定时规则：2 2 28 * *
白名单：bili_task_.+\.sh
文件后缀：sh
```

没提到的不要动。

保存后，点击运行按钮，运行拉库。

#### 1.2.2. 方式二：定时任务拉库
青龙面板，`定时任务`页，右上角`添加任务`，填入以下信息：

```
名称：拉取Bili库
命令：ql repo https://github.com/kizunerwe/BiliBiliToolPro.git "bili_task_"
定时规则：2 2 28 * *
```

点击确定。

保存成功后，找到该定时任务，点击运行按钮，运行拉库。

### 1.3. 检查定时任务

如果正常，拉库成功后，会自动添加bilibili相关的task任务。

![qinglong-tasks.png](../docs/imgs/qinglong-tasks.png)

### 1.4. 配置青龙Client Secret（可选）

扫码登录Bili后，需要有权限向青龙的环境变量中持久化Cookie，所以需要添加一个鉴权。

青龙官方说明：https://qinglong.online/api/preparation

#### 1.4.1. 新建 Application

青龙 -> 系统设置 -> 应用设置，点击新建。

![qinglong-application](../docs/imgs/qinglong-application.png)

#### 1.4.2. 密钥配置到环境变量

将上面2个值添加到环境变量中即可。

Name分别为：

- Ray_QingLongConfig__ClientId
- Ray_QingLongConfig__ClientSecret

![qinglong-app-env](../docs/imgs/qinglong-application-key.png)


### 1.5. Bili登录

在青龙定时任务中，点击运行`bili扫码登录`任务，查看运行日志，扫描日志中的二维码进行登录。
![qinglong-login.png](../docs/imgs/qinglong-login.png)

登录成功后，如果已配置了上述的Application，会将cookie保存到青龙的环境变量中：

![qinglong-env.png](../docs/imgs/qinglong-env.png)

如果未配置Application，会打印出cookie，请手动自己到环境变量中添加。

首次运行会自动安装环境，时间可能长一点，之后就不需要重复安装了。

## 2. UP友好模式（可选）

开启后，每日观看任务和每个待投币视频都会按实际经过时间发送播放心跳：短视频默认看完，长视频默认观看 60 秒，每约 15 秒上报一次进度。该模式不会下载完整音视频媒体；已验证可以形成账号观看历史，但无法保证 UP 后台采用相同统计口径。播放成功后再按现有开关分享、点赞、投币，并可选收藏到独立收藏夹。

在青龙环境变量中添加：

```bash
Ray_DailyTaskConfig__IsUpFriendlyMode=true
Ray_DailyTaskConfig__UpFriendlyWatchSeconds=60
Ray_DailyTaskConfig__SelectFavorite=false
Ray_DailyTaskConfig__FavoriteFolderName=BiliBiliToolPro-UP支持
```

- `SelectLike` 继续控制投币时是否联动点赞。
- `SelectFavorite` 默认关闭；开启后只使用指定的独立收藏夹，不移动或删除账号原有收藏。
- 观看或收藏失败会写入日志；投币前观看失败会跳过该视频，收藏失败仍继续投币。
- 开启后任务耗时会增加。默认配置下，每日观看最多约增加 1 分钟，投 5 枚币最多约增加 5 分钟；实际耗时取决于待投币数量、视频长度和观看秒数配置。

## 3. GitHub 加速

如需使用下载代理，请通过 `BILI_GITHUB_PROXY` 配置可信地址。第三方代理可能失效或存在供应链风险，请自行确认来源。

## 4. 常见问题

### 4.1. 安装 .NET 失败怎么办

首先，青龙有两个版本的镜像：

- alpine：whyour/qinglong:latest
- debian：whyour/qinglong:debian

安装dotnet失败的情况，几乎全发生在alpine版上。。。

所以，如果你“执迷不悟”，就是一定要用alpine版，那请先通过日志自行排查，不行就根据微软官方文档，进入qinglong容器后，手动安装。

如果还不行，那么可以切换到基于`bilitool`的二进制包运行方式，该方式不需要安装`dotnet`，方式：

编辑青龙面板的`配置文件`，新增如下两行：

```
export BILI_MODE="bilitool" # bili运行模式，dotnet或bilitool
export BILI_GITHUB_PROXY="" # 可选：可信的下载代理地址，不使用则留空
```

![qinglong-login.png](../docs/imgs/qinglong-run-as-bilitool.png)

`bilitool` 模式使用 `main` 分支发布的二进制包。

当前任务脚本还会执行以下保护：

- 同一个仓库同一时间只允许一个 BiliBiliTool 任务运行，重复任务会直接退出，避免并发编译、更新和重复操作账号。
- `dotnet` 模式只在源码版本变化时重新发布 Console，日常任务直接运行发布产物，不再每次执行 `dotnet run`。
- 安装、下载或安装后校验失败时，任务会返回失败状态，不再继续运行损坏或缺失的程序。
- 青龙 OpenAPI 保存 Cookie 或 `access_key` 失败时，任务会返回失败；日志不会输出完整 Cookie 或 `access_key`。

本地稳定测试可运行：

```powershell
./scripts/ut.ps1
```

真实 B 站接口测试不会进入普通验证；如需手动运行：

```powershell
./scripts/ut.ps1 -External
```

另外，alpine版的问题，我不建议来提交issue，因为已经大大超出本项目的scope了，建议可以去给alpine官方或微软的dotnet官方提交issue。

### 4.2. Couldn't find a valid ICU package installed on the system

如 #266 ，需要在青龙面板的环境变量添加如下环境变量：

```
名称：DOTNET_SYSTEM_GLOBALIZATION_INVARIANT
值：1
```

### 4.3. 提示文件不存在或路径异常，怎么排查

需要`docker exec -it qinglong bash`后，查看几个常用路径：

```
/ql
    /data
        /repo
    /scripts
    /shell
```

- `/ql/dada/repo`目录下存储了拉库后，bilitool的源代码
- `/ql/scripts`目录下存储了bilitool的定时运行脚本
- `/ql/shell`目录下是青龙的基础脚本

请cd到相应目录，查看该目录下文件是否存在，状态是否正常。

### 4.4. The configured user limit (128) on the number of inotify instances has been reached

报错：

```
Asp.Net Core - The configured user limit (128) on the number of inotify instances has been reached
```

可以尝试添加如下环境变量解决：

```
DOTNET_USE_POLLING_FILE_WATCHER=1
```

添加后，对配置变更事件的监听，会从监听 Linux 系统的 inotify 事件，变成定时轮询。
