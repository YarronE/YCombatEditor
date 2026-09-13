# Contributing

Develop the package in a Unity 6000.3.6f1 project. Preserve existing meta GUIDs and serialized identities. Keep project-specific input, controllers, combat rules and AI in consumer adapters.

For action edits use Unity SerializedObject/Undo and validation. New runtime or editing behavior needs focused regression coverage; import Playback and Interaction Windows before running all package tests. Enable the package in the project's `testables` manifest list. Run EditMode tests (some enter Play Mode), then call `Ethan.ActionEditor.Samples.SamplePlayerBuild.Run` in a closed batch project for a Windows sample build.

The Demo's embedded package is the maintainer's development source. Export reviewed snapshots to the independent repository; do not maintain a second manually edited copy. Each release uses a new version and immutable tag. Never import the Demo's assets or Git history into this repository.

Static CI checks package identity, assembly boundaries and required documentation. It is not a Unity test pass. Unity CI may be enabled only on a runner with a working licensed Editor; an unavailable license or skipped tests must not be represented as success.

Include Unity/package versions, a minimal reproduction and the first error with bug reports. Do not attach third-party assets or credentials. Contributions must identify external sources and retain compatible notices. Preview APIs may evolve; document compatibility and migration changes.

## Keep the Chinese user guide current

For changes to tracks, subtype menus, buttons, properties, shortcuts, defaults or preview behavior, update `Documentation~/UserGuide.zh-CN.md` in the same change. Keep English control names identical to the UI, explain their usage in Chinese, and revise affected examples and limitations. Record user-visible changes in CHANGELOG.md. Review the guide against the implementation before handing off. Static CI checks that the guide exists; it does not verify semantic accuracy.

General improvements developed in the action Demo must be synchronized to the standalone YCombatEditor source after testing and documentation updates. Port independent fixes back first. Preserve immutable versioned releases. Follow Documentation~/Maintenance.zh-CN.md and update ROADMAP.zh-CN.md when priorities change.
