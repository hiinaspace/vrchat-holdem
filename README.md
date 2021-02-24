# VRChat Holdem

No-Limit Texas Holdem implementation in VRChat Udon, with automatic
dealing and full rules enforcement.

## Status

Core game logic mostly works, but there are still some hard-to-catch bugs.
UX is still entirely through Unity UI Canvases with programmer art. More
satisfying physical interaction with the chips and cards is planned once
the core game logic is fully baked.

## Design

A single Holdem game is implemented as a single `HoldemGame` UdonSharpBehavior
and 10 `HoldemPlayer` UdonSharpBehaviors. `HoldemGame` runs the game logic on
the instance master. The `HoldemPlayer` instances broadcast each player's action (check, bet, fold).

### HoldemGame

mostly encoded as a `switch` state machine in Update(),
with game state synchronized as a 7-bit encoded UdonSynced string.

TODO more detail

### HoldemPlayer

TODO

### Testing

Since the Udon virtual machine has no debugger, a lot of the core logic is unit
tested as real C# code using Unity's test runner EditMode (with full
interactive debugging support). Unfortuantely UdonSharp can't link against
(easily testable) standalone code outside a behavior, so testable methods
within the behavior are made static. However, UdonSharp also doesn't support
the `static` keyword, so the csharp preprocessor is used to conditionally
compile the methods as static only in csharp. For complex static methods, return
values are encoded as poor man's `object[]` "struct"s, as UdonSharp also lacks
support for user-defined classes/structs or out parameters.

I don't think making the methods `static` is strictly necessary but it avoids having
to stick the UdonSharpBehavior on a gameobject.

TODO need to write more PlayMode tests to exercise the rest of the stateful logic where possible.
