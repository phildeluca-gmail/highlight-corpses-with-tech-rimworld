<!-- Architecture for Highlight Corpses With Tech. Stub created 2026-08-29; designed the same day from a Q&A with the user. Section 8 lists what is still open. NOTHING HERE IS A GO-AHEAD TO BUILD - the design is agreed, the implementation is not ordered. -->

# Highlight Corpses With Tech - Architecture

**Status: designed 2026-08-29, not implemented.** The folder `HighlightCorpsesWithTech/`
holds a scaffold that loads and logs one line. Everything below is design.

**Ordered:** 2026-08-27 (as a name only). **Designed:** 2026-08-29.

---

## 1. The ask

**Verbatim, 2026-08-27:**

> "New mods coming, just build separate folders for each: Highlight
> Corpses With Tech; Notify Ripe (options: Ambrosia, Berries); Notify
> Still Being Attacked; Uninstall Hotkey; Menu Hotkeys."

**Settled 2026-08-29, by direct answer:**

| Question | Answer |
|---|---|
| Does "with tech" describe the corpse or the highlight? | **The corpse.** It carries tech worth recovering. Not a rendering style. |
| What triggers it? | **Installed bionics and implants only.** Not mechanoid corpses, not worn apparel, not carried weapons. |
| Where is the cutoff? | **Industrial and up by default** - simple prosthetics included, peg legs excluded. |
| ...but configurable? | **Yes.** Verbatim: *"I want to configure the settings so I can check all of the installed tech, from peg legs up to archotech."* One toggle per tech tier. See section 4. |
| How does it appear? | **Alert plus on-map marker.** |
| Does it react to rot? | **No - CUT 2026-08-29.** The answer given was "escalate as it rots", but reading the installed R3 mod showed rot costs the player nothing. See sections 2 and 5.3. |

The user's own words on the trigger: *"If the corpse has ANY non-organic
tech besides peg legs, it is subject to this."* The tier toggles are how
"besides peg legs" becomes a setting rather than a hardcoded judgement.

---

## 2. Why this mod earns its place - and the risk that it does not

**In vanilla, a corpse's implants are believed to be unrecoverable.**
RimWorld lets you strip a corpse of apparel and weapons, but surgery is
performed on live pawns; there is no vanilla operation that extracts a
bionic from a body. If that is right, then in a vanilla game this mod
would highlight corpses you can do nothing about, which is worse than
useless.

**It is not a vanilla game.** The modlist already on file for this user
includes **Reclaim, Reuse, Recycle (Continued)** (`Mlie.ReclaimReuseRecycle`),
which adds a **harvesting table** whose WorkGiver is `R3_DoWorkHarvestCorpse`.
That is precisely a post-mortem implant recovery route. So the real job
of this mod is:

> Tell me which corpses are worth hauling to the harvesting table.

That reframing matters for every decision below - especially rot, which
is only interesting if decay actually costs you the implant. It does
not; see the verification immediately below.

### Verified against the installed copy, 2026-08-29

Read directly from
`E:\SteamLibrary\steamapps\workshop\content\294100\2567364887` (R3 1.5).

**1. Yes - R3 recovers implants from corpses.** Its own description:
*"Harvest implants and bionics from corpses and prepare them for reuse."*
The harvesting bench runs `R3_HarvestCorpseFlesh_*` recipes over the
`Corpses` category. So the premise of this mod holds in this install.

**2. No - rot does not matter. This is settled, and it kills section 5.3
as originally designed.** The evidence, in descending order of strength:

- **The R3 assembly contains no rot handling at all.** A byte scan of
  `ReclaimReuseRecycle.dll` for `Rot`/`Dessicat`/`Decay`/`Spoil`, in both
  ASCII and UTF-16, returns nothing. (An earlier scan appeared to confirm
  this but was worthless - `strings` is not installed here and the empty
  output was the tool missing, not the absence of matches. The Python
  scan is the real result.)
- **No harvest recipe filters on rot.** `Recipes_Harvest.xml` uses only
  `categories: Corpses` plus R3's own `specialFiltersToDisallow`
  (harvested / unharvested-by-complexity). Nothing about condition.
- **Vanilla's `AllowRotten` filter sits on `parentCategory: Root` with
  `allowedByDefault: true`**, so it does reach corpses - but it is on by
  default, meaning a harvest bill accepts rotten corpses unless the
  player deliberately turns it off.
- **Corpses are never destroyed by rot.** They stop at `Dessicated` and
  persist. There is no deadline to race.

