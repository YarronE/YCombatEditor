# ACT Agent integration guide

Protocol versions 1 and 2 · package 0.2.0-preview.2 · tested engine baseline 6000.3.6f1.

## What this integration surface is

An Agent with filesystem access can read this package, implement consumer adapters and prepare actions. For asset creation and scene operations it also needs either a connected Unity Editor tool capable of executing C# on the main thread, or the Editor command line against a closed project. This package does not install an Agent, expose an HTTP/MCP server, or grant remote control of a running Editor.

The human tutorial is index.html. Use AGENTS.md at the package root and AgentCapabilities.json as the discovery entrypoints. Resolve paths relative to the installed package; do not assume the original game checkout or a particular Windows username. Read consumer project instructions first.

## Integration workflow

1. Inventory the consumer project: Unity full version, render pipeline, action assets, input stack, scene/prefab actor hierarchy, Animator and Avatar, movement owner, damage interface and existing controller.
2. Identify the minimal requested behavior. An animation-only prototype needs an actor, matching animation, ActionPlayer, IActionAnimator and a trigger. A combat prototype additionally needs hit queries/deduplication/damage rules. No controller replacement is necessary by default.
3. Put integration scripts under Assets/ActionEditorIntegration (or the project's agreed location). Reuse existing input/state machine to call TryPlay, existing damage service from attack handlers, and existing movement system via IDisplacementBackend.
4. Create an isolated action or duplicate a specifically authorized asset through AssetDatabase. Bind AnimationClips by verified path and, for FBX sub-assets, exact clip name. Keep references as Unity assets rather than invented GUIDs.
5. Run config validation, then InspectActor with the intended actor and config. Resolve failures and explain any preflight limitations. Check Play Mode in the actual consumer scene before claiming integration complete.
6. Verify interrupted/replaced/completed actions clean up windows and project effects. Verify save/reopen and serialize changes through Undo/SerializedObject.

## Existing extension points

- IActionAnimator.CanPlay/Play/Stop: animation backend. ActionClipAnimator is provided and requires an Animator on its object. Child Animator hierarchies need a consumer adapter; do not blindly move components between hierarchy levels.
- IActionAnimationClock.Sample(float timelineFrame): optional absolute-frame sampling tied to ActionPlayer. If a custom animator omits it, the host must deliberately handle speed/pause synchronization.
- IActionEventHandler<T>.Handle(in ActionExecutionContext, T): generic event handler on the SAME GameObject as ActionPlayer. Context contains actor, target, config, frame, phase, event phase. See ActionContracts.cs for exact types.
- IActionTargetProvider.ResolveTarget(Global.MoveTargetKind): consumer targeting.
- ConfigureDisplacement(IDisplacementBackend, ITargetResolver, IActionRootMotionSource, Animator): movement backend. ActionCharacterControllerDisplacement bridges CharacterController. Legacy totalMoveDistance/moveCurve are not an automatic standalone fallback.
- TryPlay(ActionPlayRequest, out ActionPlayError), Stop(ActionStopReason), Started, Stopped: host input/state machine boundary. Default AutoAdvance=true; set false before manual Advance to avoid double stepping.
- TryBeginExternal/ReportExternalFrame/CompleteExternal: external lifecycle reporting only. It does not run normal animation/event dispatch. Do not use it as a substitute for TryPlay when expecting tracks to execute.

## Runtime semantics and boundaries

Attack OneShot dispatches one Tick at its start frame. Continuous dispatches Tick at tickInterval across its configured range. The host decides hit queries, target deduplication and damage. FxAndSound dispatches Enter only at its start; endKeyNumber does not automatically stop spawned effects. Project effect lifetimes and interruption cleanup need a handler/lifecycle owner.

Jump dispatch range ends at endKey-1. Most other ranges include endKey and Exit on the following frame, or on Stop. Cancel and Trail with end<=start hold until RuntimeEndFrame. Explicit ends remain recommended. Phase>=2 ADDS phase2 Attack/FX to phase1 data; the player does not apply phase2RangeMultiplier itself. HitFx is configuration for host hit reactions and has no direct ActionPlayer dispatch. CD, energy, input, combo cancellation, super-armor rules, projectiles and AI are host logic.

Discovery currently includes disabled MonoBehaviours at runtime; do not rely on disabling a receiver to remove it. Remove/unregister through an intentional adapter strategy or gate Handle explicitly. InspectActor is deliberately stricter and counts active enabled components only. It recognizes the bundled CharacterController bridge, but cannot infer custom ConfigureDisplacement calls, exact target availability or gameplay correctness.

Only built-in serialized tracks are currently supported. Implementing IActionEventHandler<MyEvent> alone does NOT add a persistent custom track or dispatch it. Prefer using existing events plus consumer metadata assets; a new track requires an intentional package fork with data model, editor registry/inspector, validation, dispatch, serialization/migration and tests. Avoid changing package caches.

New actions use explicit timing (default 30 FPS), shared clip sampling and a two-input blend. Source trim frames retain clip FPS. Existing timingVersion=0 actions preserve legacy behavior, including legacy preview differences; migrate explicitly with a backup. Root Motion, complex displacement and optional URP/navigation integrations need their own acceptance. Unity 2022 has not been run here.

Hosts preserving legacy empty FX/Sound placeholders can opt into `ActionPlayRequest.allowLegacyEmptyFx` and `ActionValidationCapabilities.AllowLegacyEmptyFx`. Only ACT115 on legacy timing assets becomes a warning; explicit timing, missing projectile references and other errors remain blocking. Authoring/Agent validation stays strict by default. `ActionRootMotionCollector.ApplyUnclaimedRootMotion=false` prevents builtin root movement when a controller owns actor displacement; explicitly captured animation deltas remain available to the displacement driver.

## Executable authoring API

Editor assembly: Ethan.ActionEditor.Editor. Public class: Ethan.ActionEditor.Editor.ActionEditorAgent.

Call Execute(Request) on the Editor main thread. Requests and Results are public serializable classes with public fields and schemaVersion=1. Result includes success, operation, unityVersion, packageVersion, createdAssetPath, actionAssets, animationAssets and issues. Each issue includes assetPath, code, severity, track, eventIndex and message. Unknown operations and errors return success=false. Execute does not throw expected request validation errors to the caller.

Supported operations:

- inventory: read-only listing of action and animation asset paths under Assets. FBX paths may contain multiple clips; inspect sub-assets before choosing animationClipName.
- validate: requires a nonempty assetPaths array. Runs ActionConfigValidator on each asset without saving. It does NOT validate scene/actor handlers.
- create_action: requires a NEW outputAssetPath below Assets and animationAssetPath. Optional animationClipName disambiguates imported clips; displayName names the action. exitFrame=-1 derives the clip duration; 0 keeps legacy auto extent; positive sets an explicit exit. Creates current-schema single-clip config at frame 0. Requires a non-Legacy, nonempty animation. Existing files/metas are never overwritten. Does not spawn or modify a scene actor.

InspectActor(GameObject actor, SkillConfigSO config) is a separate read-only C# call for a loaded actor. It checks ActionPlayer, active animation adapter, required event types and bundled CharacterController movement bridge. It does not invoke Awake, Play, damage or movement. For batch scene inspection, a consumer script must deliberately open its chosen scene; preserve any unsaved scenes in a connected Editor.

### JSON requests

```json
{"schemaVersion":1,"operation":"inventory"}
```

```json
{"schemaVersion":1,"operation":"validate","assetPaths":["Assets/ACTBasicPlayback/BasicAction.asset"]}
```

```json
{"schemaVersion":1,"operation":"create_action","outputAssetPath":"Assets/ActionEditor/Skills/MyAction.asset","animationAssetPath":"Assets/ACTBasicPlayback/Bounce.anim","displayName":"My Action","exitFrame":30}
```

The last two examples require the generated Basic Playback assets. Otherwise substitute actual inventory paths. For FBX also supply animationClipName. Re-running create_action against an existing destination intentionally fails.

### Command line against a closed consumer project

Use a new result filename for every invocation. Result files are restricted to the consumer project's Logs/ActionEditorAgent directory and are not overwritten. Keep the request JSON outside that result path.

```text
<UnityEditor> -batchmode -nographics -projectPath <consumer-project>
-executeMethod Ethan.ActionEditor.Editor.ActionEditorAgent.RunFromCommandLine
-actionEditorRequest <absolute-request.json>
-actionEditorResult <consumer-project>/Logs/ActionEditorAgent/run-001.json
-logFile <absolute-log-path>
```

Combine these into one invocation with paths quoted for the host shell. The entrypoint exits with 0 on success or 2 on a rejected request/validation error. Compile/launch failures can prevent a result from being written: require BOTH process success and an existing result with success=true; read the Unity log otherwise. Do not claim success from an old JSON file. Execute JSON via Unity JsonUtility so assets are handled by Unity, not external YAML manipulation.

## Copyable task briefs

Integration: Read this package's AGENTS.md and the consumer project instructions. Identify my input/controller/Animator hierarchy. Keep my existing controller. Propose and implement the smallest adapter that triggers a selected action, then validate it and tell me what to attach and what has actually been tested.

New behavior: Read the event contract and relevant data first. Implement the requested project-side receiver under Assets using existing services. Handle interruption and object cleanup. Do not invent custom serialized track support or modify the installed package cache.

New action: Inventory verified clips, create a NEW current-schema action through ActionEditorAgent, validate it, and wire it only to the actor I specified. Do not overwrite another action or silently migrate all configs.

## Future scope

Bundled character controllers, input presets, combat receivers and a behavior-tree editor are possible later layers, not part of this preview. Keep current interfaces narrow so projects can replace those layers. Do not generate calls to unreleased features.
## Extensible interaction windows (working preview)

`SkillConfigSO.interactionWindows` is edited through the Interaction timeline track. Built-in presets provide Evade, Block and Parry parameters. This is an on-demand query API, not `IActionEventHandler<InteractionWindow>` dispatch. The package does not itself implement damage or input actions.

The consumer calls `ActionPlayer.TryResolveInteraction(in InteractionQuery, out InteractionResolution)` when an incoming hit or custom signal occurs. The active window must match the signal, inclusive frame interval, source cone, attack flags and every `InteractionCondition`; highest valid priority wins, with stable list order for ties. Rejected windows do not consume activations. `maxActivations=0` is unlimited; `FirstActivation` identifies the first committed resolution per window per playback. The player resets runtime counters on stop/replacement/disable. `InteractionWindowRuntime.Evaluate` supplies a non-consuming preflight.

Extend `InteractionCondition` as a ScriptableObject with pure `Evaluate` and optional `ConfigurationError`. Built-ins cover facing, attack tags/flags and resource thresholds via `IInteractionResourceProvider` on the actor. A missing condition fails closed. Use custom signal strings and `Custom` responses for additional gameplay, and implement side effects in the consumer after resolution. Conditions must never spend resources or mutate shared assets. Validation issues use ACT140 and locate the Interaction track.

`ActionInputBuffer` is an optional per-KeyCode latest-intent utility; the host owns capture, expiry clock, consumption and transition policy. It does not install an Input System dependency or a controller.

## Semantic Jump input

`Global.Jump.triggerCommandId` optionally identifies a consumer-defined command. A nonempty ID takes precedence over `triggerKey` in the consumer; automatic Jump events require no input. The core still dispatches Jump windows and does not sample devices. The editor provides conventional shortcuts and custom text entry, while the project owns cross-profile validation and legacy-key aliases. Existing assets need no batch migration.


## Schema 2: inspect, clone and targeted edit

Use `read_action` with exactly one `assetPaths` entry. The returned `fields` contain Unity property paths and kinds; asset references use `referenceGuid` + `referenceLocalId`. `contentDigest` hashes stable serialized values, not in-session instance IDs. Reading an invalid action still returns fields and issues so it can be repaired.

`edit_action` and `clone_action` require that exact `expectedDigest`. A stale request fails before mutation. First send `dryRun:true` and inspect `changes` and `issues`; then submit against the same digest. `clone_action` also requires a new `outputAssetPath`. Validation runs on a temporary copy, and only a valid result is committed. One Undo group covers the action edit. These calls never edit referenced condition/animation assets.

```json
{"schemaVersion":2,"operation":"read_action","assetPaths":["Assets/ACTInteractionPlayground/Action.asset"]}
```

```json
{"schemaVersion":2,"operation":"edit_action","assetPaths":["Assets/ACTInteractionPlayground/Action.asset"],"expectedDigest":"<digest from read_action>","dryRun":true,"edits":[{"operation":"set","path":"interactionWindows.Array.data[1].endKeyNumber","value":{"kind":"Integer","integer":28}}]}
```

Supported leaf kinds: Integer (`integer`), Float (`number`), Boolean (`boolean`), String (`text`), Enum (exact enum name in `text`), Vector2/3/4, Quaternion and Color (`vector`), AnimationCurve (`curve`), ObjectReference (verified GUID/local ID; empty GUID clears). ArraySize and Unity internal fields cannot be set. Timing migration is protected; use `ActionTimingMigration.Migrate` explicitly.

Root track edits use `operation:insert|remove|move`, a root list `path`, `index`, and `destinationIndex` for move. Insert constructs a default event; subsequent set edits fill its fields in the same transaction. All edits validate together. Nested condition lists use typed object-reference leaf edits; composite condition assets are created/edited through Unity's normal asset APIs.

Actor preflight remains `InspectActor(GameObject,SkillConfigSO)`, a read-only C# API. It never starts gameplay. CLI request processing supports both schemas. No MCP server or specific Agent is required.

## Timing and interaction contracts

- New action creation menus, sample builders and create_action opt into timingVersion=1. Programmatic consumers must call InitializeExplicitTiming explicitly; CreateInstance alone retains legacy-compatible defaults.
- Explicit timeline starts/events/blend frames use timelineFrameRate; crop frames use clip.frameRate. Duration is ceil(cropped seconds × timeline FPS). RuntimeEndFrame is independent of the minimum editor viewport length.
- The core samples the current pose before events. Fast stepping dispatches elapsed temporal events; it does not reconstruct historical physical contacts. External hosts remain responsible for their own event delivery and collision policy.
- InteractionConditionGroup validates the entire graph, supports All/Any/Not, rejects cycles/missing children, and never consumes resources. Empty groups are invalid; Not requires one child. Exceptions fail closed, including under Not.
- Enable ActionPlayer.InteractionDiagnosticsEnabled to retain candidate/condition traces, or use its runtime Inspector. Diagnostics are opt-in to avoid normal-play allocation.
- External hosts must pause both their gameplay clock and animation when ActionPlayer.IsPaused is true; ReportExternalFrame alone cannot pause a host. Release all host presentation ownership on completion, replacement, interruption and disable.
