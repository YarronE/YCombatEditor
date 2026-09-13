using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ethan.ActionEditor
{
    [DisallowMultipleComponent]
    public sealed partial class ActionPlayer : MonoBehaviour
    {
        [SerializeField, Min(1f)] float defaultFrameRate = 30f;
        [SerializeField] bool discoverHandlersOnAwake = true;

        readonly Dictionary<Type, List<object>> _handlers = new Dictionary<Type, List<object>>();
        readonly HashSet<Type> _missingHandlerWarnings = new HashSet<Type>();
        readonly ActionTargetResolverBridge _targetResolverBridge = new ActionTargetResolverBridge();

        IActionAnimator _animator;
        IActionTargetProvider _targetProvider;
        SkillDisplacementDriver _displacementDriver;
        IDisplacementBackend _displacementBackend;
        readonly Dictionary<object, Action> _rangeExits = new Dictionary<object, Action>();
        int _generation;
        bool _stopping;
        SkillConfigSO _config;
        float _elapsed;
        float _speed = 1f;
        float _frameRate = 30f;
        int _currentFrame = -1;
        int _phase = 1;
        bool _playing;
        bool _externallyDriven;
        int _externalPresentationFrame=-1;
        readonly InteractionWindowRuntime _interactions = new InteractionWindowRuntime();
        public string LastInteractionRejection { get; private set; }
        public bool InteractionDiagnosticsEnabled { get => _interactions.DiagnosticsEnabled; set => _interactions.DiagnosticsEnabled = value; }
        public IReadOnlyList<InteractionWindowTrace> LastInteractionEvaluation => _interactions.LastEvaluation;

        public bool TryResolveInteraction(in InteractionQuery query, out InteractionResolution result)
        {
            result = default;
            if (!_playing || _stopping || _config == null) { LastInteractionRejection = "No active action."; return false; }
            bool success = _interactions.Resolve(_config.interactionWindows, _currentFrame, query, out result, out var rejection);
            LastInteractionRejection = rejection;
            return success;
        }
        readonly HashSet<object> _pauseOwners = new HashSet<object>();

        public bool IsPaused => _pauseOwners.Count > 0;

        /// <summary>Each owner releases only its own pause; global Unity time is unchanged.</summary>
        public void SetPresentationPaused(object owner, bool paused)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (paused) _pauseOwners.Add(owner);
            else _pauseOwners.Remove(owner);
        }

        public SkillConfigSO CurrentConfig => _config;
        public int CurrentFrame => _currentFrame;
        public int CurrentPhase => _phase;
        public bool IsPlaying => _playing;
        /// <summary>Set false when the host calls Advance with its own clock.</summary>
        public bool AutoAdvance { get; set; } = true;

        public event Action<SkillConfigSO> Started;
        public event Action<SkillConfigSO, ActionStopReason> Stopped;

        void Awake()
        {
            if (discoverHandlersOnAwake) RefreshCapabilities();
        }

        void OnDisable()
        {
            if (_playing) Stop(ActionStopReason.OwnerDisabled);
        }

        void Update()
        {
            if (AutoAdvance) Advance(Time.deltaTime);
        }

        /// <summary>Advances the same clock used by Update. Hosts driving this manually should disable automatic updates.</summary>
        public void Advance(float deltaTime)
        {
            if (!_playing || _externallyDriven || _config == null || _speed <= 0f || IsPaused) return;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            int generation = _generation;
            _elapsed += deltaTime * _speed;
            int nextFrame = Mathf.FloorToInt(_elapsed * _frameRate);
            int endFrame = _config.RuntimeEndFrame;
            while (_playing && _currentFrame < nextFrame)
            {
                _currentFrame++;
                DispatchFrame(_currentFrame);
                if (!_playing || generation != _generation) return;
                if (_currentFrame >= endFrame)
                    Stop(ActionStopReason.Completed);
                if (_playing && IsPaused)
                {
                    _elapsed = _currentFrame / _frameRate;
                    return;
                }
            }
            if (_playing && _animator is IActionAnimationClock clock) clock.Sample(_elapsed * _frameRate);
        }

        public void RefreshCapabilities()
        {
            _handlers.Clear();
            _missingHandlerWarnings.Clear();
            _animator = null;
            _targetProvider = null;

            var behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || ReferenceEquals(behaviour, this)) continue;
                if (_animator == null && behaviour is IActionAnimator animator) _animator = animator;
                if (_targetProvider == null && behaviour is IActionTargetProvider targetProvider) _targetProvider = targetProvider;

                var interfaces = behaviour.GetType().GetInterfaces();
                for (int j = 0; j < interfaces.Length; j++)
                {
                    var candidate = interfaces[j];
                    if (!candidate.IsGenericType || candidate.GetGenericTypeDefinition() != typeof(IActionEventHandler<>))
                        continue;

                    var eventType = candidate.GetGenericArguments()[0];
                    if (!_handlers.TryGetValue(eventType, out var list))
                    {
                        list = new List<object>();
                        _handlers.Add(eventType, list);
                    }
                    list.Add(behaviour);
                }
            }
            _targetResolverBridge.Provider = _targetProvider;
        }

        public void ConfigureDisplacement(
            IDisplacementBackend backend,
            ITargetResolver targetResolver = null,
            IActionRootMotionSource rootMotion = null,
            Animator animator = null)
        {
            _displacementDriver?.Abort();
            _displacementBackend = backend;
            _displacementDriver = backend == null
                ? null
                : new SkillDisplacementDriver(backend, targetResolver ?? _targetResolverBridge, rootMotion, animator);
            if (_displacementDriver != null) _displacementDriver.AllowFacing = frame => ActionMotionPolicy.AllowsTurning(_config, frame);
        }

        public bool TryPlay(ActionPlayRequest request, out ActionPlayError error)
        {
            if (!ValidateRequest(request, out error)) return false;
            if (_stopping)
            {
                error = new ActionPlayError("ACT_PLAY_STOPPING", "Cannot start during range cleanup.");
                return false;
            }
            // Validate against a candidate set without changing a currently playing action on failure.
            var oldAnimator = _animator;
            var oldTargetProvider = _targetProvider;
            var oldHandlers = new Dictionary<Type, List<object>>(_handlers);
            RefreshCapabilities();
            var capabilities = new ActionValidationCapabilities
            {
                HasAnimator = _animator != null,
                RequireEventHandlers = request.RequireHandlers,
                AllowLegacyEmptyFx = request.AllowLegacyEmptyFx,
                CanHandleEvent = CanHandle
            };
            var issues = ActionConfigValidator.Validate(request.Config, capabilities);
            var candidateAnimator = _animator;
            var candidateTargetProvider = _targetProvider;
            var candidateHandlers = new Dictionary<Type, List<object>>(_handlers);
            RestoreCapabilities(oldAnimator, oldTargetProvider, oldHandlers);
            if (ActionConfigValidator.HasErrors(issues))
            {
                error = new ActionPlayError("ACT_PLAY_INVALID", "Action config failed validation.", issues.ToArray());
                return false;
            }
            foreach (var segment in request.Config.GetEffectiveSegments())
            {
                if (candidateAnimator != null && !candidateAnimator.CanPlay(segment))
                {
                    error = new ActionPlayError("ACT_PLAY_ANIMATION_UNSUPPORTED", "The animator cannot play an animation segment.");
                    return false;
                }
            }

            if (_playing) Stop(ActionStopReason.Replaced);
            if (_playing)
            {
                error = new ActionPlayError("ACT_PLAY_SUPERSEDED", "A stop callback started another action.");
                return false;
            }
            RestoreCapabilities(candidateAnimator, candidateTargetProvider, candidateHandlers);
            _config = request.Config;
            _flowRequest = request;
            _phase = request.Phase;
            _speed = request.Speed;
            _frameRate = ResolveFrameRate(_config, request.FallbackFrameRate > 0f ? request.FallbackFrameRate : defaultFrameRate);
            _elapsed = 0f;
            _currentFrame = 0;
            int generation = ++_generation;
            _playing = true;
            _externallyDriven = false;
            _interactions.Clear();
            _displacementDriver?.Begin(_config.moveSegmentList);
            Started?.Invoke(_config);
            if (_playing && generation == _generation) DispatchFrame(0);
            error = ActionPlayError.None;
            return true;
        }

        public bool TryBeginExternal(ActionPlayRequest request, out ActionPlayError error)
        {
            if (!ValidateRequest(request, out error)) return false;
            if (_stopping)
            {
                error = new ActionPlayError("ACT_PLAY_STOPPING", "Cannot start during range cleanup.");
                return false;
            }
            var capabilities = new ActionValidationCapabilities
            {
                HasAnimator = true,
                RequireEventHandlers = false,
                AllowLegacyEmptyFx = request.AllowLegacyEmptyFx
            };
            var issues = ActionConfigValidator.Validate(request.Config, capabilities);
            if (ActionConfigValidator.HasErrors(issues))
            {
                error = new ActionPlayError("ACT_PLAY_INVALID", "Action config failed validation.", issues.ToArray());
                return false;
            }

            if (_playing) Stop(ActionStopReason.Replaced);
            if (_playing)
            {
                error = new ActionPlayError("ACT_PLAY_SUPERSEDED", "A stop callback started another action.");
                return false;
            }
            ++_generation;
            _config = request.Config;
            _flowRequest = request;
            _phase = request.Phase;
            _speed = request.Speed;
            _frameRate = ResolveFrameRate(_config, request.FallbackFrameRate > 0f ? request.FallbackFrameRate : defaultFrameRate);
            _elapsed = 0f;
            _currentFrame = 0;
            _playing = true;
            _externallyDriven = true;
            _externalPresentationFrame=-1;
            RefreshCapabilities();
            _interactions.Clear();
            Started?.Invoke(_config);
            error = ActionPlayError.None;
            return true;
        }

        public void ReportExternalFrame(int frame)
        {
            if (!_playing || !_externallyDriven) return;
            _currentFrame = Mathf.Max(0, frame);
        }

        /// <summary>Opt-in camera track dispatch for legacy hosts. Call after ReportExternalFrame; gameplay remains host-owned.</summary>
        public void SampleExternalPresentation()
        {
            if (!_playing || !_externallyDriven || _config == null) return;
            int generation=_generation;
            int end=Mathf.Min(_currentFrame,_config.RuntimeEndFrame);
            while (_externalPresentationFrame < end)
            {
                int frame=++_externalPresentationFrame;
                DispatchRange(_config.cameraCues,frame,x=>x.keyNumber,x=>x.endKeyNumber);
                if (!_playing || generation!=_generation) return;
            }
        }

        public void CompleteExternal(ActionStopReason reason = ActionStopReason.Completed)
        {
            if (_externallyDriven) Stop(reason);
        }

        public void Stop(ActionStopReason reason)
        {
            if (_stopping || (!_playing && _config == null)) return;
            var stoppedConfig = _config;
            _stopping = true;
            ++_generation;
            _playing = false;
            _interactions.Clear();
            try
            {
                var exits = new List<Action>(_rangeExits.Values);
                _rangeExits.Clear();
                foreach (var exit in exits)
                {
                    try { exit(); }
                    catch (Exception exception) { Debug.LogException(exception, this); }
                }
                _displacementDriver?.Abort();
                if (!_externallyDriven) _animator?.Stop(reason);
            }
            finally
            {
                _config = null;
                _currentFrame = -1;
                _elapsed = 0f;
                _externallyDriven = false;
                _stopping = false;
            }
            if (stoppedConfig != null) Stopped?.Invoke(stoppedConfig, reason);
        }

        public bool CanHandle(Type eventType)
        {
            if (eventType == typeof(Global.MoveSegment) && _displacementBackend != null && _displacementBackend.CanMove) return true;
            return eventType != null && _handlers.TryGetValue(eventType, out var list) && list.Count > 0;
        }

        void DispatchFrame(int frame)
        {
            var config = _config;
            int generation = _generation;
            if (ActionFlow.IsComplete(config, frame)) { Stop(ActionStopReason.Completed); return; }
            if (TryAutomaticFlow(frame) || !IsCurrent(generation)) return;
            DispatchAnimation(frame);
            if (!IsCurrent(generation)) return;
            if (_animator is IActionAnimationClock clock) clock.Sample(frame);
            if (!IsCurrent(generation)) return;
            DispatchRange(config.jumpList, frame, x => x.beginKey, x => x.endKey - 1);
            if (!IsCurrent(generation)) return;
            DispatchAttacks(config.attackList, frame);
            if (!IsCurrent(generation)) return;
            DispatchStarts(config.fxList, frame, x => x.keyNumber);
            if (!IsCurrent(generation)) return;
            DispatchRange(config.cameraCues, frame, x => x.keyNumber, x => x.endKeyNumber);
            if (!IsCurrent(generation)) return;
            DispatchRange(config.cancelList, frame, x => x.keyNumber, x => x.endKeyNumber, true);
            if (!IsCurrent(generation)) return;
            DispatchRange(config.projectileList, frame, x => x.keyNumber, x => x.endKeyNumber);
            if (!IsCurrent(generation)) return;
            DispatchRange(config.warningCueList, frame, x => x.keyNumber, x => x.endKeyNumber);
            if (!IsCurrent(generation)) return;
            DispatchRange(config.superArmorList, frame, x => x.keyNumber, x => x.endKeyNumber);
            if (!IsCurrent(generation)) return;
            DispatchRange(config.adjustMotionList, frame, x => x.keyNumber, x => x.endKeyNumber);
            if (!IsCurrent(generation)) return;
            DispatchRange(config.trailToggleList, frame, x => x.keyNumber, x => x.endKeyNumber, true);
            if (!IsCurrent(generation)) return;
            if (_displacementDriver != null) _displacementDriver.Tick(frame, Vector3.zero);
            else DispatchRange(config.moveSegmentList, frame, x => x.keyNumber, x => x.endKeyNumber);

            if (!IsCurrent(generation)) return;
            if (_phase >= 2)
            {
                DispatchAttacks(config.phase2AttackList, frame);
                if (!IsCurrent(generation)) return;
                DispatchStarts(config.phase2FxList, frame, x => x.keyNumber);
            }
        }

        bool IsCurrent(int generation) => _playing && generation == _generation;

        void DispatchAnimation(int frame)
        {
            if (_animator == null) return;
            int generation = _generation;
            var segments = _config.GetEffectiveSegments();
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment != null && segment.startFrame == frame && _animator.CanPlay(segment))
                {
                    if (_animator is IActionAnimationClock previousClock) previousClock.Sample(frame);
                    _animator.Play(segment, _frameRate);
                }
                if (!IsCurrent(generation)) return;
            }
        }

        void RestoreCapabilities(IActionAnimator animator, IActionTargetProvider targetProvider,
            Dictionary<Type, List<object>> handlers)
        {
            _animator = animator;
            _targetProvider = targetProvider;
            _targetResolverBridge.Provider = targetProvider;
            _handlers.Clear();
            foreach (var pair in handlers) _handlers.Add(pair.Key, pair.Value);
        }

        static bool ValidateRequest(ActionPlayRequest request, out ActionPlayError error)
        {
            if (float.IsNaN(request.Speed) || float.IsInfinity(request.Speed) ||
                float.IsNaN(request.FallbackFrameRate) || float.IsInfinity(request.FallbackFrameRate))
            {
                error = new ActionPlayError("ACT_PLAY_INVALID_REQUEST", "Playback speed and frame rate must be finite.");
                return false;
            }
            error = ActionPlayError.None;
            return true;
        }

        void DispatchAttacks(List<Global.Attack> list, int frame)
        {
            if (list == null) return;
            int generation = _generation;
            for (int i = 0; i < list.Count; i++)
            {
                var attack = list[i];
                if (attack == null) continue;
                int end = attack.endKeyNumber > attack.keyNumber ? attack.endKeyNumber : attack.keyNumber;
                if (frame < attack.keyNumber || frame > end) continue;
                if (attack.damageMode == Global.DamageMode.OneShot && frame != attack.keyNumber) continue;
                int interval = Mathf.Max(1, attack.tickInterval);
                if (attack.damageMode == Global.DamageMode.Continuous && (frame - attack.keyNumber) % interval != 0) continue;
                Dispatch(attack, frame, ActionEventPhase.Tick);
                if (!IsCurrent(generation)) return;
            }
        }

        void DispatchStarts<T>(List<T> list, int frame, Func<T, int> getStart)
        {
            if (list == null) return;
            int generation = _generation;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (ReferenceEquals(item, null) || getStart(item) != frame) continue;
                Dispatch(item, frame, ActionEventPhase.Enter);
                if (!IsCurrent(generation)) return;
            }
        }

        void DispatchRange<T>(List<T> list, int frame, Func<T, int> getStart, Func<T, int> getEnd, bool holdUntilEnd = false)
        {
            if (list == null) return;
            int generation = _generation;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (ReferenceEquals(item, null)) continue;
                int start = getStart(item);
                int configuredEnd = getEnd(item);
                int end = configuredEnd > start ? configuredEnd : holdUntilEnd ? _config.RuntimeEndFrame : start;
                if (frame == start)
                {
                    _rangeExits[item] = () => Dispatch(item, _currentFrame, ActionEventPhase.Exit);
                    Dispatch(item, frame, ActionEventPhase.Enter);
                }
                else if (frame > start && frame <= end) Dispatch(item, frame, ActionEventPhase.Tick);
                if (!IsCurrent(generation)) return;
                if (frame == end + 1 && _rangeExits.Remove(item)) Dispatch(item, frame, ActionEventPhase.Exit);
                if (!IsCurrent(generation)) return;
            }
        }

        void Dispatch<T>(T data, int frame, ActionEventPhase eventPhase)
        {
            if (!_handlers.TryGetValue(typeof(T), out var handlers))
            {
                if (_missingHandlerWarnings.Add(typeof(T)))
                    Debug.LogWarning($"[ActionPlayer] No handler registered for {typeof(T).Name}.", this);
                return;
            }

            Transform target = _targetProvider?.ResolveTarget(Global.MoveTargetKind.LockedTarget);
            var context = new ActionExecutionContext(gameObject, target, _config, frame, _phase, eventPhase);
            int generation = _generation;
            for (int i = 0; i < handlers.Count; i++)
            {
                ((IActionEventHandler<T>)handlers[i]).Handle(in context, data);
                if (generation != _generation) break;
            }
        }

        static float ResolveFrameRate(SkillConfigSO config, float fallback)
        {
            return ActionTiming.FrameRate(config, fallback);
        }

        sealed class ActionTargetResolverBridge : ITargetResolver
        {
            internal IActionTargetProvider Provider { get; set; }

            public Transform Resolve(Global.MoveTargetKind kind)
            {
                return Provider?.ResolveTarget(kind);
            }
        }
    }
}