**What actually degrades the prize is damage, not time.** From R3's own
documentation: *"Parts are either reclaimed as 'Non-Sterile' or
'Mangled' (or not at all) depending on their damage state."* Damage is
dealt by whatever killed the pawn - it does not accumulate while the
body lies there. **So there is no time pressure on the player at all**,
and any urgency this mod displays would be invented.

One residual unknown, low risk: whether vanilla strips hediffs at
`Dessicated`. Nothing suggests it does, and R3 would visibly break on
old corpses if it did. Worth an in-game glance, not worth blocking on.

**3. Should the mod depend on R3?** **No hard dependency.** Detection is
pure vanilla API, other recovery mods exist, and a dependency buys
nothing while costing portability. R3 is why the mod is *useful here*,
not what it is built on.

### Where the real value turns out to be

With no deadline, the mod is not "act before you lose it". It is:

> Of the bodies on this field, which ones are worth carrying to the
> bench - and which are already-harvested shells not worth the trip?

That is a **triage** tool, not an alarm. It changes the emphasis in
section 5: the marker, which tells you *which body in the heap*, is now
the more valuable half; the alert is the supporting cast.

---

## 3. Detection

All of the following is **verified by reflection** against
`lib/Assembly-CSharp.dll` on 2026-08-29, not assumed:

| Member | Type | Confirmed |
|---|---|---|
| `Verse.Corpse.InnerPawn` | `Pawn` | yes |
| `Verse.Corpse.Age`, `.Bugged`, `.CurRotDrawMode` | | yes |
| `Verse.HediffSet.hediffs` | `List<Hediff>` | yes |
| `Verse.HediffDef.countsAsAddedPartOrImplant` | `bool` | yes |
| `Verse.HediffDef.spawnThingOnRemoved` | `ThingDef` | yes |
| `Verse.ThingDef.techLevel` | `RimWorld.TechLevel` | yes |
| `RimWorld.CompRottable`, `RimWorld.RotStage` | | confirmed present, but **not used** - see 5.3 |
| `RimWorld.TechLevel` | `Undefined 0, Animal 1, Neolithic 2, Medieval 3, Industrial 4, Spacer 5, Ultra 6, Archotech 7` | yes |

### The rule

A corpse qualifies when **any** hediff on `InnerPawn.health.hediffSet.hediffs`
satisfies all three:

1. `hediff.def.countsAsAddedPartOrImplant` is true - this is vanilla's
   own flag for "artificial part or implant", so we are not inventing a
   definition of tech.
2. `hediff.def.spawnThingOnRemoved != null` - **the part leaves a real
   item behind.** This is the load-bearing condition. Something that
   vanishes on removal is nothing to haul a body for, whatever tier it
   claims.
3. `spawnThingOnRemoved.techLevel` is in the player's enabled tier set
   (section 4).

**Why not `HediffSet.IsBionicOrImplant`.** It exists and is public, but
its signature is `IsBionicOrImplant(BodyPartDef partDef)` - it answers a
question about one body part, not about the pawn. Wrong shape here.

**Why the tier comes from `spawnThingOnRemoved`, not the hediff.**
`HediffDef` has no tech level. The item the part turns into does, and
that item is exactly what the player is trying to recover, so its tier
is the honest one to rank by.

**Modded implants come along for free.** Nothing in the rule is a
hardcoded def list, so any mod that follows the vanilla pattern -
`countsAsAddedPartOrImplant` plus `spawnThingOnRemoved` - is detected
without a compatibility patch. A modded implant with a missing or
`Undefined` tech level lands in a tier the player can toggle like any
other; see section 4.

---

## 4. Settings

**Driven by the user's mid-design instruction:** *"I want to configure
the settings so I can check all of the installed tech, from peg legs up
to archotech."*

One checkbox per tech tier, **generated from the `TechLevel` enum rather
than hardcoded**, so a tier added by a mod cannot fall through a gap:

| Tier | Default | Typical contents |
|---|---|---|
| Neolithic | **off** | peg leg, hook hand, wooden foot |
| Medieval | **off** | denture |
| Industrial | **on** | simple prosthetic arm / leg |
| Spacer | **on** | bionic parts, joywire, painstopper |
| Ultra | **on** | high-end implants |
| Archotech | **on** | archotech parts |
| Undefined | **off**, shown last, labelled as a catch-all | modded implants that declare no tech level |

`Animal` is not offered - nothing implantable carries it.

**These tiers line up with R3's own complexity bands**, which is a happy
accident worth recording. From R3's documentation: its three tiers
*"mostly correspond to tech levels Animal-Medieval, Industrial-Spacer
and Ultra and above"*, though it also weighs price.

