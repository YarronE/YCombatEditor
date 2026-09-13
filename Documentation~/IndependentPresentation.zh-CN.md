# 不依赖插件的基础表现

此版本的公开包使用新实现，不发布项目旧表现层。旧插件复制/改写内容已从当前游戏工作区移除，已识别的旧候选 ZIP 和临时验证副本也已删除；原游戏 Git 历史未改写，不能随公开包发布。实现过程基于动作需求和 Unity 官方 API，但开发者接触过旧代码，不宣称严格洁净室或法律免责。

## 动画

对象添加 ActionClipAnimator 与 ActionPlayer。动画由动作时间轴手动采样，不再依赖另一套自动动画时钟；动作倍率和暂停作用于同一时间轴。clipStartFrame/clipEndFrame 用源动画帧率转换为裁剪区间，到达末尾保持姿势。交叉混合最多保留两个片段输入，旧片段在过渡时继续推进到其裁剪终点，不提供通用移动混合树/插件状态 API。

停止保持最后姿势；禁用动画组件释放图。新动作默认使用显式 30 FPS 时间轴，支持不同源帧率动画；旧数据保留兼容模式并提供备份迁移。动画根运动和复杂阶段仍需项目专项验收。

## 顿帧

对象添加 ActionHitStop，命中确认后调用 Request(0.08f)。只暂停同对象 ActionPlayer，不写 Time.timeScale。多次请求取剩余时长与新时长的较大值；禁用时释放自身暂停。它不会暂停项目其他移动脚本、音频、物理或粒子，也不控制项目自行维护的播放循环。

ActionPlayer.SetPresentationPaused(owner, true/false) 提供各自拥有的暂停；不要遗忘释放自己设置的暂停。

## 震屏

相机层级为 CameraRig → CameraShakePivot → Camera。把 ActionCameraShake 挂在专用 Pivot，调用 Request(0.15f, 0.05f)。移动/跟随脚本控制上级 Rig，不能同时写 Pivot.localPosition。请求替换当前脉冲，使用非缩放时间衰减，结束/禁用后恢复位置，不搜索或自动创建全局相机。

示例生成器已添加两个组件。业务命中时显式调用它们，不把攻击判定开始误当作实际命中。色差和全局慢动作本版不提供；通过项目事件接口接入，后续可另行实现。

示例运行时可从 BasicActionLauncher 组件菜单执行「Demo Impact (Play Mode)」，观察震动；在动作播放中触发还能观察局部顿帧。结束后可用「Play Action」重播。

## 来源与验收

使用 Unity 官方 Playables、AnimationClipPlayable、OnAnimatorMove API。代码和回归用例是本变更新增。程序集与源码隔离不是对其余编辑器/数据代码的版权保证；仍需维护者完成来源核查。

CombatDemo 的移动混合、全局战斗时间策略、Cinemachine 相机偏移和 URP 色差由 Assets/_Game/Presentation 中的新项目组件承接，不随本包分发。新项目无需导入游戏适配层；请从 BasicPlayback 示例接入包内组件。
