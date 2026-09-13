# ACT 动作编辑器使用手册

更新日期：2026-09-13。适用于当前工作区的统一时间轴版本。编辑器界面使用英文，本手册用中文解释界面中的原始名称。后续新增、删除或调整轨道、按钮、字段与操作流程时，须在同一变更中更新本手册。

## 1. 先认识窗口

打开 Unity 的 `Tools > ACT Action Editor > Open Editor`，选择一个 Action 资源。工具栏的 **Help**，或 `Tools > ACT Action Editor > Help > User Guide (Chinese)`，可打开本手册。

整个窗口使用同一个播放头、帧率和时间轴。上方工具栏管理资源与预览；时间轴配置事件；属性面板编辑选中的事件。宽窗口会把时间轴和属性并排放置，窄窗口调整为上下结构。

时间轴明确分成两层：

| 分层 | 用途 | 轨道 |
| --- | --- | --- |
| Presentation（表现层） | 动作看起来、听起来如何 | Animation、Effect、Camera、Telegraph、Hit Effect、Trail |
| Mechanics（机制层） | 动作如何移动、命中、转接与响应 | Motion、Attack、Transition、Cancel、Projectile、Armor、Interaction |

两层在同一个时间轴直接编辑。例如第 12 帧可以同时开始攻击判定、播放剑光、播放挥刀声和触发镜头。Effect 中的 Visual Effect 和 Audio 是同一种轨道上的不同事件，不需要分别建立两条轨道。

角色与怪物共用这些轨道。`Action properties > Actor profile` 区分配置身份；它不会自动创建玩家输入、怪物 AI、伤害系统或锁定目标。项目的运行时接收器决定事件在游戏里如何执行。Scene 中预览正确也不等于真实战斗已接入完成。

## 2. 工具栏按钮

| 控件 | 用法 |
| --- | --- |
| New | 创建新动作资源，再选择保存位置。新动作默认使用显式时间轴帧率。 |
| Save | 保存当前动作。修改后应保存，再重开确认。 |
| Undo / Redo | 撤销或重做资源编辑，例如添加、删除、拖动事件和修改属性。 |
| Action | 选择正在编辑的动作资源；切换前会结束当前预览。 |
| Action properties | 编辑整段动作的名称、说明、Actor profile、退出帧、阶段标记和机制参数。 |
| Tools | 打开高级设置、默认命中形状、角色设置、备份恢复与迁移等入口，见下表。 |
| Help | 打开本中文手册。 |
| Preview actor | 指定用于预览的模型或场景对象。预览系统使用隔离的模型副本。 |
| Clip source | 指定含动画的模型资源，供 Animation 的 Add 菜单选择动画片段。 |
| Play / Stop | 开始或停止编辑器预览；不会替代游戏 Play Mode 中的完整战斗测试。 |
| Previous / Next | 播放头向前或向后移动一帧。 |
| Frame | 直接输入要查看的帧。右侧显示时间轴长度、当前秒数与 FPS。 |
| Saved / Unsaved | 保存状态提示，不是按钮。 |
| Issues | 展开或收起校验结果；数字为错误和警告总数。 |
| Zoom | 调整每帧的显示宽度。只改变视图，不改变事件时间。 |
| Fit | 调整缩放，让时间轴适应当前可用宽度。 |

`Tools` 菜单：

| 选项 | 用法 |
| --- | --- |
| Show advanced properties | 显示较少使用的字段。Effect 即使展开高级属性，也只显示当前类型的字段。 |
| Default hit shape | 设置新建 Attack 的默认形状、尺寸和偏移。已有事件可用 Override hit shape 单独设置。 |
| Actor settings | 编辑已有的角色或怪物扩展配置；实际作用取决于接入项目。 |
| Restore backup | 选择备份恢复当前资源。先核对恢复目标和备份时间。 |
| Upgrade schema with backup | 旧资源需要升级时出现。先备份，再升级；不会批量自动升级其他资源。 |
| Arrange animation clips | 至少有两段动画时出现。可按 3 / 5 / 8 帧混合重新排列，关闭空隙，或清除全部混合。会修改动画时间位置，应检查原有事件是否仍对齐。 |

`Action properties` 中的 `Migrate to explicit timing` 用于旧帧率模式迁移，会先确认并备份。迁移保留事件帧号；包含不同 FPS 动画时，应重新检查长度与重叠。

## 3. 时间轴基本操作