| R3 complexity | Tech levels | Our default |
|---|---|---|
| primitive | Animal - Medieval | **off** (this is the peg-leg band) |
| advanced | Industrial - Spacer | **on** |
| glittertech | Ultra and above | **on** |

So the agreed default set is exactly "R3 advanced and glittertech", and
turning Neolithic + Medieval on is exactly "also show me primitive".
**This is a coincidence of two sensible schemes, not a coupling** - do
not import R3's tiers or key off its defs. Ours are computed from
vanilla `TechLevel` and stand alone if R3 is absent.

**Research gating is deliberately ignored.** R3 gates each complexity
tier behind a research project, so early on you may be shown a corpse
you cannot yet harvest. Reading another mod's research state to suppress
that would be the dependency section 2 rejected, and knowing a body is
worth keeping until the research lands is arguably useful anyway.

The defaults reproduce the agreed "Industrial and up" line exactly, so
the mod behaves as specified out of the box, and turning Neolithic on
gets peg legs as asked. **Do not hardcode the peg-leg exclusion
anywhere** - it is a consequence of the default tier set and must stay
that way, or the setting is a lie.

Second setting, needed by section 5: **show the on-map marker** (on by
default). The alert has no toggle; a mod that can be silenced entirely
is just an uninstall.

---

## 5. Presentation

Two independent surfaces. **Neither is a Harmony patch** - this mod
needs no patches at all, which is worth stating plainly given every
other mod in this repo is patch-driven.

### 5.1 The alert

`Alert_CorpsesWithTech : RimWorld.Alert`. Vanilla's `AlertsReadout`
discovers `Alert` subclasses by reflection at construction (confirmed:
it has a parameterless ctor and an `AllAlerts` list), so **subclassing
is the whole registration** - no def, no patch, no XML.

- `GetReport()` returns `AlertReport.CulpritsAre(list)` so clicking the
  alert cycles the camera through the qualifying corpses. This is the
  entire answer to "the corpse is off-screen", which no on-map drawing
  can solve.
- `defaultPriority` scales with the worst rot state present - see 5.3.
- Label carries the count. Explanation lists the corpses and what is on
  each, because "3 corpses with tech" without naming the archotech eye
  is a prompt to go hunting manually.

### 5.2 The on-map marker

A `MapComponent` override drawing a small icon above each qualifying
corpse. **Marker, not a tint on the corpse graphic** - chosen
deliberately: tinting means touching the corpse draw path, and this
user runs ~60 mods including Performance Fish and RocketMan, which
already patch rendering and tick paths heavily. An overlay icon cannot
collide with that; a draw-path change might.

The marker exists to answer the one question the alert cannot: *which
body in this heap of nine is the one worth hauling.*

### 5.3 Rot escalation - CUT

**Do not build this.** Section 2 settled it: rot costs the player
nothing, corpses are never destroyed by decay, and R3 does not care
about condition. An escalating marker would be inventing urgency that
the game does not have, and training the player to hurry for no reason
is worse than staying quiet.

The rot answer given during design was "escalate as it rots", but that
was chosen on the assumption decay destroyed the implant. The evidence
says otherwise, so the design overrides the preference here rather than
building something that lies. **One marker state. No escalation. No rot
reading at all** - `CompRottable` is not needed by this mod.

### 5.4 What replaces it - already-harvested corpses

The genuinely useful distinction is not fresh-vs-rotten, it is
**still-has-parts vs already-stripped**. R3 ships exactly this as
`SpecialThingFilterDef`s over the `Corpses` category:

| R3 filter | Means |
|---|---|
| `R3_AllowHarvested` | corpse has **no** remaining added parts |
| `R3_AllowUnharvested_Primitive` | parts of primitive complexity remain |
| `R3_AllowUnharvested_Advanced` | advanced remain |
| `R3_AllowUnharvested_Glittertech` | glittertech remain |

Our own section 3 rule already produces this for free: a harvested
corpse has no qualifying hediffs left, so it stops being marked without
any special handling. **That is the behaviour to preserve and test** -
the marker vanishing when the bench finishes is the mod's most useful
single moment, and it costs nothing to implement.

**Do not read R3's filters directly.** They are another mod's defs and
would create the dependency section 2 rejected. Vanilla hediffs give the
same answer.

---

## 6. Structure

**This mod lives in its own repository as of 2026-08-29:**
`https://github.com/phildeluca-gmail/highlight-corpses-with-tech-rimworld`
It was split out of the Do Not Be Lazy repo, where it had been scaffolded
earlier the same day.

