# Project Tests

**Status:** ACTIVE
**Last Updated:** 2026-07-03 (rewritten for the round-2 test consolidation, plan R48)

All NUnit tests for the project live in this directory — terrain, player, bootstrap, and smoke tests alike — under exactly two assemblies:

| Folder | Assembly | Runs in | Contents |
|--------|----------|---------|----------|
| `EditMode/` | `DOTS.Tests.EditMode` (Editor-only) | Test Runner **EditMode** tab | Fast unit/integration tests needing no play loop (34 files) |
| `PlayMode/` | `DOTS.Tests.PlayMode` | Test Runner **PlayMode** tab | Tests needing frame updates, physics, scenes, or bootstrap flows (17 files, incl. `TestSystemBootstrap` helper and the `Smoke_BasicPlayable` smoke tests) |

Namespaces mirror the folders: `DOTS.Tests.EditMode` and `DOTS.Tests.PlayMode`.

## Running Tests

**In the editor:** Window > General > Test Runner, pick the EditMode or PlayMode tab, Run All.

**Command line** (editor must be closed — single-instance lock):

```powershell
& "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" `
    -runTests -batchmode `
    -projectPath "<project-path>" `
    -testPlatform EditMode `   # or PlayMode
    -testResults results.xml -logFile run.log
```

Note: a handful of PlayMode tests use `WaitForEndOfFrame` or spawn player visuals and cannot pass in batchmode — run those in the editor. Known pre-existing failures are tracked in the cleanup plan (§6.7 S14/S15).

## Writing New Tests

1. EditMode if it needs no play loop, PlayMode otherwise; put the file directly in that folder.
2. `[TestFixture]` on the class, `[Test]` for simple tests, `[UnityTest]` for coroutine/frame-based tests.
3. Naming: `MethodUnderTest_Scenario_ExpectedBehavior`.
4. Tests create and dispose their own DOTS worlds — see `PlayMode/TestSystemBootstrap.cs` for the bootstrap-system helper.
5. New dependencies go in the folder's `.asmdef` references.

## Related Docs

- `Assets/Docs/Testing/` — smoke-test scene setup and test plans
- `Assets/Docs/Process/CODEBASE_SIMPLIFICATION_PLAN.md` §6.1 R48 — how this layout came to be
