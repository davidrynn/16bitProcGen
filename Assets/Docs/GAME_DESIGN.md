# Game Design (GDD) — working title TBD

**Status:** DESIGN — v0.1 (minimal draft, built to iterate — do not treat as settled)
**Last Updated:** 2026-07-21
**Owner:** Design

**Purpose:** One current, minimal answer to *"what is the game, what makes it fun, and why keep
playing"* — the connective tissue the technical specs assume but never state. It reconciles the
established pillars (`MASTER_PLAN.md` §1), the movement-feel thesis (`MOVEMENT_PLANNING.md`), the
vista hook (`MVP_VISTA_MOMENT_SPEC.md`), and the node-building mechanic (`MAGIC_GRID_SPEC.md`) into
one loop. Deliberately minimal — expand by iteration, not up front.

### Scope — what this document is, and is not

> **This is the authoritative gameplay-intent document.** It defines the player fantasy, the core
> loop, and the design principles that decide *why* a feature earns its place.
>
> **It does not track implementation status, sequencing, or schedule.** Nothing here should be read
> as a claim that something is built.
>
> - Execution sequencing and phase direction: [`MASTER_PLAN.md`](MASTER_PLAN.md) — the *what* and *when*
> - Current execution state: [`Tickets/TICKETS.md`](Tickets/TICKETS.md) — the live truth
>
> When intent and roadmap disagree, **this document wins on intent** and `MASTER_PLAN.md` wins on
> ordering. `MASTER_PLAN.md` §1 adopts §2's north star and §6's two joys as its product thesis, so
> the two are meant to stay in lockstep — if they drift, fix the drift rather than picking a side.

---

## 1. Pitch

A crafting-and-exploration sandbox where **getting there is the best part.** You arrive on a strange
procedural world and the joy is *moving through it* — slingshotting across plains toward impossible
ruins — gathering, surviving, and raising settlements that snap themselves into beautiful form as you
place them.

## 2. Pillars & the north star

Pillars (established, `MASTER_PLAN.md` §1): **Exploration · Destruction · Movement · Crafting · Minimalism.**

**North star — the thing that makes us different:** in most crafting games, travel is a *tax* you pay
between the fun (Minecraft: hold-W across empty terrain for five minutes). Here, **travel is the
fun** — traversal is a skill toy you *want* to use for its own sake. Every other system serves that.

## 3. The core tension (design on purpose)

Two truths that pull against each other, stated so we resolve them deliberately:

- **Travel & exploration must be enjoyable**, never a drag. Movement is intrinsically fun
  (`MOVEMENT_PLANNING.md`: "fun even without objectives").
- **Grind, difficulty, and struggle are necessary.** Game psychology needs *earned* achievement;
  frictionless games feel hollow.

**Resolution (the design stance this whole game hangs on): put the friction in the _world_, never in
the _locomotion_.**

| Keep (meaningful friction) | Cut (dead-time friction) |
|----------------------------|--------------------------|
| Scarcity of resources | Straight-line travel tedium |
| Danger / difficulty of places | Inventory busywork |
| Skill mastery (movement, building) | Backtracking as a time-sink |
| Earning progression | Waiting / padding |

Travel is the **reward-delivery vehicle**, not the grind. You never pay tedium to get somewhere — the
getting-there is the toy. You grind by *doing interesting things in interesting places*, and the trip
between them is a pleasure, not a cost.

**The corollary — every survival pressure must have a buildable answer.** The trap most crafting games
fall into is *maintenance you wait through*: hunger you pause to eat past, durability you stop to
repair, encumbrance you shuffle in menus. Our test is different — a survival pressure earns its place
only if its *resolution is construction*:

- Hunger → you build a **farm**.
- Durability → you build a **forge**.
- Encumbrance / the long haul home → you build a **teleporter** (a magic-chest link between nodes).

