# Architecture

The runtime assembly owns serialized action data, frame scheduling, validation, animation contracts, target contracts, and displacement contracts. It never references the project's `Assembly-CSharp`.

`ActionPlayer` is the portable orchestration boundary. It advances a deterministic integer frame clock and dispatches events through `IActionEventHandler<TEvent>`. Project rules such as damage, energy, resonance, camera direction, input, and boss state live in adapters.

Legacy serialized types remain in the global namespace during 0.1.x to avoid combining a file move, assembly move, namespace change, and schema rewrite in one migration. New public APIs use `Ethan.ActionEditor`.

Optional Unity packages belong in independent integration assemblies guarded by package version defines. Project-only integrations remain in `Assets`.
