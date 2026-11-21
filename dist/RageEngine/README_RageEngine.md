# Rage Engine 0.11: In-game guide

## Install (Quest)
1. Plug the Quest into the PC. In File Explorer open **Quest 2 → Internal shared storage → Android → data → com.Warpfrog.BladeAndSorcery → files → Mods**.
2. **Delete the old `NomadRageEngine` folder.** The mod was renamed. If both folders are there, the game loads the mod twice.
3. Paste the new `RageEngine` folder. It must contain `manifest.json`, `RageEngine.dll`, `thumbnail.png`, `Sounds` and `Waves`.
4. Start the game. RealisticBlood and Realistic Deaths can stay installed.

## Settings
Open the **book → Mod Options → Rage Engine**. Every option shows a tooltip explaining what it does. They're grouped as follows:

| Group | What it controls |
|---|---|
| General | Master switch, personalities, witness reactions, effect volume, debug log |
| Physics Reactions | Reaction strength, muscle loss on hit, balance loss, stagger threshold, recovery speed |
| Pain & Bleeding | Pain intensity, internal bleeding on/off, bleeding rate |
| Bones | Bone breaks on/off, bone toughness, break chance, open-fracture chance, jaw breaks, break vibration |
| Wounds | Wound threshold, clutch time, cut arms go dead, leg wounds drop them |
| Dismemberment & Adrenaline | Survive dismemberment, flee chance, adrenaline chance, adrenaline time, screaming |
| Choking | Choke-out time, choke-out kills, choke vibration, red eyes |
| Gore & Death | Head crushing, flesh tearing, chunk limit, eyes closing, last gasp |
| Fisticuffs | Fists up when disarmed, grab nearby weapons, throw things, barehanded blocking |
| Test Tools | Test pistol and dynamite, bullet force |

## How to see each feature

**Physical state (new in 0.7)**
Every NPC has one physical state (balance, posture, injuries, pain), and everything they do reads from it:
- **Losing balance:** their free arms swing out to catch themselves. **Next to a wall**, they put a hand on it to steady themselves and recover faster.
- **Unsteady or hurt:** they move slower, and limp with injured legs.
- **Knocked down while hurt:** they stay on the ground longer, writhing and holding wounds, before getting up. Everyone gets up at a different pace.
- **After getting up:** they're **shaky** for a few seconds and easy to knock down again.
- **Hard hits and falls:** can knock the weapon out of their hand.
- **Heavy blunt hits to the head:** leave them **dazed**.

**Physics hit reactions (Euphoria-style)**
- Reactions now come from **physical contact** (mass x speed), so **thrown objects**, shield bashes and light pokes all count, not just damaging hits.
- **Throw something heavy at them:** they get knocked back, stumble, or fall.
- **Poke or stab a leg:** the knee can buckle and drop them.
- **Poke the head:** the head snaps back.
- **Jab the gut:** they fold over.
- **Hit a shoulder:** it twists away.
- **Throw a heavy object at the chest:** they get knocked back in the direction it was flying.
- **Keep hitting:** balance drains with each hit, so they stumble harder and finally fall, then get back up.
- *Too floppy or too stiff?* Adjust **Muscle loss on hit** and **Reaction strength**. For more stumbles, lower **Stagger threshold**.

**Breaking bones (blunt only: fists, maces, hammers, shield bashes, thrown rocks or chairs)**
- **How to do it:** hit a forearm, upper arm or leg hard with something blunt. A fast, full swing works best.
- **When it breaks:**
  - You hear a loud **snap and crunch**.
  - Your controller gives a **double buzz**.
  - They scream.
- **Arm:** drops its weapon and **dangles on physics**. They can't grab with it again.
- **Leg:** buckles, they go down, and afterwards they drag or limp on it.
- **Jaw:** hit low on the face, under the nose. It hangs open and crooked.
- **Open fracture:** sometimes a **bone shard sticks out** of the limb and it bleeds.
- *Bones never break?* Lower **Bone toughness** (try 0.6) or raise **Break chance**.

**Cuts and stabs**
- **Slash or stab an arm hard:** that hand drops its weapon, the arm goes dead, and the other hand grabs the wound.
- **Stab the chest or neck:** a free hand presses on the wound. On bad wounds they drop their off-hand weapon to do it, and they keep going back to the wound while it hurts. Deep chest or neck stabs also make them cough blood.
- **Slash a leg:** the leg gives out, they fall, then get back up and limp.
- **Cut across the forehead:** blood gets in their eyes, so they wipe their face and can't see you as well for a few seconds.
- *Not enough reactions?* Lower **Wound threshold**.

**Dismemberment**
- **Cut off an arm or leg.** They usually survive for a while: they scream, grab the stump and bleed.
- **Nervous or cautious NPCs** usually **run away screaming**. Brave ones (aggressive or confident) **keep coming at you**, screaming.
- **Losing a leg** puts them on the ground. They keep trying to stand and keep falling.
- They can **suddenly collapse** at any time, and eventually they bleed out.
- Decapitation always kills.

**Adrenaline**
- **What happens:** sometimes a "killed" NPC keeps fighting for a few seconds, even after a stab to the head, screaming, then drops dead.
- **Settings:** controlled by **Adrenaline chance** and **Adrenaline time**.

**Choking**
- **How to do it:** grab an NPC by the **neck** and hold.
- **What you'll see and feel:**
  - Their eyes go red.
  - They choke, then gurgle with blood at the mouth.
  - Your controller buzzes with their struggle, and the buzz fades as they go limp.
- **The outcome:** after **Choke-out time** they pass out and wake up later, or die if **Choke-out kills** is on.

## Troubleshooting
- **No Rage Engine menu:** the `RageEngine` folder is in the wrong place, or the files are nested one folder too deep. `manifest.json` has to sit directly inside `Mods\RageEngine`.
- **No sounds:** check that the `Sounds` folder (21 `.wav` files) is inside the mod folder, then check **Effect volume**.
- **Debug log:** on by default in this test build.
- **Something seems off:** turn on **Debug log**, play for a minute, then send `Android\data\com.Warpfrog.BladeAndSorcery\files\Logs\Player.log`. Lines starting with `[RageEngine]` show what happened. If something reads `disabled: not available in this game version`, that one feature couldn't run on your game version.

## Testing 0.11
- **Grounded:** in **Duel (endless)**, hit them with ordinary weapon hits and punches. They should rock and take small staggers, but not fall. About 7 clean head punches in a row puts someone down.
- **No tripping:** watch them walk around corpses and gore for a minute. Cut a leg: they should limp, not collapse over and over.
- **Faces:** kill a few NPCs, let the wave keep spawning, and check that new spawns have normal faces.
- **Bones:** single punches shouldn't break anything. A big hammer swing or several heavy hits on the same arm should. Hold an arm and punch it to break it.
- **Disarmed:** knock the weapon out of their hand. They should put fists up, grab a nearby weapon, or throw plates and bowls at you from a distance.
- Send `Player.log` afterwards. Rage Engine's lines start with `[RageEngine]`.
