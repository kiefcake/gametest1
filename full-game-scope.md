# Full Game Scope Document
### 4-Player Co-op Dungeon Crawler — Full Release Scope

This document extends the MVP (one hub, one dungeon, four classes, one boss) into the shape of the finished game. Target: a full run takes up to 3 hours, with enough systems in place that runs stay replayable long after a player has "beaten" the game once.

**Status:** Dungeon count (8-10) and 3-ability-per-class kit are confirmed. Section 10 below locks in the seven previously-open design questions.

---

## 1. Design Pillars (unchanged, restated for scope decisions)

- **Interdependence over solo power** — every class exists because the other three need it.
- **A run has a beginning, middle, and end** — not an open-ended grind. 3 hours is a session, not a slice of one.
- **Replayability comes from variance, not length** — the answer to "played it already" should be modifiers, builds, and randomness, not just "add more content."

---

## 2. Run Structure — fitting 3 hours

A full run should feel like three acts. Rough budget for a 4-player group of reasonable skill:

| Phase | Time | Content |
|---|---|---|
| **Hub & prep** | 10–15 min | Gearing up, picking classes/loadouts, checking sector unlocks |
| **Outer sectors** (easy) | 30–40 min | 2–3 short dungeons, low difficulty, teaches mechanics, gathers early loot |
| **Mid sectors** | 45–60 min | 2–3 mid dungeons, first real status-effect combos required, first "hard" boss |
| **Inner sectors** (hard) | 45–60 min | 1–2 dense dungeons, heavy status synergy required, harder bosses |
| **Final dungeon + final boss** | 20–30 min | The capstone — multi-phase boss fight that tests the full kit |

That totals roughly 2.5–3 hours for a full clear from hub to final boss. Key scoping implication: **you don't need 20 dungeons** — you need maybe **8–10 dungeons total**, arranged in rings of increasing difficulty, with the outer ones short and the inner ones dense. Length should come from difficulty and required coordination, not repeated backtracking.

**Suggestion:** build a "run clock" concept into the design — sector difficulty scales partly with elapsed time, so groups that dawdle in the hub or early sectors feel gentle pressure to move on. This keeps pub groups from stalling a run at 5 hours.

---

## 3. World & Content Scope

### Sectors (rings around the hub)
- **Ring 1 (Outer, 3 sectors):** tutorialize mechanics — status effects introduced one at a time.
- **Ring 2 (Mid, 3 sectors):** combine 2 status effects per dungeon (e.g. a dungeon built around Poison + Weaken stacking).
- **Ring 3 (Inner, 2 sectors):** dense encounter design, requires full 4-role coordination, environmental hazards layered on top of status effects.
- **Final sector (1):** capstone dungeon + final boss, gated behind clearing enough of Ring 3.

