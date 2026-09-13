# 策划与 Agent 工作流 · 0.2.0-preview.2

## 从独立示例开始

1. 在 Unity 6000.3.6f1 的 Built-in 3D 空工程中安装 TGZ，然后导入 Samples 中的 Playback and Interaction Windows。
2. 用“工具 > ACT 示例”生成基础播放场景，确认方块播放以及第 15 帧事件。生成交互窗口场景，确认来袭查询和自定义信号。
3. 打开“工具 > 技能编辑器”，选择生成的动作，模型选择场景 ActionActor。新动作默认 30 FPS 时间轴；60 FPS 动画裁剪的 30 帧表示 0.5 秒。
4. 编辑片段、窗口与条件，预览、Undo/Redo、保存、关闭重开；进入 Play 后再验证真实交互请求。预览不会模拟伤害和物理碰撞。

## 配置窗口

在交互窗口轨道创建闪避、格挡、弹反或自定义窗口。帧区间包含两端，signal 必须与请求相同；满足条件的窗口按 priority 从高到低选择，同优先级按列表顺序。maxActivations=0 不限次数；失败不消耗次数。

条件资产可复用：朝向、攻击属性、资源读取，以及 Condition Group 的 All/Any/Not。Not 必须只有一个子条件，空组、循环和缺失引用均无效。条件只负责校验；实际扣资源、奖励、伤害与特效由项目在成功裁决后执行。

交互示例的自定义 Distance Condition 和 training-pulse 展示了扩展方法。无需增加核心响应枚举；用 Custom 响应加窗口 ID 区分业务。

## 让自己的 Agent 辅助编辑

把包根目录 AGENTS.md 和 Documentation~/AgentGuide.md 提供给 Agent，并提供可在 Unity 主线程执行编辑器接口的环境。已有 Unity 工程打开时，不要另开同工程批处理。

可直接使用这些任务描述：

- “读取指定动作，复制为一个新资产，保持动画引用，将第二个弹反窗口结束帧改为 28；先返回 dry-run 差异，校验通过后提交并验证 Undo。”
- “给我的窗口增加距离条件。继承 InteractionCondition，提供配置错误提示和拒绝原因，不在条件里扣资源。”
- “复用我的角色控制器，预检 ActionPlayer、Animator、事件接收器和位移后端，再接入这个动作。分别报告配置检查和真实播放结果。”

Agent 通过 read_action 获得字段及 contentDigest；编辑需提交 expectedDigest。引用使用真实 GUID/local file ID，不能猜 YAML。非法编辑不写回；资产克隆创建新 GUID。接口不批量修改引用的动画或条件资产。

## 旧动作升级

旧动作显示兼容模式，不会因打开编辑器自动迁移。基本信息里的“备份并迁移显式时间轴”保留事件帧坐标，并使用首段动画 FPS。混合帧率旧动作的片段长度可能变化，迁移后必须逐项核对。备份在 Assets/ActionEditorBackups，可恢复原资产身份。

## 工具与项目的边界

包提供动作编辑与播放，不包含输入、完整角色控制器、伤害结算、CD/资源政策、行为树或 1v多遭遇系统。CombatInputProfile 与 CombatActionSet 是原 Demo 的项目适配器，未随包分发。可以让 Agent 参照语义指令接口为自己的项目建立对应配置。

新接入至少验证：编译、动作数据、角色绑定、真实播放、中断清理、保存重开。自动测试通过不等于已完成手感和布局验收。
