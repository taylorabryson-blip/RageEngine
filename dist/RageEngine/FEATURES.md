# Rage Engine 0.11: Every feature, in detail

Euphoria-style physical NPCs for Blade & Sorcery: Nomad. Applies to every human NPC (book spawns, dungeons, waves); no special wave is needed. All settings are under **Mod Options → Rage Engine**, and every option has a tooltip. It works alongside RealisticBlood and alongside death-animation mods. If you'd rather use another mod's deaths, switch off **Dying moments**.

## 0.11: Stability overhaul (fix the foundation first)
- **Grounded for every NPC.** The game's stock brains turn head and leg hits at push level 2 and up into a fall (`Destabilize`), which is why NPCs dropped after a couple of hits. Rage Engine now retunes the hit-reaction table of whatever brain each NPC has, at runtime. No brain files are replaced:
  - Push level 0-1: no canned stagger.
  - Level 2: a small stagger at most.
  - Level 3: a stagger, never a fall.
  - Level 4: a fall only if they were already off balance.
  - Option: **Grounded hit reactions**. Turning it off restores the stock values.
- **No more random tripping.** Rage Engine no longer loosens leg muscles on a standing NPC. A wounded or broken leg is weak and limping, not at zero strength, which used to make them fold up again and again. The always-on full-body physics added in 0.10 is now off by default: it made legs physical and they caught on everything. Bullets and explosions still turn it on briefly for the NPC they hit. Gore chunks no longer collide with people. Trunk shoves are capped. Leg cuts make them limp instead of falling, unless **Leg wounds drop them** is on.
- **No more deformed faces.** The game re-uses NPC bodies. Before 0.11, a body that had died with closed eyes, a broken jaw or a crushed skull came back that way on the next spawn. Rage Engine now puts back every change when an NPC despawns: eyes, jaw, head scale, eye colour, joint limits and limb strength.
- **Bones take real punishment.** A limb breaks from ONE very heavy blunt hit, or from several heavy hits on the same limb (bone stress builds up and slowly fades). Ordinary punches and light hits never break a bone. Bone pokes out only on severe breaks: 30% by default, always on Haymaker and very heavy hits. Haymaker (hold the limb and hit it) still breaks it from one solid hit.
- **Textured gore:**
  - Bone: grain, pores, a bloody root and marrow at the snapped end.
  - Flesh chunks: the NPC's own skin on the outside, raw muscle, fat and blood inside.
  - Wound craters: a dark wet hole, muscle, a fat layer and a torn skin edge in the NPC's skin colour.
  - Bruises: blotchy, with soft transparent edges.
  - Every surface has a normal map. Textures are generated in code, so there are no asset bundles to fail on Quest.
- **Clutching that keeps going:** they keep returning to a wound while it hurts, and the more it hurts the sooner they go back. Clutch time is now 3.5 s by default.
- **Decapitation is always instant.** The game reports the kill before the head comes off, so 0.10 sometimes revived a headless NPC. Any head or neck killing blow is now final.
- **Fisticuffs (engine-level, every NPC):**
  - A disarmed NPC puts their fists up using the game's own unarmed stance.
  - If a weapon is lying next to them, they snatch it up.
  - When you're out of reach, they pick up small loose objects (plates, bowls, cups, bottles, rocks) and throw them at you, using the game's own NPC throw with a physics fallback.
  - When your hand or weapon comes in fast at their head, they throw their forearms up.
- **Compatibility:**
  - Realistic Deaths is detected automatically, and Rage Engine stops reviving or force-killing NPCs. There's also a manual **Leave deaths to other mods** option.
  - RealisticBlood is unaffected.
  - Rage Engine no longer replaces any game brain or creature file.
- **Waves:** only **Civilians (endless)** (up to 3 at a time) and **Duel (endless)** (one at a time). Both use regular people in civilian clothes with random civilian weapons. Rage Engine works on every other NPC in the game anyway.
- **Rebrand:** the mod is now **Rage Engine** (folder `RageEngine`).

## 0.10 features (tuned in 0.11)
- **Full-body physics (the Euphoria fix):** the base game only switches an NPC's physics on right where your hand touches them; the rest of the body just plays animation. That's why throws, shoves and bullets barely moved them. Rage Engine now keeps NPCs within 8 m (adjustable) fully physical, so every reaction below is real physics. Options: Full-body physics, Full-body physics range.
- **Punch-drunk knockdowns, not based on HP:** in 0.9 the first punch drained balance and the second punch then dropped them, every time. Now clean blunt shots fill a "rocked" meter:
  - A head shot counts fully; neck about 70%, body about 30%, limbs about 10%.
  - When the meter fills, they drop. Around 5 clean head shots does it (adjustable with **Punches to knock down**).
  - From about 60% full, their knees visibly wobble.
  - The meter drains over a few seconds if you stop hitting.
  - Full health or nearly dead makes no difference. Glass jaws take about half as many.
  - One single huge force still drops anyone. Punches no longer trigger the game's canned stagger animation.
