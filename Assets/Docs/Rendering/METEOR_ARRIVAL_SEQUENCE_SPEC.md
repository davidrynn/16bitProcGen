# Meteor Arrival Sequence Spec

**Status:** ACTIVE — V14 shell built 2026-07-18 (see §12 build record); V13 not started
**Last Updated:** 2026-07-18
**Owner:** Rendering / Vista
**Phase:** Vista MVP follow-on (tickets V13 + V14)
**Keywords:** meteor, sky-drop, loading, diegetic loading screen, readiness gate, spawn sequence, VFX, first-person, UI overlay

---

## 1. Purpose

Turn the game's opening — currently a player silently hanging at ~400u while the world streams in,
then falling — into one continuous diegetic beat: **you arrive as a meteor.** The player starts
*inside* the meteor (a full-screen interior that doubles as the initial loading screen), the shell
breaks open when the world is actually ready, the descent burns with flames and embers that
extinguish as you fall, and the landing hands off to the existing dust-burst plan. The loading
screen never lies: the signal that opens the shell is the real readiness gate, not a fake progress
bar.

Owner decisions baked in (2026-07-08): interior is **binary** (rumble → open) with a minimum hold,
progress-driven crack glow is polish; scope is the **initial spawn only**; the meteor is a property
of the **arrival sequence**, never of altitude or fall speed.

---

## 2. Current State (what exists, 2026-07-08)

- **There is no loading UI of any kind.** The project has no UI system beyond debug overlays; this
  sequence introduces the first UI element.
- **The substance of a loading system already exists**: the sky-drop readiness gate in
  `PlayerEntityBootstrap` (ticket V7). The player spawns at ~400u under a **gravity hold**; a ground
  probe (`ProbeDistance = max(96, spawnY + 64)`) gates release on near-spawn collider readiness,
  with an **~8s timeout fallback**. Colliders build player-nearest-first (V10). Today this plays out
  in full view — the player watches terrain, scatter, and relics pop in below.
- The descent itself is plain free-fall. Camera Feel **C2** (speed lines, FOV punch — velocity-
  thresholded, mode-agnostic) and **C3** (landing dip + dust burst) are specced but unbuilt.

---

## 3. The Arc

```
Phase 0  Engine scene load            → out of scope (Unity default; brief)
Phase 1  INTERIOR   (= loading)       → screen filled by meteor inside: dark rock vignette,
                                        glowing cracks, rumble + light shake. Gravity held.
Phase 2  BREAK-OPEN (= gate release)  → cracks flare, shell shatters/burns away, view opens
                                        to the plain below. Gravity releases the same beat.
Phase 3  BURNING DESCENT              → screen-edge flames + embers streaming past, burning
                                        off over the fall (altitude band or elapsed time).
Phase 4  IMPACT                       → hands off to C3 landing dust burst. Meteor becomes
                                        crater; play begins.
```

Phase 2 — the break-open moment — is the **shared contract** between the two ticket slices: V14
owns Phases 1–2, V13 owns Phases 3–4's VFX (C3 owns the impact itself).

---

## 4. Scope

**In scope:**

1. **V14 — Meteor-interior loading shell.** Full-screen interior overlay shown from the first frame
   of our code until gate release; break-open transition; the DOTS→managed **gate-state bridge**
   that drives it.
2. **V13 — Burning-descent VFX.** First-person flame/ember layer ignited at break-open, burn-off
   curve, C3 handoff. Dev-toggle (third-person) smoke trail as stretch.
3. A **minimum interior hold** (~1.5–2s) so a fast load doesn't flash-open; the gate's 8s timeout
   means the shell always opens eventually (failure handling inherited for free).

**Out of scope / Non-Goals:**

- **No fake progress.** No progress bar, no invented percentages. Binary state for MVP; if progress
  display is ever wanted, drive crack-glow intensity from real readiness (fraction of near-spawn
  colliders built) — listed as polish in §8.
