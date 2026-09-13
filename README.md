# YCombatEditor

基于 Unity 原生 API 的动作编辑器与播放核心。当前版本 **0.2.0-preview.2**，编辑器界面为英文，使用手册与 roadmap 为中文。仓库同时提供完整源码和 UPM 安装包，角色与怪物共用一套动作时间轴。

## 获取源码或安装包

- **源码**：仓库根目录即 UPM 包，包含 Runtime、Editor、Integrations、Tests 和 Samples~。保留所有 .meta 文件。
- **Release 文件**：见 [Releases~](Releases~/)。每个版本提供 TGZ、*-source.zip、SHA256SUMS.txt、inventory.json、RELEASE-NOTES.zh-CN.md 和 validation.json。
- **安装 TGZ**：Unity Package Manager → **Install package from tarball**，选择 .tgz，无需解压；不是 .unitypackage。
- **本地源码安装**：Package Manager → **Install package from disk**，选择本仓库 package.json；或复制到项目 Packages/com.ethan.act-action-editor。
- **Git 安装**：仓库地址为 https://github.com/YarronE/YCombatEditor.git 。上传并验证版本标签后可使用 https://github.com/YarronE/YCombatEditor.git#v0.2.0-preview.2；本地制品存在不代表在线标签或 GitHub Release 已创建。

最低声明 Unity 6000.0，验证基线为 **6000.3.6f1**。其他 Unity 版本、可选集成和项目适配需分别验证。

## 能做什么

- 同一时间轴分成 Presentation 与 Mechanics：统一编辑动画、Effect、Camera、位移/朝向、攻击、派生、取消与交互窗口。
- Effect 新增时选择 Visual Effect 或 Audio，只显示对应属性；保留旧混合事件的数据。
- Motion 中分别配置 Displacement、Allow target tracking 与 Lock facing；可用于 Boss 追踪目标与落脚阶段锁定。
- Unity Undo/Redo、资源校验定位、备份迁移、独立预览和可扩展运行时事件。

它是动作创作与播放核心。伤害结算、输入、AI、目标解析和游戏专属表现通过宿主接收器实现，不提供完整角色控制器或成品战斗游戏。核心无 Animancer / Feel / TrailsFX 硬依赖；Unity 引擎模块依赖列在 package.json，可选集成不计入核心。现有代码来源核对仍在进行，不能将无插件依赖表述为百分之百原创认证。

## 上手与文档

1. 安装包，打开 **Tools > ACT Action Editor > Open Editor**。
2. **New** 创建动作，指定 **Preview actor** 与 **Clip source**。
3. 在轨道 **Add** 添加事件，选择事件编辑属性，通过 **Issues** 检查，再 **Save**。
4. 需要示例时，在 Package Manager 导入 **Playback and Interaction Windows**，使用导入后的 ACT 示例生成菜单。

- [中文使用手册：轨道、按钮、快捷键和示例](Documentation~/UserGuide.zh-CN.md)
- [更新 roadmap](ROADMAP.zh-CN.md)
- [Demo 通用改进同步、构建与发布流程](Documentation~/Maintenance.zh-CN.md)
- [技术接入与 Agent 指南](Documentation~/AgentGuide.md)
- [发布检查与当前限制](Documentation~/ReleaseChecklist.zh-CN.md)
- [更新记录](CHANGELOG.md)

## 维护方式

通用能力在动作 Demo 的 Embedded 包中开发，完成测试并同步中文手册后更新独立仓库；游戏专属资产、插件和 Demo Git 历史不复制。独立仓库的修复先回移到同一开发源。源码可持续同步，Release 安装包按版本保留，禁止用不同字节覆盖旧版本。

Tools~/Build-Release.ps1 可独立构建源码 ZIP 与 TGZ；Tools~/Verify-Release.ps1 在新建临时 Unity 工程验证实际安装包，并可构建示例 Windows Player。静态 GitHub CI 不等于 Unity 测试；精确测试结果见各版本 validation.json。

MIT License，Copyright (c) 2026 Ethan。示例不附带游戏角色、商店插件或第三方美术；引擎及其他外部资源遵循各自许可。
