# 从空项目到第一个动作

建议先用 Unity 6000.3.6f1 的 Built-in 3D 空项目，不要第一次就导入生产项目。

详细步骤、轨道能力、代码接入和排错见 [简洁 HTML 使用教程](index.html)。安装时在 Package Manager 选择 Install package from tarball，选择 .tgz。

1. 安装包，在 Package Manager 的 Samples 中导入 Playback and Interaction Windows。
2. 编译后执行「工具 > ACT 示例 > 生成基础播放场景」。先保存当前场景；取消则不生成。
3. 新的 Assets/ACTBasicPlayback 目录包含动作、动画与场景。重复生成自动编号，不覆盖旧资源。
4. 点击 Unity Play：方块在约一秒内上升再回落。第 15 帧输出攻击日志。Play 模式可从 BasicActionLauncher 组件菜单执行 Play Action 重播。
5. 打开「工具 > 技能编辑器」，选择生成的 BasicAction.asset，选择动画块查看属性。
6. 修改技能名称，用 Ctrl/Cmd+Z 撤销，再使用 Unity 重做。点击工具栏保存，关闭再打开确认值保留。

场景中的 ActionActor 包含 Animator、ActionClipAnimator、ActionPlayer、BasicActionLauncher。Bounce.anim 由曲线生成，仅记录 Transform 的 Y 位置，30 fps、1 秒，动作第 30 帧退出。不附带游戏角色、音效或商店素材。

新动作通过工具栏「新建」，默认 Assets/ActionEditor/Skills；项目设置 ACT Action Editor 可调整目录。空资源必须分配有效 AnimationClip 才能播放，首次可用生成的 Bounce.anim。

校验错误允许保存但阻止预览/运行。先修复红色问题，不要为绕过错误删除不理解的数据。旧 schema 资源通过「升级数据」显式备份；取消后保持只读。生产数据先做版本控制备份。

看不到方块时确认打开 BasicPlayback.unity；材质异常时先在 Built-in 模板复现，示例不会更改项目渲染管线。

## B 站录制顺序

固定版本标签：空项目安装 → 导入示例 → 生成场景 → 播放 → 修改 → Undo/Redo → 保存重开。简介写明 Unity 与包版本，不展示私有路径和未授权资源。公开前需补实际截图与人工操作验证，本文不替代视觉验收。