1. 点击标尺定位播放头，或在 Frame 输入目标帧。
2. 点击轨道右侧 **Add**，在播放头处添加事件。Effect、Motion 会继续弹出子类型菜单。
3. 点击事件，在属性面板修改内容。拖动事件主体可移动时间；有边缘手柄的范围事件可拖动起止边界。
4. 预览、检查 Issues，再 Save。对于命中、交互、转向、镜头等行为，还需在接入角色的 Play Mode 中验证。

| 控件或右键选项 | 用法 |
| --- | --- |
| Add track | 添加尚未显示的轨道；已显示的类型会标注 already added。 |
| Add | 在当前帧新增该轨道事件。 |
| Hide / Show | 折叠或展开轨道内容，不会禁用事件。 |
| Add … at playhead | 轨道右键菜单中的新增入口；Effect 同样先选择 Visual Effect 或 Audio。 |
| Copy / Copy selected … | 复制选中事件及其配置和资源引用。 |
| Paste … at frame … | 在播放头处粘贴兼容类型的事件，保留原相对长度。Visual Effect 和 Audio 均可粘贴到 Effect。 |
| Remove / Remove selected … | 删除选中事件，支持 Undo。 |
| Move track up / down | 调整轨道顺序；Presentation 与 Mechanics 的分层显示始终保留。 |
| Hide track | 从当前轨道列表移除显示项；事件仍保留并参与运行。使用 Add track 恢复显示。 |

Animation 的右键菜单还包括 `Reset source trim`（恢复源动画裁剪）、`Blend over 5 frames`、`Blend over 8 frames`（设置过渡混合）、`Clear blend`（清除该段混合）。

快捷键仅在编辑器取得焦点且没有输入文字时生效：Space 预览/停止；左右方向键逐帧；Home / End 跳到时间轴首尾；Delete 删除选中事件；Ctrl+C / V 复制/粘贴；Ctrl+Z 撤销；Ctrl+Y 或 Ctrl+Shift+Z 重做。时间轴上 Ctrl+滚轮缩放。Mac 可用 Command 代替复制、粘贴、撤销和重做的 Ctrl。

## 4. 各轨道如何配置

### Animation：动画

Add 选择 `Clip source` 中的动画；没有可用片段时可先添加 `Empty clip (assign animation later)`，再指定 Clip。常用属性为 Clip、Start Frame、Clip Start Frame、Clip End Frame、Blend In Frames。

Start Frame 是这段动画在动作时间轴的位置。源裁剪帧属于动画本身，不能把不同 FPS 动画的源帧号直接当作同一时间轴的帧号。重叠片段与 Blend In Frames 一起控制过渡。先确定动画节奏，再配置命中和表现事件。

### Effect：特效与音效

点击 **Add** 后必须先选类型：

| 子类型 | 属性面板显示 | 示例 |
| --- | --- | --- |
| Visual Effect | Start frame、Visual effect、Offset、Rotation、Scale、Follow actor、Custom Lifetime | 第 12 帧在武器位置生成剑光。 |
| Audio | Start frame、Audio clip、Base Volume | 第 12 帧播放挥刀声。 |

Visual Effect 的高级属性包含 End frame、Playback Speed、Use World Space；Audio 的高级属性包含 End frame、Audio Randomize、Pitch Variation、Volume Variation。音效事件不会显示粒子、偏移或缩放字段，特效事件不会显示音频与随机音量字段。时间轴标签分别以 **VFX:** 和 **Audio:** 开头，空引用时也保留所选类型。

需要同时播放声音和特效时，在相同帧添加两个事件，分别调整时间与属性。复制、粘贴、保存与重开会保留子类型。新事件必须指定对应资源；混入另一种资源或丢失所需资源时，校验报错 `ACT163`。

`Preview visual effect` 预览所选粒子；`Preview audio` 试听所选音频；`Stop effect preview` 清理预览粒子并停止预览音频。试听使用 Base Volume；运行时随机音高、音量等效果取决于项目接收器，不应只靠此按钮验收。

**旧资源兼容：**旧版事件可能同时引用粒子和音频。选中后可用 `Visual Effect / Audio` 页签分别编辑；切换页签只改变显示内容，不删除引用，也不改变运行时两者都可播放的行为。若希望拆成两个事件，分别新建并配置完整、检查时间和引用后，再手动删除旧事件。打开和保存不会自动拆分已有动作。

