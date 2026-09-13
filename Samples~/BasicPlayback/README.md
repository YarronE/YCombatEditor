# Basic Playback Sample

## 一键生成（推荐）

导入后执行「工具 > ACT 示例 > 生成基础播放场景」，保存当前场景后点击 Play。
方块使用生成的 30 fps 动画在一秒内上升再回落。重复生成使用唯一目录，不覆盖旧资源。
首先使用 Unity 6000.3.6f1 Built-in 3D 空项目；无需角色素材或第三方插件。

## 手动接入

1. Import this sample from Package Manager.
2. Add `BasicActionLauncher` and `BasicActionReceiver` to an actor with an `Animator`.
3. Assign a valid `SkillConfigSO` to the launcher and enter Play Mode.
4. Use the launcher context menu to play or stop manually.

`BasicActionReceiver` demonstrates project-side event handlers. Replace its log calls with damage, audio, VFX, energy, camera, or AI integrations. A player or Boss facade follows the same rule: preserve the project component API, own an `ActionPlayer`, translate the request, and handle project events outside the package runtime.