- **No altitude/velocity trigger for flames.** A player who gets very high later (slingshot,
  thermals, towers) falls with C2's wind/speed-line vocabulary, not fire — a person falling is not
  on fire. If a future mechanic warrants gameplay re-entry (e.g. a super-slingshot that "exits the
  atmosphere"), promote the trigger from "spawn sequence" to an explicit atmospheric-entry state
  *then* — per the project's case-by-case philosophy, do not build that generality now.
- **No respawn/fast-travel reuse** in v1 (natural later extension; must not shape the MVP).
- **Phase 0** (engine-level load before any bootstrap runs) — brief, not worth a loading scene.
- **No third-person fireball as a primary deliverable** — MVP is first-person only; the full-body
  meteor ball is invisible in normal play (dev V-key toggle only).

---

## 5. Design

### 5.1 V14 — Interior shell (Phases 1–2)

- **Visual:** one full-screen overlay — a Canvas image or camera-space quad (keep it to **one
  canvas + one controller MonoBehaviour**; no broader UI framework). Dark rocky interior vignette
  with glowing cracks; subtle pulse. Rumble audio + light camera shake sell "inside something
  falling."
- **Break-open:** cracks flare → shell shatters away from the screen edges (UI shatter animation or
  a burn-away/iris shader wipe — pick during build by cheapness). Syncs with gravity release and
  V13 ignition in the same beat.
- **Gate-state bridge:** the readiness gate lives ECS-side in `PlayerEntityBootstrap`. Expose its
  state via a small singleton component the overlay controller polls (the established DOTS→managed
  bridge pattern, cf. how managed rendering reads world state elsewhere). UI stays a managed
  concern, same architectural category as `SkyController`.
- **Timing:** `shellOpens = max(gateReleased, minimumHold ~1.5–2s)`. Gravity release should follow
  the shell opening, not precede it — the player should not silently start falling behind an
  opaque overlay. (Today the gate releases gravity directly; V14 inserts the shell as the visible
  face of the same moment. If decoupling gate-release from gravity-release is invasive, opening the
  shell on the gate signal and accepting one frame of coupled release is acceptable for MVP.)

### 5.2 V13 — Burning descent (Phase 3)

First-person vocabulary (the player cannot see themselves):

- **Flame tongues at the screen edges**, streaming upward (opposite velocity) — the core cue.
- **Embers/sparks flying past** the camera.
- Optional: subtle heat wobble, warm vignette tint; roaring audio that fades with the flames.
- Stretch (dev toggle / future cinematics): world-space **smoke trail** behind the player body.

Mechanics:

- **Ignites at break-open** (V14 signal), **burns off** over the descent — fade by altitude band
  (e.g. fully out below ~150u) or elapsed time; tune visually. Fully extinguished before landing so
  C3's dust burst reads clean.
- Rides the same camera rig C2 plans (camera-parented particles / screen-space layer). Build V13's
  emitters so C2's speed lines can share the mount later; do not block on C2.

### 5.3 Why this also helps rendering

The interior shell covers the ugliest visual moment the game has — terrain/scatter/relic pop-in
during streaming — and the descent's embers/edge flames further mask the residual streamed-window
detail cutoff accepted in `ATMOSPHERE_COLOR_AUTHORITY_SPEC.md` §6b. Both are bonuses, not the
purpose; the sequence must stand as spectacle alone.

---

## 6. Compute & Perf Budget

- Interior: one transparent overlay quad + a shake — negligible.
- Descent: two or three camera-parented particle systems at modest counts + optionally one
  screen-edge quad shader. Near-camera fill on a vertex-bound scene
  (`RENDER_PERF_PROFILE_REPORT.md`) — the cheap kind. No post-processing volume, no new passes on
  world geometry, no textures beyond small particle sprites.

---

## 7. Rollout / Tickets

1. **V14 — Meteor-interior loading shell** _(higher value: hides pop-in and IS the loading
   system)_. Gate-state bridge → overlay controller + interior art → break-open transition →
   min-hold + timeout behavior. Acceptance: §9.1–9.4.
2. **V13 — Burning-descent VFX.** Ignition on V14's break-open signal → flame/ember layer →
   burn-off curve → C3 handoff. Acceptance: §9.5–9.7. Buildable after V14 (needs the break-open
   signal; a debug trigger suffices to develop it in parallel).

---

## 8. Polish (explicitly deferred)

- Progress-driven crack glow (fraction of near-spawn colliders built) — honest progress, not MVP.
- Respawn/fast-travel reuse of the sequence.
- Third-person smoke/fireball for cinematics or multiplayer visibility.
- Atmospheric-entry as a gameplay state (only if a mechanic ever warrants it).

---

## 9. Acceptance Criteria

1. From the first frame of gameplay code, the player sees the meteor interior — never the world
   assembling (no terrain/scatter/relic pop-in visible on initial load).
2. The shell opens only when the readiness gate releases (or its timeout fires), never before the
   minimum hold elapses; a deliberately slowed load keeps the shell closed correspondingly longer
   with no visual breakage.
3. The player does not fall while the shell is closed (or at most one coupled frame, per §5.1).
4. Break-open reads as one beat: crack flare → shatter → world revealed → falling, with V13 flames
   igniting in the same moment.
5. Flames/embers read clearly in first person during the upper descent and are fully extinguished
   before landing.
6. Ordinary later falls from any height (slingshot, terrain) show **no** meteor effects — C2's
   speed vocabulary only.
7. Frame cost of the sequence is imperceptible (no measurable hit vs. the current drop).

---

## 10. Related Docs

- [MVP_VISTA_MOMENT_SPEC.md](MVP_VISTA_MOMENT_SPEC.md) — the opening-beat umbrella this serves.
- [ATMOSPHERE_COLOR_AUTHORITY_SPEC.md](ATMOSPHERE_COLOR_AUTHORITY_SPEC.md) §6b — the residual
  streamed-window detail cutoff the descent VFX incidentally masks.
- `../Player/Movement/MOVEMENT_PLANNING.md` Steps 5/7 — C2 speed lines (shared camera-rig mount),
  C3 landing dust (Phase 4 handoff).
- `../Player/PLAYER_BOOTSTRAP_FIX_SPEC.md` / ticket **V7** — the readiness gate this sequence gives
  a face to; **V10** — nearest-first collider builds it waits on.
- Tickets: `../Tickets/vista-moment.md` **V13**, **V14**.

---

## 11. Open Questions

- ~~Break-open technique~~ **Resolved (2026-07-18, V14 build):** burn-away shader wipe — a
  procedural dissolve (cracks + screen center open first, edges last, burning rim at the front)
  on the overlay's own packed texture. No shatter animation assets needed; see §12.
- Burn-off driver: altitude band vs. elapsed time (lean altitude — it tracks what the player
  sees). Still open — V13.
- ~~Gravity release vs. gate release~~ **Resolved (2026-07-18, V14 build):** decoupling was
  unnecessary — the min-hold moved *into the gate* (`PlayerStartupReadinessGate.MinHoldSeconds`),
  so the gate itself never releases before the hold and gravity release + shell break-open stay
  one beat by construction (the accepted §5.1 coupling, now with no silent-fall window at all).

## 12. Build record — V14 (2026-07-18)

- **Gate-state bridge = the gate component itself.** No new singleton: the overlay polls
  "player entity exists **and** no longer has `PlayerStartupReadinessGate`" — the same contract
  the PlayMode smoke test already polls. Removal of the component *is* the release signal.
- **Min-hold lives ECS-side** (see §11 resolution): new `MinHoldSeconds` on the gate, clamping
  **both** release paths (terrain-ready and timeout) so the shell can never flash-open (§9.2).
  Release predicate factored to `PlayerStartupReadinessGate.ShouldRelease` (pure static);
  EditMode contract tests in `MeteorArrivalGateTests`. Non-sky-drop spawns get `MinHoldSeconds
  = 0` — gate timing byte-identical to pre-V14.
- **Config:** `ProjectFeatureConfig.EnableMeteorArrivalShell` (default on) +
  `MeteorShellMinHoldSeconds` (default 1.75). Shell only installs when `EnableSkyDropSpawn` is
  also on; disabling the shell zeroes the min-hold.
- **Overlay:** `MeteorShellOverlay` (static install + one controller MonoBehaviour, the
  `ReticleBootstrap` pattern — runtime-built canvas, no scene wiring), installed from
  `DotsSystemBootstrap.Awake` so the screen is covered before the first rendered frame. Visuals
  are fully procedural: one packed 512² texture (R rock FBM, G Voronoi-edge crack mask,
  B radial) generated at install, one canvas shader
  (`Resources/Shaders/MeteorShellOverlay.shader`) doing rock vignette + pulsing crack glow +
  the dissolve. Rumble is unscaled-time Perlin jitter on the overlay rect (with overscan so
  edges never show); no camera-rig changes. 20 s fail-safe force-open so the shell can never
  trap the player if no release is ever observed.
- **Deferred from this slice:** rumble *audio* (no audio pipeline exists yet — first
  AudioSource is its own decision), progress-driven crack glow (§8 polish), respawn reuse.
- **Remaining:** owner eyeball of the full beat in play (interior read, flare→dissolve timing,
  min-hold feel); V13 ignition hook (V13 can key off the same gate-removal poll or a callback
  added to the overlay then).