```
RimWorld-HighlightCorpsesWithTech/     <- repo root
  HighlightCorpsesWithTech/            <- copy THIS into RimWorld/Mods/
    About/About.xml                    <- no modDependencies; nothing to depend on
    Assemblies/                        <- build output, gitignored
    Source/HighlightCorpsesWithTech/
      HighlightCorpsesWithTech.csproj  <- exists; refs ../../../lib
      Core/HighlightCorpsesWithTechMod.cs   <- exists; gains settings hosting
      Core/Settings.cs                 <- tier toggles, marker toggle
      Detection/TechCorpseScanner.cs   <- the section 3 rule; pure, no side effects
      UI/Alert_CorpsesWithTech.cs      <- Alert subclass
      UI/CorpseMarkerComponent.cs      <- MapComponent, draws markers
  lib/                                 <- GITIGNORED; run setup-lib.bat
  HighlightCorpsesWithTech_Architecture.md
  README.md
  setup-lib.bat                        <- copies the 4 DLLs from your own install
  git-commit-push.bat                  <- stage, commit, pull --rebase, push
```

**No copyrighted material is committed.** `lib/` holds
`Assembly-CSharp.dll`, `UnityEngine.dll`, `UnityEngine.CoreModule.dll`
(yours, from your RimWorld install) and `0Harmony.dll` (from the Harmony
Workshop mod). All four are gitignored and populated by `setup-lib.bat`.
The mod's only external dependency is Harmony, which players get as a
normal Workshop subscription.

**No `Defs/` folder.** Nothing here needs XML - the alert self-registers
and the settings are code.

**No Harmony.** Leave `About.xml` without the dependency block.

### Cost control

Both surfaces need "which corpses qualify right now", and the naive
version walks every corpse's full hediff list every frame. Scan on an
interval into a cached list and have both the alert and the marker read
the cache. Corpse counts are small and hediff lists are short, so this
is cheap either way - but per-frame hediff walks in a 60-mod game with
RocketMan throttling ticks is exactly the kind of thing that shows up in
somebody's performance report later.

---

## 7. Edge cases

- **`Corpse.Bugged`** - a corpse whose inner pawn is broken. Guard
  before touching `InnerPawn`; vanilla has this property for a reason.
- **Corpse in a grave / container** - not spawned on the map, so no
  marker is drawable. Decide whether the alert still counts it. Leaning
  no: a buried corpse is a decision already made.
- **Colonist corpses.** A dead colonist with a bionic arm technically
  qualifies. Highlighting your own dead for parts is grim and may be
  unwanted. **Open** - see section 8.
- **Butchered / destroyed mid-scan** - the cached list must tolerate
  entries that vanished since the last scan.
- **Multiple implants on one corpse** - one marker, and the alert
  explanation lists all of them. Rank by highest tier for colour.
- **Corpse already hauled to the harvesting table** - still qualifies
  until the implant is actually removed, which is correct: the job is
  not done yet.

---

## 8. Still open - answer before implementing

**Closed 2026-08-29 by reading the installed R3 mod** (was questions 1
and 2): implants *are* recoverable from corpses, and **rot is
irrelevant** - no deadline, no decay of the prize, section 5.3 cut. Full
evidence in section 2. The rot preference given during design was
"escalate as it rots"; the note beside it began *"Igno"* and was never
finished, but it no longer matters which was meant, because the game
does not support escalation either way.

Remaining:

1. **Colonist corpses - include, exclude, or a third toggle?** Your own
   dead with a bionic arm qualify under the section 3 rule. Harvesting
   them is a real choice some players make and a grim surprise for
   others. Leaning: exclude by default, own toggle.
2. **Corpses in graves and containers - count in the alert or not?** Not
   drawable on the map either way. Leaning: no - a buried body is a
   decision already taken.
3. **Marker art.** A drawn glyph, a reused vanilla icon, or an authored
   texture? Nothing in this repo has ever shipped an asset, so a drawn
   glyph is the path of least resistance.
4. **Does vanilla strip hediffs at `Dessicated`?** Low risk, noted in
   section 2. One in-game glance at an old corpse's health tab settles
   it. Nothing in the design depends on the answer.

---

## 9. Build plan

Small enough to be one session. Per CLAUDE.md's model rules, this is
Sonnet work throughout - there is no ambiguous integration here, no
Harmony, and the hard thinking is in this document rather than the code.

1. Settings + tier toggles (section 4).
2. `TechCorpseScanner` + the cached scan (sections 3, 6).
3. Alert (5.1).
4. Marker (5.2).
**There is no step 5.** Rot escalation was cut (5.3); the build ends at
the marker.

Steps 1-3 are useful on their own: an alert with no marker is already a
working mod.