### Dungeons per sector
- 1 dungeon per sector at launch (8–10 total), each with:
  - A unique enemy roster (2–4 enemy types unique to that dungeon, plus shared "trash" types)
  - A miniboss or set-piece encounter partway through
  - A boss fight with at least 2 phases
  - A themed status-effect gimmick relevant to that dungeon (e.g. a dungeon that constantly applies Curse, forcing the party to lean on the Healer's cleanse)

### Bosses
- 8–10 total (one per dungeon), each boss should have:
  - A mechanic that **specifically requires one role** to solve (e.g. add-heavy phase = Tank must peel, enrage timer = Buffer's Curse/Weaken is the check, high sustained damage phase = Healer output is the check)
  - This is your main lever for making "every class matters" concrete rather than aspirational

---

## 4. Class & Ability Scope (late-game)

You've already settled: 3 abilities per class (2 basics + 1 ultimate), architected as a flexible list.

For late-stage, keep the ultimate as the single unlock-later ability, but expand **build variety within the existing 3 slots** rather than adding a 4th ability outright:

- **Ability modifiers / "runes":** loot-based modifiers that change how an existing ability behaves (e.g. the Warlock's Venom Bolt can be modded to spread Poison on hit, or to pierce). This gives you build customization without new abilities to balance from scratch.
- **Stat-potion specialization:** since you're not doing XP levels, let potion investment bend a class toward a sub-role (e.g. a Paladin built tanky vs. one built for off-healing). This is a strong replayability lever that fits your existing progression system exactly.

This approach gets you build diversity (the "customization" ask) without the content cost of a 4th ability per class × 4 classes.

---

## 5. Progression & Customization

- **Loot & stat potions (already scoped):** keep as the core power progression.
- **Cosmetics:** class skins, weapon/armor visual variants, hub customization (banners, pets/mascots) — zero balance risk, high replay incentive, good for a solo dev to add post-launch incrementally.
- **Loadout presets:** let players save 2–3 rune/modifier loadouts per class so switching builds between runs is fast, not a chore.
- **Meta-progression between runs:** something persistent even after a run ends or a wipe happens — hub upgrades, unlocked cosmetics, or a "codex" of discovered enemy/status interactions. This softens the sting of a failed run and gives solo players something to chip away at.

---

## 6. Replayability Systems

This is the part that turns "an 8-10 dungeon game" into something people replay for months:

- **Seeded/randomized sector layouts:** even a modest amount of room randomization or enemy-placement variance per run keeps dungeons from becoming pure memorization.
- **Modifiers (RotMG-style "Nest keys" or roguelite-style twists):** optional run modifiers selected in the hub — e.g. "all enemies apply Curse on hit," "no revives," "double loot, double damage taken." Cheap to build (mostly flags on existing systems), huge replay value.
- **Weekly/rotating challenge run:** a fixed modifier set + leaderboard or completion tracking, refreshed on a schedule. Gives returning players a reason to hop back in without needing new content every week.
- **Secrets and optional content:** hidden rooms, optional superbosses, or alternate dungeon paths reward players who already know the base game well.
- **Difficulty tiers on repeat clears:** once a sector/dungeon is cleared once, unlock a harder variant with better loot — reuses existing content instead of requiring new builds.

Priority order if you're solo/small team: **modifiers and difficulty tiers first** (cheapest, reuse existing content), **seeded layouts second** (more engineering cost), **cosmetics and meta-progression ongoing** (steady post-launch drip content).

---

## 7. Difficulty & Group Scaling

- Confirm scaling behavior for <4 players (does a 2-player group face reduced enemy stats/counts, or is 4 players effectively required?). This affects solo/duo accessibility and needs a decision before final tuning.
- Boss "role-check" mechanics (Section 3) need to fail gracefully if a group is missing a role (e.g. no dedicated healer) — either soft-fail (harder but possible) or hard-fail (mechanically requires the role). Recommend soft-fail for accessibility, since pub groups won't always have a perfect comp.

---

## 8. Systems Checklist (non-combat, but required for "full game" scope)

- Matchmaking/lobby flow for pub groups (vs. your current player-hosted Relay setup — do you need any matchmaking, or is it invite-only?)
- Save/persistence for meta-progression, cosmetics, and loadouts
- Post-wipe/run-end flow: results screen, loot summary, return-to-hub
- Basic anti-frustration: reconnect-into-run support if a player disconnects mid-dungeon (important for 3-hour co-op sessions)

---

## 9. Suggested Next Steps (in order)

1. **Lock the final 7 open design questions** in your v0.2 technical design doc — late-stage scope depends on several of these being settled (especially anything touching sector count, boss structure, or progression).
2. **Finish MVP first** (1 hub, 1 dungeon, 4 classes, 1 boss) — treat everything in this document as post-MVP; don't let scope creep into the MVP build.
3. **Build the "role-check" boss template** early — design one boss fully (mechanics that check Tank/Heal/Buff/DPS individually) and use it as the pattern for all future bosses, rather than designing 8-10 bosses from scratch independently.
4. **Prototype one modifier system** (e.g. a single toggle like "double damage taken") before building content — this validates your architecture supports run variance cheaply.
5. **Scope dungeons 2 and 3** using the Ring 1 template (short, single status-effect focus) to validate that dungeon production time is sustainable before committing to 8-10 total.
6. **Defer cosmetics, meta-progression, and weekly modifiers** to a post-launch content cadence — they don't block a shippable full run and can be added incrementally.

---

## 10. Locked Design Decisions (previously open questions)

These resolve the seven open items from the technical design doc (v0.2, Section 3). Carry these back into that document as the source of truth for implementation.

### Weapons per class
- **Priest** → Wand (ranged, supports heal-bolt playstyle)
- **Wizard** → Staff (two-hand, channels Fireball/Icicle)
- **Knight** → Sword & Shield (charge-and-release fits a one-hand + block weapon)
- **Paladin** → Warhammer (heavy melee, matches "damage buffer" identity)

### Quest structure and rewards
**Hub bounty board**: 2-3 rotating short objectives per session (e.g. "clear Sector 2 without a wipe," "kill 10 enemies with Poison active"), rewarding currency or a stat potion. Reuses existing kill/clear tracking systems — no NPC dialogue or story infrastructure required. Doubles as a lightweight replayability lever alongside the modifiers in Section 6.

### Shop economy and currency
**Single soft currency**, dropped by enemies and bosses, spent at a hub vendor for stat potions and cosmetics. No second/premium currency at launch — avoids monetization and UI complexity that isn't needed pre-launch.

### Revive mechanic
**Proximity channel revive**: an ally must stand within ~2m and channel for ~3 seconds, interruptible by taking damage. The party shares a **limited pool of revive charges per run** (recommended: 3), rather than unlimited revives. Keeps "full wipe ends the run" meaningful while staying forgiving early in a run.

### Icicle (Wizard crowd control)
**Short freeze, 1.5-2 seconds**, does not stack duration on reapplication (refreshes instead of extending). A frozen target takes bonus damage from the next hit it receives ("Shatter" payoff). Prevents perma-lock on bosses while keeping the ability worth building around.

### Paladin damage boost
**Active cooldown ability**, not a passive aura. Lets the Paladin time the buff window with the group's burst windows (Wizard combo, boss vulnerable phase), consistent with the game's "coordinated timing" pillar.

### Stat system (potion-boostable stats)
**RotMG-style 8-stat system.** This supersedes the earlier 4-stat draft — note that **VIT's role changes**: it was previously "max HP," it's now "regen rate," with HP itself promoted to its own stat (same pattern as RotMG).

| Stat | Effect | Primary beneficiary |
|---|---|---|
| **HP** | Max health pool | All classes, especially Knight |
| **MP** | Max mana/resource pool | All classes, especially Wizard/Priest |
| **ATT** | Damage dealt per hit | Wizard, Knight, Paladin |
| **DEF** | Damage reduction | Knight, secondary value for all |
| **SPD** | Movement speed | All classes, situational for kiting/positioning |
| **DEX** | Attack rate / cast rate | Wizard, Knight (damage output over time) |
| **VIT** | HP/MP regen rate | All classes, especially sustain-focused play |
| **WIS** | Heal/buff potency + resource regen assist | Priest, Paladin |

**Potion economy:** one potion of a given stat fills **1/5 (20%) of that stat's max pool**, so **5 potions of the same stat fully max it**. This applies uniformly across all 8 stats — keeps the economy simple and consistent regardless of which stat a player is investing in.

- **All-stat potions** — a rarer tier that applies a smaller fractional boost to all 8 stats at once, reserved for late-game/boss-drop rewards where single-stat potions feel too incremental.

**Extensibility note:** structure the potion/stat system so adding a stat beyond these 8 (e.g. a later addition like **Cooldown Reduction**) is a matter of adding one more entry to a stat table/ScriptableObject, not a refactor. Build the stat system as a dictionary-style `Stat -> Value` structure per character rather than hardcoded fields, and keep the "1/5 per potion" rule as a constant applied uniformly, so new stats inherit the same economy automatically.

---

## Summary Scope Snapshot

| Category | Launch Scope |
|---|---|
| Dungeons | 8-10 (3 outer, 3 mid, 2 inner, 1 final) |
| Bosses | 8-10, each with a role-specific mechanic |
| Classes | 4 (Priest, Wizard, Knight, Paladin), 3 abilities each |
| Weapons | Wand, Staff, Sword & Shield, Warhammer (one per class) |
| Stats | 8 (HP, MP, ATT, DEF, SPD, DEX, VIT, WIS), 1/5 per potion, 5 pots max a stat |
| Economy | Single soft currency, hub vendor, hub bounty board |
| Revives | Proximity channel (~3s), 3 shared charges per run |
| Customization | Ability runes/modifiers + stat-potion specialization |
| Replayability | Modifiers, difficulty tiers, seeded layouts (roughly in that priority order) |
| Run length | ~2.5-3 hours full clear |
