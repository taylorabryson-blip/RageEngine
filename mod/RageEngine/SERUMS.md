# Rage Engine: Serums, Super Serums, Toxin Oils

Everything here is a made-up game effect, not real chemistry or medicine.

## Install
1. Copy the whole `RageEngine` folder into `Android/data/com.Warpfrog.BladeAndSorcery/files/Mods/` on the Quest, replacing the old one.
2. Start Nomad and enable Rage Engine in the mod list. The log line `Rage Engine 0.25.0 enabled` confirms it loaded.
3. Item book:
   - **Serums**: the 6 serums and the empty syringe.
   - **Super Serums**: the 9 super serums.
   - **Serum Vials**: 15 vials (potion bottles).
   - **Custom Serums**: your 6 Serum Lab slots.
   - **Rage Engine Toxins**: the 7 toxin daggers and the 7 toxin oils.
4. Mod Options: **Rage Engine: Serums**, **Rage Engine: Super Serums** and **Rage Engine: Serum Lab**.

## Using a syringe
1. Hold it in either hand.
2. Push the needle into an NPC. The game has to register a real pierce. Touching the NPC isn't enough.
3. The plunger then travels down over about 0.8 s and the liquid drains. If you pull out early, the NPC gets only the part of the dose that went in.
4. The NPC flinches at the spot, gasps and looks at it.
5. The syringe is now **empty** and can't fire again.
6. To refill it, touch the needle of an empty syringe to a full vial. It draws the compound, and the vial empties.

## Devices
| Device | Used for |
|---|---|
| Standard disposable (clear barrel, white plunger, orange hub and cap) | all 6 ailments, and the empty syringe |
| Glass laboratory syringe (glass barrel, metal flange) | Night Soldier, Titan, Ravager |
| Specialized injector (armoured body with a window) | Hero X, Phantom, Overmind |
| Compact auto-injector | Adrenal Overload, Phoenix, Colossus |
| Glass vial (ampoule with label) | all 15 compounds |

## Ailments
Each one builds up over a few seconds, then wears off when NPC recovery is on. Compatible effects stack. Stimulants and Cryo-Sleep cancel each other.

| Compound | Liquid | Effect |
|---|---|---|
| NEUROSTATIC | cyan | Punch-drunk coordination, slow reactions, poor balance, small twitches |
| VERDANT SYNDROME | green | Fatigue, slower movement, weak arms; a green tint spreads from the site |
| CRYO-SLEEP | ice blue | Sluggish and frosted, then unconscious; wakes up afterwards |
| ADRENAL SURGE | orange | Aggressive, faster and erratic, ignores pain; crashes into exhaustion near the end |
| HALLUCINATORY SYNDROME | magenta | Flinches at nothing, backs away from nothing, loses the thread of the fight, screams, looks confused |
| MOTOR SHUTDOWN | silver | Arms weaken, legs buckle, sinks to the ground; recovers |

## Super serums
Only one serum is active at a time, and it lasts 10 minutes. Visual identity is glowing eyes plus a light aura. Titan, Colossus and Mutation can also grow slightly, but only if **Size increase** is on.

| Serum | Eyes / aura | What it does |
|---|---|---|
| Night Soldier | red / dark red | No pain, stronger, more durable, small regeneration, half the flinch, steadier, never quits |
| Hero X | white / gold | Strong regeneration, strength, durability, faster reactions, little pain or flinch, clears punch-drunk, restores limb function. Outcome is rolled (below) |
| Adrenal Overload | orange / red-orange | Aggressive, faster and stronger, ignores flinch and pain, never quits. **Last Stand** below the threshold: 15 s with no pain and charging. He can still be killed |
| Titan | yellow / orange | Much stronger and harder to hurt, very steady, a bit slower |
| Phantom | blue / light blue | Fast, very quick reactions, better blocking |
| Phoenix | green / green | Regenerates; once, at 10% HP, gets a second wind that restores 60% HP |
| Ravager | crimson / crimson | Relentless rapid punches, ignores pain, bleeds slower, never quits |
| Overmind | purple / purple | Sharper reactions; allies within 8 m block and react better |
| Colossus | white-blue / blue | Barely flinches, takes 60% less damage, very strong and steady, slower |

### Hero X outcomes (Super Serum Syndrome)
| Outcome | Chance (defaults) | Effect |
|---|---|---|
| Rejection | 15% (Rejection probability) | Convulsions, coughing blood, collapse. Then death if "Rejection can kill" is on; otherwise he recovers un-enhanced |
| Success | 60% of the rest | Full effect |
| Partial | 20% | Half strength |
| Unstable | 14% | Full effect, with occasional seizures |
| Mutation | 6% | 1.4x strength, aggressive, slightly bigger (if Size increase is on) |

With "Experimental outcomes" off, every non-rejection is a Success.

### Limb restoration
Hero X restores function to one injured limb every few seconds:
- a broken bone knits;
- a dead or cut arm comes back;
- a buckled leg straightens.

**A limb that has been cut off can't be re-attached.** Nomad has no API to undo a slice, so the stump is sealed (bleeding stops) instead. This is an approximation, not regeneration.

## Research database
Every injection is appended to `RageEngine_research.txt`, next to the Rage Engine roster file. Each entry records:
- the NPC;
- the compound and the dose;
- the injection site;
- the Hero X outcome.

Named NPCs also remember their serum in the roster.

## Settings
**Rage Engine: Serums**
- Enable
- One toggle per ailment
- Effect duration (0.25-3x)
- Effect intensity (0.25-2x)
- Visual effects
- NPC recovery
- Status indicators

**Rage Engine: Super Serums**
- One toggle per serum
- Enhancement strength
- Regeneration rate
- Rejection probability
- Rejection can kill
- Experimental outcomes
- Last Stand and Last Stand threshold
- Serum visuals
- Size increase (**off by default**)
- Enhanced NPCs in waves

## Compatibility
- **Realistic Blood / Realistic Deaths:** The serums don't touch their systems. It only changes Rage Engine's own per-NPC values.
- **How effects are applied:**
  - Every effect is a reversible multiplier on Rage Engine's existing systems: toughness, pain, balance, punch-drunk, knockout, muscles and defence tuning.
  - It also uses the game's own `Creature.damageMultipliers`, `Creature.Heal` and `Creature.SetHeight`.
  - All of these are removed when the effect ends or the body is recycled.
  - The AI is never permanently changed.
- **Under the hood, devices are game items:**
  - Each syringe is the common dagger item with its blade hidden along the needle, which gives it real pierce physics.
  - Vials and toxin oils are the health potion bottle with its healing removed.
  - Rage Engine dresses both with its own models.
- **Size increase** uses the game's own height system, so in principle the rig, colliders and grips scale together. This hasn't been tested in the headset, which is why it's off by default. If you turn it on and joints, grips or walking look wrong, turn it back off.

## Toxin oils
Seven bottles in **Rage Engine Toxins**, one per toxin. Touch the mouth of the bottle to the blade of a weapon you're holding and the blade is coated (6 hits), the same as a toxin dagger. Each bottle has 3 coats; the liquid level drops with each one.
