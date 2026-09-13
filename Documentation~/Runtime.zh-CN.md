# 运行时接入

给对象添加 ActionClipAnimator（要求 Animator）以及 ActionPlayer。配置有效动画；最小接入参考 BasicActionLauncher。

```csharp
using Ethan.ActionEditor;
using UnityEngine;
public class PlayMyAction : MonoBehaviour
{
    public ActionPlayer player;
    public SkillConfigSO config;
    public void Play()
    {
        if (!player.TryPlay(new ActionPlayRequest(config), out var error))
            Debug.LogError($"{error.Code}: {error.Message}");
    }
}
```

在运行模式且组件完成 Awake 后调用。动画需要 IActionAnimator，不是缺省就能播放。ActionPlayer 默认自动推进；自行调用 Advance 时先设 AutoAdvance=false，避免双重计时。

业务事件由同一对象上的 IActionEventHandler<TEvent> 组件接收。BasicActionReceiver 仅输出日志，不生成特效。requireHandlers=false 只放宽缺失处理器检查，不会补上行为。目标解析通过 IActionTargetProvider；位移需 ConfigureDisplacement 指定后端。

Stop(ActionStopReason.Interrupted) 中止。先检查 TryPlay 返回值与错误列表，再查引用、处理器和阶段。允许保存不代表允许播放。

预览版不承诺复杂阶段、非 1 倍速、动画裁剪、所有位移组合与旧游戏完全一致。首次接入使用单段未裁剪动画、30 fps、正常速度、明确退出帧。项目伤害、输入、Boss AI、镜头和联机由接入方实现。