- **You can see the bones now:** bones, bruises and wounds use a plain lit material that renders on Quest, placed on the skin surface instead of inside it. Open fracture chance now defaults to 100%. A torn, bloody wound surrounds the white bone sticking out.
- **Hold-and-break (Haymaker style):** grab an NPC's arm or leg with one hand and punch it with the other. It snaps easily: bone out, the limb goes dead, and the weapon swaps hands.
- **Tougher limbs:** arms and legs no longer rip off from ordinary grabbing and punching.
- **Spine breaks:** a huge force to the back or chest breaks the spine, for example a very hard throw into a wall (about 12 m/s) or a golem boulder. Their legs go dead for good and they never stand again, but they keep fighting from the ground. Options: Spine breaks, Spine break force.
- **Head crushing:** a massive impact to the head caves the skull in, for example a heavy rock, a golem boulder or a high-speed slam into a wall. It's instant death with a crunch, a deformed head, blood and flying chunks. Punches and normal swings never reach this.
- **Flesh tearing:** heavy blunt impacts rip chunks out of them. Meat chunks fly off with real physics, a raw, bloody crater stays on the body and it keeps bleeding. Very heavy hits show bone in the crater, and heavy blunt hits to a limb always break it. Explosions tear flesh too. Options: Flesh tearing, Flesh tearing force, Max flying chunks.
- **Wall and floor slams count:** a body part hitting the world hard is an impact, whether they're thrown into a wall, slammed into the floor or dropped from height. Slams bruise, daze (head), break bones and tear flesh. This works on corpses too.
- **Blood in the eyes, done right:** they no longer ignore it when both hands hold weapons.
  - They either wipe the blood away or cover their eyes with one hand while the other keeps swinging blindly.
  - It repeats every few seconds while the cut keeps bleeding.
  - A stab right to the eye makes them scream and half-blinds them for longer.
- **Deaths you can read:** when an NPC dies, they take a last gasping breath and their eyes close, so you know they're gone. Options: Eyes close on death, Last gasp.
- **Bar Brawl wave and fist fighters:** a new wave, **Bar Brawl (fists)**, sends bare-knuckle brawlers that drop any weapon and fight with their fists. The **Everyone fights with fists** option makes every NPC brawl.
- **The test pistol actually works:**
  - Bullets do guaranteed damage (2.5x to the head) through the game's damage system, with a direct fallback if the game ignores the hit.
  - Aim assist counts anything within 25 cm of the shot as a hit.
  - The hit body part is turned fully physical and snaps back hard: the hit limb jerks, the body rocks, and corpses get knocked.
  - Every shot has a muzzle flash and a tracer.
  - Option: Bullet force.
- **New sounds:** flesh tearing, skull crush, spine snap and death gasp (all made from CC0/free sources or synthesized).

## 1. Physical state system
Each NPC carries one physical state, and every behaviour below reads from it:
- **Tracks:** balance, posture, pain, fatigue, toughness, personality, adrenaline, and limb condition (broken, cut, missing).
- **Posture stages:**
  - **Steady:** on their feet and composed.
  - **Unsteady:** balance below 70%.
  - **Stumbling:** balance below 30%.
  - **Down:** on the ground.
  - **Getting up**
  - **Shaky:** for a few seconds after getting up, their balance comes back gradually.
- **Movement follows the state:** they slow down when unsteady, in pain or injured, and limp on damaged legs. After getting up they don't snap straight back to full speed.

## 2. Physics hit reactions (Euphoria-style partial ragdoll)
- **What counts:** reactions come from real physical contact, measured as momentum (mass × speed), not just from damage. Thrown objects, pokes, punches, kicks, shield bashes and bodies all count.
- **Muscles let go where they're hit:** the hit body region goes loose for a moment, so physics moves it and the animation fades back in as the muscles recover.
  - **Head:** snaps back.
  - **Neck:** whips.
  - **Chest:** rocks back.
  - **Stomach:** folds over, winded.
  - **Shoulder or arm:** twists away.
  - **Leg:** jerks and gives.
- **Body shove:** heavy hits push the whole body. If they don't go down, the shove is horizontal only, so their legs keep them planted.
- **Balance:** every hit drains it, and hits on someone already unsteady rock them more. No single hit takes more than about half their balance.
- **Escalation, using the game's own systems:** flinch → stagger step → full stagger → dazed (heavy blunt hits to the head) → knee buckle (leg hits while unsteady) → fall.
- **Falls tumble in the hit direction.** They get back up by themselves.

