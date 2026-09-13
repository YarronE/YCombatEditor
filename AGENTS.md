# ACT Action Editor — Agent entrypoint

This package is a frame-based action authoring tool and playback core, not a character controller or full combat framework. Read the consumer project's own instructions first. This file documents the package and does not grant permission to change unrelated project files.

## Read in this order

1. `package.json` for identity, declared Unity minimum and current version.
2. `Documentation~/AgentCapabilities.json` for available operations, extension points and limitations.
3. `Documentation~/AgentGuide.md` for integration workflow and automation invocation.
4. `Runtime/Core/ActionContracts.cs`, `ActionPlayer.cs` and relevant event data in `Runtime/Data/Global.cs` before implementing handlers.
5. `Documentation~/index.html` is the non-programmer tutorial; keep technical detail in the Agent guide.

Paths above are relative to this file, regardless of whether installed as Embedded, Git, or tarball. An Agent may not automatically discover AGENTS.md inside Packages/Library; the user can explicitly supply this file. No specific Agent brand, MCP server or network service is required. File reading alone cannot operate Unity: execution needs a Unity Editor connection or its command line.

## Working contract

- Keep consumer adapters and new actions under the consumer's Assets directory. Do not edit Library/PackageCache or replace an existing controller just to use this package.
- Inspect existing input, Animator hierarchy, movement owner, combat API, rendering pipeline and installed Unity version. Reuse existing systems and add narrow adapters.
- No default damage, movement-input, combo, targeting, enemy AI, behavior-tree editor, multiplayer or full controller is included. Future features are not available APIs.
- Runtime event discovery uses MonoBehaviours on the SAME GameObject. Prefer a bridge when the Animator is on a child. A handler interface alone does not supply project behavior.
- Keep RequireHandlers enabled. A successful config-only validation does not prove actor bindings or gameplay behavior. Use InspectActor for preflight and Play Mode for acceptance.
- Preserve .meta GUIDs and existing ScriptableObject identities. Change assets through Unity APIs (SerializedObject/Undo/SaveAssetIfDirty), not guessed YAML/GUID substitutions. Do not auto-upgrade all legacy actions.
- Automatic creation only writes a NEW asset. Existing action edits need an explicit user request, targeted changes, Undo, and validation. Do not remove unrecognized serialized fields.
- Never launch a second Editor against a project that is already open. Use the user's connected Editor integration, or run the CLI only with that project closed. Another isolated project can be used for checks, but its success is not proof of the consumer scene.
- Report exactly what ran: source review, compilation, config validation, actor preflight, Play Mode, or Player build. Unity 2022 compatibility is NOT yet certified; tested baseline remains 6000.3.6f1.

## Minimal acceptance

Compile → run Basic Playback → validate the action → inspect the actor with the chosen config → trigger real playback → check interruption cleanup → save/reopen. For code additions, run relevant regression tests. Hand off changed files, attachment steps and checks that still require the user. Do not assume reading this file installs or enables a Unity control tool.

## Documentation maintenance

`Documentation~/UserGuide.zh-CN.md` is the Chinese reference for tracks, buttons and authoring workflows. Every user-visible editor change must update the relevant manual sections in the same change, including exact English UI labels, defaults, compatibility behavior and preview limitations. Add a CHANGELOG entry. Do not leave the manual update as future work.

## Demo and standalone repository synchronization

The maintainer develops reusable changes in the Demo Embedded package. For every general editor/runtime improvement, update the Chinese manual, CHANGELOG and roadmap as applicable, run relevant checks, and synchronize the independent YCombatEditor source using the Demo sync tool. Keep character assets, AI, game balance, commercial plugins and Demo Git history out of this package. Port independent repository fixes back before syncing; do not overwrite divergent files. Release artifacts are versioned and immutable: bump the version and validate final TGZ bytes before a new release. See Documentation~/Maintenance.zh-CN.md.
