# Audio Spec (MVP) — event-driven managed sound layer

**Status:** DESIGN (PROPOSED — open decisions in §8; discuss before building)
**Last Updated:** 2026-07-19
**Owner:** Audio / Presentation
**Keywords:** audio, sound, SFX, ambient, music, AudioSource, comet drop, landing, wind, event-driven, managed layer

---

## 1. Purpose

The project has **no sound** today — only an `AudioListener` on the player camera
(`PlayerEntityBootstrap`), nothing that emits. MVP needs audio (the comet drop especially reads as
silent film without it). This spec defines the **smallest architecture that gets us there** and the
MVP sound list, so we stop short of a mixer-graph rabbit hole while leaving room to grow.

The guiding analogy: audio is **presentation driven by gameplay events**, exactly like the atmosphere
authority (`TimeOfDayController` broadcasts) and the meteor VFX (managed overlay polling a gate
signal). It should reuse those same signals, not invent a parallel event bus.

## 2. Scope (MVP)

- A small **managed audio layer** (Unity `AudioSource`/`AudioClip` — the API is managed) that plays
  **one-shots and loops** in response to gameplay signals.
- The MVP **sound list** (§6) and the **trigger hook** for each (§7).
- Pooled `AudioSource`s + a static façade so any layer (managed bootstrap, or an ECS system via a
  tiny bridge) can request a sound without holding references.
- A master enable + volume flag (§8 decides the home).

## 3. Non-Goals (MVP)

- **No ECS/DOTS audio system.** Unity audio is managed; DOTS has no built-in audio. A managed service
  driven by ECS *signals* is correct — do not build an audio ISystem. *Revisit when:* a profiler shows
  the managed layer can't keep up (it won't at MVP source counts).
- **No mixer graph / snapshots / DSP effects** (reverb zones, ducking buses). One master volume, maybe
  per-category later. *Revisit when:* the sound list is large enough that manual gain balancing hurts.
- **No dynamic music system** (layered stems, combat/explore transitions). At most one ambient/music
  bed (§8 open decision).
- **No determinism requirement.** Audio is presentation; it may read frame time, camera, RNG freely
  (unlike everything under `WORLD_STRUCTURE_SPEC.md` §3). It is never saved.
- **No spatialized 3D mix pass** beyond Unity's built-in `spatialBlend` per source. Positional relic
  ambience etc. is post-MVP.
- **No high-frequency / destruction audio, and no per-event playback for it.** The MVP sound list (§6)
  is all low-rate one-shots and loops, so each hooks a single `GameAudio` call at its event site. This
  does **not** extend to destructible terrain: a naive `GameAudio.PlayOneShot` per SDF edit / fractured
  fragment / physics contact is the "sound per removed voxel" trap and will flood the pool. When
  terrain-edit or destruction audio arrives (the next §7 consumers), it must first **aggregate
  observations over a short time+space window** (per material, per spatial cell, per frame) into a few
  representative requests — not fire one sound per event. *Revisit when:* the first destructible or
  herd/creature sound is wired; treat aggregation as a design requirement of that ticket, not a later
  optimization.

## 4. Architecture

- **Home = managed layer**, sibling to the VFX bootstraps. One `AudioDirector` MonoBehaviour
  (scene bootstrap, the `AuthoredAnchorBootstrap`/`MeteorDescentVfx` pattern) owns a small pool of
  `AudioSource`s + the loaded clip table; a static `GameAudio` façade (`GameAudio.PlayOneShot(id)`,
  `GameAudio.SetLoop(id, on)`, `GameAudio.SetVolume(id, v)`) is the call surface, so callers never hold
  `AudioSource` references (mirrors `DebugSettings` / `AtmosphereBroadcast` static-façade convention).
- **Clip identity = a `SoundId` enum** (not string keys) so calls are compile-checked; the director
  maps `SoundId → AudioClip` from a serialized table (inspector-authored, no `Resources.Load` string
  matching — the lesson from the compute-kernel name-match pitfalls).
- **Triggering: event-driven, reuse existing signals (§7).** Two bridge shapes already in the codebase:
  1. **Poll a singleton/component** each frame from the managed side — exactly how the V14 meteor
     overlay detects gate release ("player exists && gate component gone"). Cheap, no new plumbing.
  2. **A managed C# event** raised by a thin ECS system for one-frame events (e.g. landing impact) —
     add only if polling is awkward for that signal.
- **Pooling:** N (~8) reusable `AudioSource`s for one-shots (round-robin), plus a few dedicated
  looping sources (ambient bed, comet roar). Avoids per-shot `AudioSource` instantiation GC spikes.
- **`AudioListener`** stays where it is (player camera). One listener only (bootstrap already guards
  against duplicates).

## 5. Asset sourcing

No audio assets exist. Clips live in a new `Assets/Audio/` folder. Provenance follows the existing
third-party rules — screen per `Process/THIRD_PARTY_ASSET_EVALUATION_PLAYBOOK.md`, and **do not commit
vendor/creator-named packs into git** (standing owner convention); prefer CC0/self-made, committed as
loose project clips. Asset acquisition is a **dependency** of every §6 line — the layer can ship with
placeholder clips (even silence) so the plumbing lands before the final sounds.

