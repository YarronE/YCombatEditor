# Migration

1. Back up or commit the project.
2. Install the Embedded package and allow Unity to compile.
3. Open **Tools > ACT Action Editor**. Existing resources stay at their current paths.
4. Review validation errors before previewing or running actions.
5. If a future schema migration is required, use the migration banner. The editor lists affected assets and creates a timestamped backup before writing.

Preserve `.meta` files for `SkillConfigSO`, `Global` and referenced components. The old animation and feedback implementations have been removed from the game's current source tree. CombatDemo uses new project-owned presentation components; CombatAnimationDriver retains the original scene component GUID and the three scene type identifiers have been updated. These game adapters are not distributed. New projects use ActionClipAnimator and the new package feedback components. A missing script after migration is a failed migration and should be restored from version control or the generated backup. The original game Git history has not been sanitized for public distribution.
