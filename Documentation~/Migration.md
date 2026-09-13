# Migration

1. Back up or commit the project.
2. Install the Embedded package and allow Unity to compile.
3. Open **Tools > ACT Action Editor**. Existing resources stay at their current paths.
4. Review validation errors before previewing or running actions.
5. If a future schema migration is required, use the migration banner. The editor lists affected assets and creates a timestamped backup before writing.

Preserve `.meta` files for `SkillConfigSO`, `Global` and referenced components. The old animation and feedback implementations have been removed from the game's current source tree. CombatDemo uses new project-owned presentation components; CombatAnimationDriver retains the original scene component GUID and the three scene type identifiers have been updated. These game adapters are not distributed. New projects use ActionClipAnimator and the new package feedback components. A missing script after migration is a failed migration and should be restored from version control or the generated backup. The original game Git history has not been sanitized for public distribution.

## Collision authoring

The Attack track is displayed as Collision. Serialized track IDs and attackList/Global.Attack are preserved; opening an existing action does not rename or migrate its fields. Existing events retain Damage response and a two-unit box height, matching the Demo query. New box rotation and height fields are consumed by ActionCollisionGeometry; custom host queries must use these fields to match the editor. Signal response requires a confirmed-contact callback integration.

## Action Flow authoring

New flowNodes coexist with jumpList, cancelList and exitFrame. No automatic asset conversion occurs. Jump and Cancel track IDs retain their serialized values and are grouped into Action Flow in the editor. New Complete stops before its frame events; legacy exitFrame keeps the existing frame-end semantics. New limited windows include their final frame; legacy Jump keeps its exclusive end. Review overlapping old and new rules when adopting Flow.