Same "maintenance" flavor, opposite effect: the pressure becomes a *reason to build*, a reason to claim
a node, and a reason to move back through a world you already enjoy traversing. It feeds three pillars
instead of interrupting one. **Keep the pressures whose answer is a building; cut the ones whose answer
is waiting** (corpse runs, inventory Tetris, durability babysitting with no build behind it).

*But buildable ≠ frictionless, and not everything should transport.* Node-to-node goods movement is a
reward you unlock, not a default — and it is deliberately *partial*. Certain regions hold certain ores,
and getting a material from where it is to where you need it is itself content: the geography of
scarcity is world-friction we **keep**. A teleporter might move common goods freely while region-bound
materials still have to be *carried* — so the world stays a puzzle of "how do I get **this** from A to
C," not a solved logistics grid. Which resources bind to their region (and how tightly) is a §7 tuning
question, not settled here.

## 4. Core loop (now causal — each step *because* of the last)

The old version listed activities; this one states *why each produces the next* — the loop is an
economy, not a checklist.

```
ARRIVE (meteor) — a mystery on the horizon (the vista relic) hands you a first destination.
  → you TRAVEL toward it, BECAUSE traversal is the toy and the world is the playground.
  → in crossing, you DISCOVER what lies between here and there — biomes, ruins, region-bound
      resources — AND a farther, stranger destination beyond it.
  → to claim those resources you must GATHER + SURVIVE, BECAUSE the world resists you: scarcity,
      danger, distance, the pressures of staying alive. That resistance is what makes arrival mean
      something (§3).
  → each of those pressures has a buildable answer, so you BUILD — a farm for hunger, a forge for
      durability, a teleporter for the haul — claiming a node AS the infrastructure that answers the
      world's demands.
  → that infrastructure IS PROGRESS: it extends your reach (movement, tools) and your building
      vocabulary, and it immediately opens ground that was unreachable a moment ago.
  → which puts a farther, harder, stranger destination on the horizon — repeat, deeper.
```

The engine of the loop: **the world creates a need → the need is answered by building → building
extends reach → reach reveals a new need farther out.** Exploration is gated by *capability you earn*,
never by tedium. The "one more run" pull is structural: there is always a farther, stranger place just
past what you can currently reach, and always a fresh demand that a new node would answer.

### 4.1 The opening — one-time, authored _(owner-settled 2026-07-21)_

The loop above begins mid-sentence at ARRIVE. This is what precedes it, and it is a **one-time story
beat**, not a systemic sequence replayed each session.

1. A meteor streaks across the sky. The camera travels with it, **previewing biomes** the player has
   not reached and **creatures that look up** as it passes — the world is shown to be inhabited and
   varied *before* the player can reach any of it.
2. The meteor — the player — **lands in a crash**. The player is **injured**; vision blurs.
3. Through the blur, a **mysterious being** approaches, **takes the player's hand**, and **imbues
   them with powers** — traversal first, others later.

*Why it earns its place:* this is a **diegetic tutorial that hands you the toy the game is about.**
The north star (§2) is that travel is the fun; the opening's climax is literally being given the
ability to travel. It also plants the two hooks the loop needs — a world worth crossing (the biome
preview) and someone who knows more than you (the being).

The aerial preview does double duty: it is the *far* horizon promise, where §4's vista relic is the
*near* one.

**Open — not settled here:** who the being is, whether it recurs, and whether it connects to the
ossified-god framing in §7.5.

## 5. Building — earned depth, effortless beauty

Two modes, both from `MAGIC_GRID_SPEC.md`:

- **Freeform** — place pieces one at a time, anywhere. Full control, more effort.
- **Fast-build (WFC, Townscaper-style)** — on a **magic node**, building is *power-assisted*: as each
  piece is added, the structure **auto-resolves into a coherent form** (walls grow corners, roofs
  close, a hut becomes a tower becomes a keep). You place *intent*; the wave-function-collapse system
  makes it look good for free. Low effort, high delight — the Townscaper hook.
- **Progressive + earned** — build pieces and node capability unlock over time, so the vocabulary
  grows with the player: a few pieces early, cities and fortresses later.