## 3. Grounded: no flinging, no cheap knockdowns
- **Feet stay planted.** Hits move the body part they land on and rock the upper body, but the legs keep them up. Ordinary hits never fling or floor anyone.
- **Small and medium hits** (pokes, punches, light throws) get **no canned stagger animation**: the reaction is pure physics.
- **Heavier hits** make them take a step to catch themselves.
- **Knockdowns need a genuinely heavy force:** a big thrown object, an explosion, a body slam. If they do go down, they **get up fast**; only the badly injured stay down a moment.
- **Player punches are human strength** (adjustable with **Player punch strength**): a punch rocks them, it doesn't launch them.
- **Options:** Knockdown force, Player punch strength.

## 3b. Toughness
- **Composed NPCs:** most NPCs can take a punch. A composed NPC reacts (head snaps, steps back, staggers) but **does not fall from one ordinary hit**. Only a massive hit, such as a heavy thrown object, puts a composed NPC down.
- **Once they're reeling:** low balance or still shaky from getting up, follow-up hits put them down.
- **Toughness differs per NPC:** aggressive and confident NPCs are tougher.
- **Glass jaw:** about 1 in 8 NPCs (more often nervous ones) can't take a hit, and one good punch sends them flying or crumpling.
- **Options:** NPC toughness, Glass jaw chance, Stagger threshold.

## 4. Balance reflexes
- **Arms out:** when they're losing balance, their free arms swing out to catch themselves.
- **Brace on walls:** next to a wall they put a hand on it to steady themselves, and recover balance 2.5× faster while braced.

## 5. Falling and getting up
- **Hurt NPCs stay down longer:** they writhe and keep holding their wounds before getting up. Leg injuries keep them down longer still.
- **Everyone recovers differently:** aggressive and confident NPCs get up faster.
- **Shaky afterwards:** for a few seconds after getting up their balance cap is reduced, so they're easy to knock down again.
- **Losing grip:** hard hits and falls can knock the weapon out of their hand.

## 6. Pain
- **Builds with every hit.**
- **Slows them:** pain slows balance recovery and movement and makes them weaker.
- **Changes how they act:** it makes them hold their wounds and scream.

## 6b. Visible damage and shock
- **They show every hit:** the more they're hurt, the more they hunch, wince and flinch with pain, and the slower they move. You can see a tough NPC wearing down.
- **NPC health multiplier (1–10×):** for long fights.
- **Shock:** an NPC that stays in critical pain or keeps bleeding long enough goes into shock, collapses and dies, whatever HP they have left. That way tanky NPCs don't stand around forever. Options: Shock deaths, Shock time.

## 7. Bones (blunt force only)
- **Ribs:** a hard blunt hit to the chest or stomach winds them: they double over and grab it. Hard enough, it **cracks ribs** (crack sound, controller buzz, scream). With cracked ribs every breath hurts, they wince now and then, and further body blows hurt more. Two or more cracked ribs make them cough.
- **What breaks bones:** fists, maces, hammers, shield bashes and thrown objects can break **arms, legs and jaws**. Blades never break bones.
- **How you know a bone broke:**
  - A loud snap and crunch.
  - A sharp double buzz on the controller that did it.
  - They scream.
