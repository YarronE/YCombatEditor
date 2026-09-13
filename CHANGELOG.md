# Changelog

## Unreleased

## [0.2.0-preview.2] - 2026-09-13

- Prepared the independent YCombatEditor source repository and versioned TGZ/source ZIP distribution.
- Added a Chinese roadmap, Demo-to-package maintenance contract and standalone build/verification tools.

- Added a maintained Chinese track/button user guide and a Help button in the action editor.
- Effect Add now offers Visual Effect or Audio. The selected subtype survives copy/paste, Undo and serialization, and only matching properties are shown, including in advanced views.
- Preserved legacy mixed effects with separate property tabs; added ACT163 validation for missing or mismatched typed effect references.
- Effect previews now play only the selected subtype, and Stop effect preview also stops preview audio.

- Added opt-in legacy empty FX placeholder compatibility for playback requests; strict authoring validation and explicit-timing validation remain unchanged.
- Added root-motion ownership control so character controllers can suppress unclaimed builtin animation movement without disabling authored root-motion capture.

## [0.2.0-preview.1] - Local candidate

- Added explicit timeline FPS, source-frame trimming, shared preview animation output, moving outgoing blends and backed-up opt-in legacy timing migration.
- Separated runtime content duration from the minimum editor viewport length; preserved legacy mode.
- Added All/Any/Not condition assets, cycle validation, opt-in per-condition diagnostics and an interaction playground sample.
- Added schema-2 action read/clone/edit operations with stable GUID/local-ID references, stale-digest rejection, dry-run diffs and atomic Undo commits.
- Fixed explicit-FPS clip trimming, preview isolation/cleanup and animation segment drag offsets.
- Added designer/Agent workflows, static CI and reproducible sample Player-build entrypoint. Publication remains gated by provenance and final acceptance.

## [0.1.0-preview.2] - Unreleased

- Added optional device-independent Jump.triggerCommandId with editor shortcuts and clipboard/Undo regression. Consumer adapters resolve IDs; legacy triggerKey remains serialized.

- Added an interaction-window timeline with Evade/Block/Parry presets, reusable condition assets, custom signals, priority/activation resolution and ACT140 validation.
- Added on-demand interaction queries and per-playback state cleanup; damage and committed effects remain consumer-owned.
- Added a per-key latest-intent input buffer utility and editor/runtime regression coverage.
- Restricted animation displacement/flags to their frame intervals and made horizontal override independent of segment list order.

- Replaced the long user tutorial with an offline HTML guide separating built-in capabilities from consumer responsibilities.
- Added package-root AGENTS.md, a machine-readable capability manifest and project integration/extension guidance.
- Added Editor automation for inventory, read-only config validation, non-overwriting action creation and actor preflight, with a JSON batch entrypoint.
- Added always-available help menus independent of importing Basic Playback.
- Kept controllers, input, damage rules and AI on the consumer side; no behavior-tree editor or full controller is bundled.
- Unity 2022 remains unverified; the declared Unity minimum is unchanged.

## [0.1.0-preview.1] - Unreleased

- Added Package Manager tarball export and isolated installation validation in maintainer tools.
- Fixed playback ending before an explicit exit frame beyond the animation extent.
- Guarded sample Play/Stop outside Play Mode, enabled strict handlers by default and exposed validation details.
- Connected a generated frame-15 attack event to the sample receiver for a complete timing example.
- Added a full Chinese installation, authoring, integration and import-acceptance tutorial.

- Withdrew earlier local candidates after owner confirmed plugin-derived presentation code.
- Isolated legacy implementations outside the package, preserving game script GUIDs.
- Added ActionClipAnimator, local ActionHitStop, dedicated-pivot ActionCameraShake and a root-motion boundary using Unity APIs.
- Public package no longer includes the legacy mixer, global time effects or URP chromatic effect implementation.

- MIT license selected by owner; provenance verification remains a release gate.
- Added safe, generated Basic Playback scene/animation sample without game art.
- Added Chinese quickstart, runtime guide, limitations and publication checklist.
- Included imported sample compilation in isolated regression validation.
- Earlier editor work includes serialized inspectors, native Undo gestures and backed-up restoration.

## [0.1.0] - 2026-09-02

- Added a Unity 6 Embedded/Git-ready package layout.
- Added ActionPlayer, portable runtime contracts, and shared validation.
- Added Unity-native editor transactions, settings, migration support, and validation UI.
- Preserved existing action asset and component script identities.