*Why it fits the north star:* fast-build stops building from becoming its own grind. The effort is in
*earning* the pieces and *reaching* the nodes — not in fiddly placement.

## 6. Progression (minimal — to expand)

Progression should make the **two joys deeper**: move better, build better.

- **Movement unlocks** (glide, chain-slingshot, thermals — already in the Movement backlog) raise reach
  and the skill ceiling.
- **Tool / build unlocks** (a drill / explosive / shaping / grapple-hand tool tree, per the archived
  production plan) extend what you can destroy, gather, and build.
- Unlocks are **earned through the struggle** (§3) and immediately **open new territory** (§4) —
  progression and exploration are the *same axis*.

### 6.1 The gem — powers are fiction-wrapped, not gem-gated _(owner-settled 2026-07-21)_

The powers granted in §4.1 are expressed through a **gem set into the player's forearm or hand**
(which reads better is undecided). In fiction, the player **turns or touches the gem** to activate a
power; in play, that corresponds to a keypress.

**The gem is a story device, not a dynamic.** Owner call, and worth stating plainly because it is a
scoping gift: the gem **gates nothing, costs nothing, and has no resource behind it.** It is the
narrative and visual wrapper around ability activation — the reason the player has these powers and
the object they touch to use them. A power works identically whether or not the gem is ever drawn.

*Why it earns its place:* it converts a menu of abilities into a **single diegetic object the player
owns**, granted by a character in the opening. Powers stop being a UI and become a possession — which
matters for a game whose fantasy is *becoming capable of crossing this world.*

Powers it wraps, current thinking:

| Power | Status in the fantasy |
|---|---|
| **Traversal** — slingshot, glide, and successors | The first grant, the north star made playable |
| **WFC fast-build** (§5) | The building joy, framed as a power rather than a menu |
| **Telekinesis** — grab items at range | New idea; answers "reaching a thing" without the trip being tedious |

Additional powers are expected; the gem is the frame that lets them be *given* rather than unlocked
in an interface.

*Ideal but not required:* a hand/forearm animation for activation. Nice, plausibly beyond MVP, and —
because the gem gates nothing — **its absence blocks no power from shipping.**

## 7. What we are NOT deciding yet (iterate on these)

1. **Currency of progression** — resources? discovery? building milestones? a mix?
2. **Win / endgame** — a goal, or open-ended sandbox? (MVP is open-ended.)
3. **Shape of the struggle** — enemies/mobs? environmental hazard? survival needs (hunger/cold)? how
   much combat vs. environmental challenge?
4. **The alignment hook** — the archived good-vs-evil / order-vs-corruption fantasy (your builds shift
   the world's alignment). Revive, cut, or reshape?
5. **Setting fiction** — the ossified-god / colossus-remains framing (the V11 hero relic). How central
   is it to the fantasy, and does it carry a narrative? _(Partly answered 2026-07-21: §4.1 establishes
   there **is** a narrative and at least one character — the mysterious being. Still open: who they
   are, whether they recur, and whether they connect to the ossified gods.)_
6. **Session shape** — persistent world, run-based, or both?
7. **Working title / name.** The Hand of the Colossus...?

## 8. Related docs

- `MASTER_PLAN.md` — pillars + phase roadmap. This doc is the *"why it's fun"* companion to that
  *"what / when."*
- `Rendering/MVP_VISTA_MOMENT_SPEC.md` — the opening hook (see-it-across-the-plain).
- `Player/Movement/MOVEMENT_PLANNING.md` — the travel-is-fun thesis, in depth.
- `Structures/MAGIC_GRID_SPEC.md` — the node-building / WFC fast-build mechanic.
- `Rendering/METEOR_ARRIVAL_SEQUENCE_SPEC.md` — the onboarding beat.
- `Archives/MVP_Ideation_2026/mvp_feature_list.md` — the earlier (archived) core loop + alignment
  system; disowned at the vista pivot but worth mining for §7.