Effect 是按起始帧触发的表现事件。`End frame` 是编辑时间范围，不能保证在该帧自动停止粒子或声音；粒子清理时长通过 Custom Lifetime 等配置交给接收器，声音播放长度通常来自 AudioClip。值为 0 的 Custom Lifetime 表示使用粒子的默认时长。高级字段是否被执行须以项目接收器为准。

### Camera：镜头

配置 Start frame / End frame、Position Offset、Rotation Offset、Field Of View Offset、Shake Amplitude、Shake Frequency 和 Envelope。Envelope 控制范围内的效果强度，适合做短促强调、蓄力收镜、命中震动。

需接入 Camera 事件接收器。包内 `ActionCameraDriver` 需要绑定专用镜头支点或相机；与项目已有相机控制器协调，避免多个组件同时写同一 Transform。当前 Scene 预览不模拟 Camera 轨道，请在 Play Mode 检查并测试中断后复原。

### Motion：位移与朝向

同一 Motion 轨道内分为 Displacement 和 Facing / target tracking 两排。Add 有三个选择：

| 选项 | 用法 |
| --- | --- |
| Displacement | 配置一段位移。设置帧范围、Drive Mode、Direction、目标、曲线、Distance limit 等。曲线或 Root Motion 的执行需接入位移后端。 |
| Allow target tracking | 允许在指定范围内朝目标转向。配置 Tracking target、Turn speed (deg/s)、Y Axis Only。 |
| Lock facing / planted feet | 在指定范围内锁定朝向，适合落脚、出刀等不应持续追踪的阶段。 |

`Allow target tracking` 是朝向开关，关闭时目标与转速不产生追踪作用。存在朝向窗口时，窗口之间的空隙保持朝向；重叠时锁定优先；完全没有朝向窗口则保持旧的朝向策略。转速 0 表示立即朝向目标，较大的转速会更快跟随。

Boss 蓄力可先 Allow target tracking，出招落脚时切到 Lock facing。新建敌人追踪窗口默认目标为 Player，玩家默认为 LockedTarget。锁定只限制朝向，不能自动解决动画脚位、根运动或位移曲线不匹配造成的滑步；仍须连同动画和位移一起调整。自有控制器需遵守同一朝向策略，或接入包内 `ActionFacingDriver`。

### Attack：攻击判定

设置 Start frame / End frame、Damage Mode、Tick Interval、Damage Ratio、Impact Level。通过 Override hit shape 配置本次攻击的形状、尺寸与偏移；Unblockable、Unparryable、Interaction Tag 用于交互判定条件。实际碰撞查询、扣血、命中去重和受击响应由项目接收器实现。

高级骨骼工具：`Bind bone path` 绑定所选骨骼路径；`Sample bone path` 采样整段判定偏移；`Update current offset` 更新当前帧偏移；`Clear bone offsets` 清除采样并关闭骨骼跟踪；`Focus hit shape` 将 Scene 视角移到判定范围。需要先指定合适的 Preview actor 与动画。

### Transition：动作衔接

设置有效帧范围、Next Skill、Input command、Auto Trigger、能量条件/消耗和 Fade Duration。Input command 可选 Attack、Shoot、Special、Dodge、Jump，或使用自定义命令；有命令 ID 时旧 Trigger Key 被忽略。`Preview transition cue` 只预览提示，不等于执行真实输入和完整连段。

Transition 的 End frame **不包含**该帧：例如 15–25 在第 15 至 24 帧可用。输入映射和资源消耗由宿主负责接入。

### Cancel：取消窗口

指定帧范围与 Min Cancel Priority 等取消条件。用于区分不可取消的出招段和允许高优先级动作打断的恢复段。是否真的打断当前动作由运行时取消逻辑判断；移除轨道显示不会关闭取消行为。

### Projectile：投射物

指定 Prefab、Fire Point、Spawn Offset、Speed、Lifetime、Damage Ratio、Auto Aim、Shot Count、Spread Angle 等。用于箭矢、飞弹、弹幕。`Preview projectile` 用于观察生成位置；真实移动、追踪和碰撞需投射物接收器及项目实现。

### Armor：霸体窗口

为指定帧段配置霸体级别等数据，适合重攻击或 Boss 强韧阶段。霸体表示如何抵抗打断，不等于自动免伤；伤害与受击控制由宿主决定。

### Interaction：闪避、格挡与弹反

