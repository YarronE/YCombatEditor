using Ethan.ActionEditor;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ActionPlayer), typeof(ActionClipAnimator))]
public sealed class BasicActionLauncher : MonoBehaviour
{
    [SerializeField] SkillConfigSO action;
    [SerializeField, Min(1)] int phase = 1;
    [SerializeField] bool playOnStart = true;
    [SerializeField] bool requireHandlers = true;
    [SerializeField] ActionCameraShake cameraShake;

    ActionPlayer _player;

    void Awake()
    {
        _player = GetComponent<ActionPlayer>();
    }

    void Start()
    {
        if (playOnStart) Play();
    }

    [ContextMenu("Play Action")]
    public void Play()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (_player == null) _player = GetComponent<ActionPlayer>();
        if (action == null)
        {
            Debug.LogWarning("Assign an action config before playback.", this);
            return;
        }

        if (!_player.TryPlay(new ActionPlayRequest(action, phase, requireHandlers: requireHandlers), out var error))
        {
            Debug.LogError($"Action playback failed: {error.Code} {error.Message}", this);
            foreach (var issue in error.Issues)
                Debug.LogWarning($"{issue.Code} [{issue.Track}:{issue.EventIndex}] {issue.Message}", this);
        }
    }

    [ContextMenu("Stop Action")]
    public void Stop()
    {
        if (!Application.isPlaying) return;
        if (_player == null) _player = GetComponent<ActionPlayer>();
        _player?.Stop(ActionStopReason.Interrupted);
    }

    [ContextMenu("Demo Impact (Play Mode)")]
    public void DemoImpact()
    {
        if (!Application.isPlaying) return;
        GetComponent<ActionHitStop>()?.Request(0.12f);
        cameraShake?.Request(0.18f, 0.08f);
    }
}
