# YCombatEditor

YCombatEditor 是面向 Unity 动作游戏的可视化动作编辑器，提供时间轴编排、动画预览和可扩展的运行时播放能力。

通过统一的动作配置组织角色与怪物的表现和机制，让动作设计、调试与迭代集中在同一套工作流程中。

## 核心功能

- **时间轴编排**：在统一时间轴中组织动画、音效、特效、镜头与战斗事件。
- **分层创作**：区分表现层与机制层，清晰管理动作内容及其时序关系。
- **动作预览**：支持模型预览、逐帧查看与动画片段编辑。
- **编辑与校验**：提供撤销重做、配置检查和资源迁移工具。
- **运行时扩展**：通过事件与适配接口接入项目的移动、输入、战斗和 AI 系统。

## 环境要求

- Unity 6，最低版本 6000.0
- 当前验证版本：6000.3.6f1
- 当前版本：0.2.0-preview.2

编辑器界面为英文，提供中文使用手册。

## 安装

### 安装包

从 [Releases~](Releases~/) 获取对应版本的 TGZ 文件，在 Unity Package Manager 中选择 **Install package from tarball** 完成安装。

### 源码

克隆或下载本仓库，在 Unity Package Manager 中选择 **Install package from disk**，打开仓库根目录的 [package.json](package.json)。

仓库根目录为完整 UPM 包，包含编辑器、运行时代码、测试与示例。

## 快速开始

1. 打开 **Tools > ACT Action Editor > Open Editor**。
2. 创建动作资源，设置预览模型与动画来源。
3. 在时间轴中添加轨道与事件，调整时序和属性。
4. 预览动作，检查配置并保存。

可在 Package Manager 中导入 **Playback and Interaction Windows** 示例，了解动作播放与交互配置。

## 文档

- [中文使用手册](Documentation~/UserGuide.zh-CN.md)
- [运行时与集成指南](Documentation~/AgentGuide.md)
- [更新日志](CHANGELOG.md)
- [贡献指南](CONTRIBUTING.md)

## 许可证

本项目采用 [MIT License](LICENSE.md)。