设置唯一 ID、Signal、范围、Priority、Response、Source Angle、Damage Multiplier、Suppress Hit Reaction、Max Activations，以及可复用的 Conditions。窗口范围包含首尾帧。相同信号下多个条件有效的窗口以最高优先级决胜。

| 按钮 | 预设内容 |
| --- | --- |
| Evade preset | IncomingHit 信号、Evade 响应、360°来源角、伤害倍率 0、优先级 20。 |
| Block preset | IncomingHit 信号、Block 响应、120°来源角、伤害倍率 0.2、优先级 10。 |
| Parry preset | IncomingHit 信号、Parry 响应、120°来源角、伤害倍率 0、优先级 30。 |

三个预设都开启 Suppress Hit Reaction，并将 Max Activations 设为 0（不限次数）。预设会覆盖相应字段，使用后检查条件与范围。条件资源在 `Create > Combat > Interaction Conditions` 创建；可用组合条件表达 All / Any / Not。宿主必须提交交互信号并应用结果，单独添加窗口不会让角色自动具备弹反能力。

### Telegraph：预警表现

配置预警范围、Warning VFX、Warning Sound 等，适合 Boss 出招提示或危险区域提示。它属于表现层，不替代 Attack 的实际伤害判定；玩家与怪物均可使用。

### Hit Effect：命中反馈

配置命中目标时使用的特效与声音等反馈数据。它与 Effect 的区别是执行条件：Effect 跟随动作时间，Hit Effect 供真实命中逻辑使用。此轨道仍保留其独立的命中反馈数据结构，不使用 Effect 的新增子类型菜单。

### Trail：拖尾

设置帧段内拖尾的开关与相关配置。需要宿主接到实际使用的拖尾实现，配置轨道本身不会自动创建拖尾渲染器。

## 5. 两个配置示例

**玩家普通攻击：**先放 Animation；在挥刀开始帧添加 Effect > Visual Effect 和 Effect > Audio；在刀刃经过目标的帧段添加 Attack；需要向前挪动时添加 Motion > Displacement；恢复段添加 Transition 接下一招；最后 Save，并在真实角色上验证动画、位移、命中与输入衔接。

**Boss 蓄力斩：**先放 Animation；蓄力阶段使用 Motion > Allow target tracking，目标选择 Player；双脚落地至出刀结束使用 Lock facing；危险提示使用 Telegraph；刀锋接触时配置 Attack 与 Effect；需要视觉强调时加 Camera。在 Play Mode 检查朝向锁定、退出清理和镜头复原。

## 6. 校验、保存与排查

Issues 展示配置错误与警告。`Locate` 跳到对应事件和帧；有些全局问题没有可定位事件。常见原因包括缺少动画或资源、非法范围、交互条件缺失、没有对应接收器。新建 Effect 未指定资源时出现校验错误是正常的未完成状态，填写对应资源后再检查。

若新增类型选择后属性不对，先确认选择的是新事件还是旧混合事件；旧事件通过页签切换显示。若预览按钮不可用，检查 Preview actor、资源引用以及旧资源是否尚未升级。若预览有声音但游戏没有，检查宿主音频接收器与音频输出，而不是增加第二种 Effect 字段。

推荐每次交付前执行：校验配置 → 预览关键帧 → Save → 重开资源 → 在角色/怪物上播放 → 中断动作 → 检查粒子、声音、朝向和镜头是否按预期清理。包当前是动作创作与播放核心，不能仅凭编辑器配置保证某个完整动作游戏的品质或所有宿主系统的兼容性。

## 7. 文档同步约定

本文件是轨道、按钮和操作流程的中文使用参考。开发者应在每次界面或行为变更中同步更新对应章节、英文控件名称、默认值、示例与限制，并在 CHANGELOG 中记录用户可见变化。维护要求也写入包内 AGENTS.md 与 CONTRIBUTING.md。静态检查会检查本文件存在，但不会自动证明描述与代码一致，仍需人工核对。

本次更新：统一时间轴两层结构；Effect 在 Add 时选择 Visual Effect / Audio；按类型筛选基础与高级属性；旧混合事件分页编辑；新增 Help 入口。技术接入参考 [AgentGuide.md](AgentGuide.md)，首次安装参考 [index.html](index.html)。

源码与安装包的维护方法见 [维护与同步说明](Maintenance.zh-CN.md)，后续计划见 [更新 roadmap](../ROADMAP.zh-CN.md)。
