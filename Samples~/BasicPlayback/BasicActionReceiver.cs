using Ethan.ActionEditor;
using UnityEngine;

public sealed class BasicActionReceiver : MonoBehaviour,
    IActionEventHandler<Global.Attack>,
    IActionEventHandler<Global.FxAndSound>,
    IActionEventHandler<Global.WarningCue>
{
    public void Handle(in ActionExecutionContext context, Global.Attack data)
    {
        Debug.Log($"Attack event at frame {context.Frame} from {context.Config.name} (demo log only, no damage).", this);
    }

    public void Handle(in ActionExecutionContext context, Global.FxAndSound data)
    {
        Debug.Log($"FX event at frame {context.Frame} from {context.Config.name}", this);
    }

    public void Handle(in ActionExecutionContext context, Global.WarningCue data)
    {
        Debug.Log($"Warning event at frame {context.Frame} from {context.Config.name}", this);
    }
}