## 6. MVP sound list

| Sound | Type | Read | Priority |
|-------|------|------|----------|
| **Comet descent roar** | loop (ducks to impact) | sustained rumbling roar through the scripted drop, intensifying, cut/impact on landing | **P0** (the ask) |
| **Landing impact** | one-shot | heavy thud/boom as the player lands out of the drop; volume scales with impact speed | **P0** |
| **Plains wind ambient** | loop | continuous low wind bed under everything | **P1** |
| **Slingshot charge** | loop/rising | tension riser while charging (ties to C1) | **P2** |
| **Launch release** | one-shot | crack/whoosh on launch (ties to C2) | **P2** |
| Footsteps / terrain-edit / relic ambience / music bed | — | **post-MVP** (see §3, §8) | — |

Note the comet roar can **reuse `MeteorDescentVfx.EvaluateIntensity`** (the V13 envelope) as its volume
curve — the fire and the sound then rise and fall as one, for free.

## 7. Trigger hooks (signal → sound)

| Signal | Exists today? | Drives |
|--------|---------------|--------|
| Meteor shell install / gate present | ✅ V14 (`PlayerStartupReadinessGate`) | start comet roar |
| Gate release (component removed) | ✅ V14 (overlay already polls this) | duck roar → landing impact; stop descent VFX |
| V13 descent intensity envelope | ✅ `MeteorDescentVfx.EvaluateIntensity` | comet roar volume |
| Landing impact | ⏳ lands with **C3** (`LandingImpactEvent`, one-frame) | landing thud (scale by vertical speed) |
| Slingshot charge / launch | ⏳ movement mode (`PlayerMovementMode`) + C1/C2 | charge riser / launch whoosh |
| Scene / player ready | ✅ bootstrap | start ambient wind loop |

Where a signal isn't built yet (C1/C2/C3), the audio hook lands **with that ticket** — the `GameAudio`
façade call is one line at the existing event site.

## 8. Open decisions (discuss before building)

1. **Music bed in MVP?** A single low ambient/music pad under the wind, or wind-only for MVP? (Leaning
   wind-only + the pad as a fast follow.)
2. **Settings home:** master enable + volume on `ProjectFeatureConfig` (matches the other feature
   flags) vs a new `AudioSettings` asset (room for per-category volumes). Leaning `ProjectFeatureConfig`
   for MVP (one flag), split out later.
3. **Ticket track letter.** Propose a new track for audio work (ID scheme in `Tickets/TICKETS.md`);
   letter is owner-discussed per convention — not assigned here. First tickets would be: the
   `AudioDirector` + `GameAudio` façade + pool (the plumbing), then the comet roar + landing as its
   first consumers.
4. **Clip acquisition path** — CC0 pack vs generated vs self-recorded — per §5; affects timeline more
   than architecture.

### Known growth path (not MVP decisions — recorded so §8 frames them)

The MVP layer is the correct first slice of a fuller managed-presentation architecture (ECS decides
*what*, the managed bridge decides *how*). Two upgrades are the most likely first steps beyond MVP, and
both are **additive** — the `SoundId` + `GameAudio` façade hides them from callers, so neither is a
rewrite:

1. **Serialized `SoundId → AudioClip` table → a `SoundCatalog` ScriptableObject** carrying per-sound
   clip set, volume/pitch ranges, spatial settings, concurrency/cooldown and priority. Adopt when the
   sound list outgrows one flat inspector table or manual gain-balancing starts to hurt.
2. **Signal-polling triggers → a singleton `DynamicBuffer<SoundRequest>`** drained once in the
   presentation layer, appended via ECB from jobs. Adopt when many systems (esp. parallel jobs) start
   emitting sounds and the per-signal poll/event wiring gets awkward — *not* before; polling reuses the
   V14 gate pattern and needs no new plumbing at MVP source counts. A voice allocator (distance /
   concurrency / cooldown / priority-stealing) rides on top of this buffer when it lands.

## 9. Acceptance (MVP slice)

- Comet drop plays a sustained roar that rises with the descent and resolves into a landing impact —
  no silence over the arrival beat.
- Ambient wind loops under normal play; one master mute/volume works.
- No GC spikes from audio (pooled sources); one `AudioListener`; no console warnings.
- Adding a new sound = one `SoundId` entry + one table row + one `GameAudio` call at the event site.

## 10. Related Docs

- `Rendering/METEOR_ARRIVAL_SEQUENCE_SPEC.md` — the comet drop; audio's first consumer (V13/V14 signals)
- `Tickets/vista-moment.md` — C1/C2/C3 Camera Feel events the later sounds hook into
- `Process/THIRD_PARTY_ASSET_EVALUATION_PLAYBOOK.md` — clip provenance / third-party screening
- `../DOCUMENTATION_SYSTEM_SPEC.md` — doc conventions this follows
