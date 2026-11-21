# Rage Engine 0.25: Serum Lab

Make your own serums from the ones you already have: serums, super serums and toxins, in any combination. Everything is a made-up game effect.

## How to use it
1. Open **Mod Options > Rage Engine: Serum Lab**.
2. Set it up:
   - Pick a **Slot** (Custom Serum 1 to 6).
   - Pick up to four **Ingredients**.
   - Pick a **Name** ("Auto" names it after the reaction it triggers, or after its ingredients).
   - Pick a **Liquid colour** ("Auto" blends the ingredients' colours).
   - Set **Strength** and **Duration**.
3. Press **Save to slot**.
4. Spawn **Custom Serum N** from the item book (category **Custom Serums**) and inject it like any syringe.

The other buttons:
- **Duplicate slot** copies the selected slot into the next empty one.
- **Delete slot** empties it.
- **Load slot** puts a saved recipe back into the fields so you can edit it. Reopen the menu page to see the values.

Recipes are saved in `RageEngine_serums.txt`, next to the Rage Engine roster, and survive restarts. A custom syringe that's already in your hand picks up a new recipe's colour when you save.

## How a recipe resolves (the reaction registry)
Each ingredient goes through the system that already handles it:
- serums and super serums: the same effect pipeline as the normal syringes;
- toxins: the same poisoning as the toxin daggers.

Before that, the registry resolves the mix:
- **Duplicates count once.**
- **Sleep and stimulants cancel out.** Cryo-Sleep or Sedative together with Adrenal Surge, Adrenal Overload, Berserk or Ravager: both sides drop out.
- **Only one super serum takes hold.** That's the first one in the recipe. Each extra one overloads the body: convulsions and punch-drunk.
- **Only one toxin takes hold**, again the first. Each extra toxin makes it stronger.
- **Reactions:** registered combinations add a bonus effect on top. Each one runs for its own time, then ends cleanly.

| Reaction | Ingredients | Effect |
|---|---|---|
| Bloodrage | Adrenal Overload + Berserk | Last Stand at once: no pain, charging, 20 s |
| Night Terror | Hallucinatory + Terror | Drops everything, screams and runs, 25 s |
| Iron Statue | Colossus + Motor Shutdown | Frozen on his feet, takes 15% damage, 20 s |
| Green Rot | Verdant + Acid | Burns spread and keep eating, 30 s |
| Dead Sleep | Cryo-Sleep + Paralytic | Asleep at once, 30 s |
| Ashes to Fire | Phoenix + Hemotoxin | Bleeds hard, but at 10% HP heals completely, once |
| Hive Fever | Overmind + Neurostatic | NPCs around him go punch-drunk |
| Ghost Rush | Phantom + Adrenal Surge | Very fast punching, then a hard crash |

Adding a reaction is one new entry in the `Reactions` table in `SerumLab.cs`; nothing else changes.

## Compatibility audit
Written before the lab was built.

**What already existed and is reused, unchanged:**
- **Syringes:** `Devices.cs` handles item scan, dressing, needle-in detection, plunger and dose.
- **Serums and super serums:** `Biogenesis.cs` keeps a per-NPC effect list, applies reversible multipliers and handles expiry. The visuals are eye glow, aura, scaling (via the game's `Creature.SetHeight`) and the status marker.
- **Toxins:** `Toxins.cs` keeps one toxin state per NPC.
- **NPC state:** each NPC has its own state object from a pool, so effects never leak between NPCs.

**How the lab fits in:**
- Custom syringes are ordinary devices with a slot number. The original syringes behave exactly as before.
- Per-NPC tracking adds an active-reaction list and the custom serum name to each NPC's own state. Expired reactions are removed on the existing 0.1 s tick.
- Everything is cleared when the body is recycled, so nothing is shared between NPCs.

**Game APIs used** (all checked against the game's real DLL):
- `Creature.damageMultipliers`, `Creature.Heal`, `Creature.Damage(float)` and `Creature.Kill`;
- `Item.isPenetrating`;
- the `ModOption` attributes, buttons and arrow lists. These are the only in-game mod UI Nomad documents.

**Quest 2 performance:**
- No new per-frame loops.
- Reactions run on the existing 0.1 s NPC tick.
- No particles were added.
- Recipes are read once and written only when you press a button.

**Limitations:**
- **No free-text names.** The Mod Options menu can't take typed text on Quest, so you pick from 15 names or "Auto".
- **Six fixed slots.** New items can't be added to the item book at runtime (no documented catalog API), so the book has six "Custom Serum" entries whose contents you define.
- **The menu doesn't show results.** The recipe and its reactions are written to the game log (`lab: slot 1 = ...`) and to the research file. It can't display text itself.

**Needs testing in the headset:**
- that the lab menu shows its lists and buttons;
- that each reaction does what the table says;
- that saved recipes survive a restart.
