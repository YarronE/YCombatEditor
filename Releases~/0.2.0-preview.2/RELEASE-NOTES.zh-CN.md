# 0.2.0-preview.2 本地预览 Release

日期：2026-09-13。仓库：YarronE/YCombatEditor。包：com.ethan.act-action-editor。许可：MIT，Ethan。

## 本次内容

- 独立编辑器源码与版本化 UPM 安装包，保留原脚本 GUID 与包标识。
- 单一时间轴内区分 Presentation / Mechanics，支持角色与怪物共用配置。
- Effect 新增 Visual Effect / Audio 子类型选择与专属属性视图；保留旧混合事件。
- Camera 轨道与 Motion 中的位移/目标追踪/朝向锁定配置。
- 中文使用手册、中文 roadmap、持续同步约定以及独立构建/验证/同步脚本。

## 安装文件

- com.ethan.act-action-editor-0.2.0-preview.2.tgz：在 Unity Package Manager 选择 Install package from tarball，无需解压。
- com.ethan.act-action-editor-0.2.0-preview.2-source.zip：完整包源码，可用于阅读、修改或本地源码安装。
- inventory.json：逐文件源码 SHA-256。
- SHA256SUMS.txt：本目录制品与说明文件的 SHA-256。
- validation.json：最终 TGZ 的安装、测试和示例构建结果。

测试基线 Unity 6000.3.6f1。验证范围包括 UPM tarball 安装、包回归、导入基础播放与交互示例，以及 Windows Player 构建。精确数量与通过状态见 validation.json。Player 构建用于验证示例兼容性；编辑器安装文件是 TGZ。

## 当前边界

本地预览文件已准备不等于 GitHub 标签、Release 或下载链接已经上线。真实窗口视觉验收、既有代码来源与发布权核对、固定 Git 标签安装及可选集成矩阵仍需完成。预览包没有包含游戏 Demo 的资产、商店插件或 Git 历史；无第三方插件硬依赖不等于全部代码原创认证。

完整伤害、玩家输入、敌人 AI、目标与游戏专属表现需要消费项目接入。升级前备份动作资源，不自动批量迁移旧资产。后续通用修改先进入 Demo 中的 Embedded 包并同步源码；新 Release 提升版本，旧制品保留。