- **Broken arm:** drops its weapon, can never grab again, and **dangles on physics** (no muscle at all). The joint bends in ways a healthy one can't.
- **Broken leg:** buckles and they go down. Afterwards they limp and drag it, with permanently reduced balance and speed.
- **Broken jaw:** hangs open and crooked, and the mouth no longer moves when they talk. A smashed jaw hangs much lower, swings loose with every head movement, and bleeds. (It can't come fully off: the jaw is part of the head model.)
- **Open fracture:** a splintered bone shard sticks out of the skin and moves with the limb, and the wound bleeds.
- **Hitting a broken limb again** doubles them over.
- **Options:** Bone toughness, Break chance, Open fracture chance, Jaw breaks, Break vibration.

## 8. Bruising
- **Where:** blunt hits (punches, kicks, maces, shield bashes, thrown objects) leave a bruise where they land, including on the face.
- **Colour over time:** **red** at first, **purple** after a few seconds, then **deep purple-blue**.
- **Same spot again:** the bruise gets bigger and darker.
- **Movement:** bruises follow the body part.
- **Options:** Bruising, Bruise sensitivity.

## 9. Cuts and stabs
- **Cut or stab to an arm or hand:** a pained arm with a weak, trembling grip. You can see they don't want to hold the weapon, but they keep fighting with it.
- **Serious cut or stab to an arm:** that arm goes dead and hangs, and the weapon switches to the good hand so they keep fighting. Dead arms can't reach or grab anything.
- **Holding wounds:** they hold wounds with their free hand only, briefly, and keep fighting while they do.
- **Cut or stab to the chest or neck:** a free hand presses on the bleeding wound. On bad wounds they drop their off-hand weapon to do it, and they keep going back to the wound while it hurts.
- **Cut or stab to a leg:** the leg gives out and they fall, then get up and limp.
- **Light pokes and nicks:** still get a reaction, either a brief clutch or a leg jerk.
- **Cut across the forehead:** blood gets in their eyes. They wipe their face, and their vision is dulled for a few seconds.
- **Options:** Wound threshold, Clutch time, Cut arms go dead, Leg wounds drop them.

## 10. Bleeding
- **Internal bleeding:** deep stabs to the chest or neck, open fractures and stumps keep bleeding, so health drains over time.
- **Coughing blood:** with blood in the airway they double over, coughing blood with a wet gurgle.
- **Options:** Internal bleeding, Bleeding rate.

## 11. Dismemberment
- **Losing an arm or leg doesn't kill them outright:** they scream, grab the stump with their other hand, and slowly bleed out.
- **Flee or fight:** these are fighters. Most keep pushing at you, screaming; only some run (Flee chance, 30% by default). Witnessing a death no longer makes anyone cower.
- **Losing a leg** puts them on the ground. They keep trying to stand and keep falling.
- **Adrenaline crash:** they can suddenly collapse at any moment.
- **Decapitation** always kills.
- **Options:** Survive dismemberment, Flee chance, Screaming.

## 12. Dying moments
- **What happens:** a fatal blow doesn't switch them off like a light. They keep their momentum: an attack already in motion is finished, they reel from the blow and stumble as their strength drains away, then they collapse into a ragdoll, carried in the direction of the killing blow.
- **How long:** head and neck kills give the shortest window; gut and chest the longest. A second blow during the window drops them at once.
- **Options:** Dying moments, Dying time.

## 13. Adrenaline
- **What happens:** sometimes a "killed" NPC keeps fighting on adrenaline for a few seconds, even after stabs to the head, screaming, and then collapses for good.
- **Options:** Adrenaline chance, Adrenaline time.

## 14. Choking
- **How to do it:** grab an NPC by the neck and hold.
- **What happens:**
  - They sag as the air runs out.
  - The whites of their eyes turn red.
  - They choke, then gurgle with blood at the mouth.
  - Your controller buzzes with their struggle, and the buzz fades as they go limp.
- **The outcome:** after **Choke-out time** they pass out and come round about 12 seconds later, or die if **Choke-out kills** is on.

## 15. Personalities and awareness
- **Six personalities:**
  - **Calm:** balanced.
  - **Confident:** recovers fast, tough.
  - **Nervous:** rocks easily, panics, glass jaw more often.
  - **Aggressive:** recovers fastest, toughest, bravest, strongest adrenaline.
  - **Cautious:** wary.
  - **Unpredictable:** random reactions.
- **Witness reactions:** NPCs near an ally who dies lose balance from the shock, and nervous ones may panic and run.

## 15b. Test tools (Mod Options → Test Tools)
- **Spawn test pistol:** a gunmetal-tinted dagger, where the blade is the barrel.
  - Pull the trigger to fire a hitscan bullet. It does real damage through the game, so wounds, bones, pain and blood all react, and it gives a physical bullet impulse.
  - You get gunshot and flesh-impact sounds, and recoil.
- **Spawn test dynamite:** a red-tinted bottle.
  - Pull the trigger to light it (you'll hear the fuse sizzle), then throw it. It explodes 3.5 seconds later.
  - Everyone nearby is knocked down and thrown by real explosion force and takes damage by distance, and loose objects fly.
- **Options:** Pistol damage, Dynamite power.

## 16. Sounds and voice
- **Mod sounds** load straight from the mod's `Sounds` folder: 41 public-domain sounds (Kenney.nl, plus Freesound CC0 recordings by Morgan Purkis and others):
  - punches, body hits and body falls
  - bone snaps for arms, legs and jaws (real cracks layered with crunches), bone crunches and rib cracks
  - blade slashes and stabs into flesh, and dismemberment
  - gunshots, bullet impacts, explosions and a fuse sizzle
- **Choking and death voices** are the game's own voice acting (the old bubbling gurgle is gone).
- **Options:** Effect volume.

## 17. Debug
- **Debug log:** writes every event to the game log (`Logs/Player.log`): hits, contacts, falls, posture changes, breaks, bruises, wounds, deaths. It's on by default in this test build.
- **Self-protection:** if a feature can't run on your game version, it switches itself off and logs `disabled`, and the rest keeps working.
