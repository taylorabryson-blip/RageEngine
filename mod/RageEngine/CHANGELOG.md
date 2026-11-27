# Rage Engine changelog

## 0.53 - back to what worked
- **NPC footing is back to how it played in 0.22.** Only cheap knockdowns get blocked, at the moment they happen. Real hits, grabs, throws, telekinesis and spells all go through. NPCs no longer get stuck standing in mid-air.
- **Punch-through chest removed.**
- **Settings cleaned up** to be close to the live version. The extra switches added in 0.50 and 0.51 are gone from the menu (those features just run). Injury effects are back to Off / Mild / Medium / Heavy, with Mild as the default.
- **Organs fall out of the body again**, and a bit more often.
- **Glass:**
  - Shatters into jagged shards from where it was hit, plus a burst of small chips.
  - Brushing or leaning on it no longer breaks it; it takes a real hit.
- **Super soldiers:** glowing eyes that show up on Quest, plus a pulsing colored light aura.
- **Fire:** burning NPCs keep moving and stagger around beating at the flames. They get another try every few seconds. Strong, healthy ones usually get it out; weak, drunk or badly hurt ones often don't. Char marks build up as they burn.
- **Lightning scars:** now also triggered by the game's electrocution effect and by big lightning hits.

## 0.52
- **Health bars** (Rage tab):
  - Show: Off / Only when hurt / Enemies / Everyone.
  - Styles: Classic (framed, with a damage chip), Slim, Segmented, Souls (wide, with a slow yellow damage trail).
  - Colors: By health (green to red), Red, Orange, Blue, White.
  - Sizes: Small / Normal / Large.
  - Bars face you, stay readable at a distance and fade out when the NPC dies.
- **New 3D models made in Blender:** real teeth (incisor, canine, premolar, two-rooted molar), skull fragments with clean fracture edges, brain chunks with folds, a full vertebra (body, arch and processes), a broken jaw piece, and a proper NPC Spawner (knurled grip, metal cage, glowing core).

## 0.51
**New**
- **NPC Spawner** (item book → Utilities): point it and pull the trigger to spawn an NPC where you point. The other button switches between Enemy, Unarmed brawler, Super soldier and Ally.
- **NPC life (RDR2-style):**
  - Bump into a calm NPC and he stumbles, turns and shoves you off. Keep doing it and he gets angry.
  - Aim a weapon at a calm or beaten NPC and he puts his hands up and backs away. Cocky ones square up instead.
  - NPCs who spot a fresh body stop and stare. A friend of the dead gets furious.
  - Morale: watching you do something brutal, or watching others die, breaks the weak. They run or give up.
  - In a lull, a hurt NPC looks down at his wound and holds it.
  - Look a calm NPC in the eye up close and he looks back and nods.
- **Hanging jaws:** damage to the jaw adds up. Enough breaks it; more tears it loose so it hangs open and swings.
- **Knife lines:** NPCs say something when they pull a knife.

**On/off switches**
Most systems can now be turned off on their own:
- Rage tab: Map rescue.
- Gore tab: Hanging jaws.
- NPCs tab: NPC life, Keep NPCs standing, Smart NPCs, Knife pulls.
- You tab: Chokes, Throat punches, Gravity slams.
These sit alongside the existing switches, so Rage Engine can run as a light quality-of-life add-on.

## 0.50
**Spells and the elements**
- **Lightning scars:** a real strike leaves the branching lightning scar where it went in, a track of them down the body, and a burn where it went out (a foot).
- **Fire:** burning NPCs try to put themselves out by patting at the flames or dropping and rolling. Most manage it; slow, weak, drunk or badly hurt ones don't. Burns leave char marks, and bodies burned long enough collapse into a pile of ash.
- **Gravity:** throw a man hard into a wall or the floor and the landing breaks bones where he hit and leaves blood on the surface. Hard enough and he bursts. Floating NPCs flail and scream.

**Damage**
- **Brain damage:** head hits add up. A damaged brain fights slower and dumber: pauses, forgets to attack, stumbles, looks confused.
- **Concussions:** one huge head blow means a minute or two of dazed, drunk fighting.
- **Throat punches:** the head snaps back, both hands go to the throat, he gags and can't fight for a few seconds.
- **Teeth:** knocking teeth out now makes a real crack sound and more blood from the mouth.

**New**
- **Realism style:** every setting on, heavy injury effects, intense desperation, and every solid hit really hurts.
- **Two-hand chokes:** both hands on the throat choke him out much faster, and you feel it in both hands.
- **Rage Yard:** a new sandbox map. A walled lot with shipping containers to climb, a two-level scaffold, a glass alley to throw people through, cover, throwables and floodlights.
- **Wave music:** battle music plays during Rage Engine waves. Change the volume in the Rage tab.
- **Super soldier waves:** Super Soldier Duel, Squad and Army. Everyone spawns already injected with a super serum.

## 0.49
**NPCs stay on their feet**
- NPCs stand the whole fight. They only go down when they die or are very low on HP (under 20%).
- The lower their HP, the less they tank hits. Some tough or strong-willed NPCs hang on even with their guts out.
- Knockouts only happen at very low HP. Sleep serum, poison, choke-outs, pressure points, a lost leg or a broken spine still put them down.
- Grabbing, telekinesis and throws still work as before.

**New**
- **Rip organs out by hand.** Cut him open, put your hand in the wound and grip. Chest gives the heart or a lung, belly gives the liver, stomach, spleen or guts, and the back gives the spine. Throw it or drop it.
- **Punch through the chest.** A really fast punch goes clean through, leaving a hole front and back. He hangs on your arm grabbing at it. Pull your arm out and he drops.
- **Heads turn to goo.** Keep beating a caved-in skull, or land a huge blow, and the head pops.
- **More organs:** stomach, spleen, bladder and intestine. Organs now come out of the spot they actually sit in, and slide out instead of flying.
- **Taunts:** cocky NPCs square up, beckon you in, spread their arms and taunt you.
- **Fear and surrender voices:** begging, fleeing and surrendering NPCs use the surrender, beg, panic and fear lines from any voice pack they have.
- **Other gore mods:** set Gore → "Gore & blood from: My other gore mod" and Rage Engine turns off its own blood and gore. Everything else keeps running.
- NPCs actually pull knives now when they're unarmed and you're on them.

**Fixes**
- Syringes, serums, vials and oils are back in the item book (Potions tab; toxin daggers are in Daggers).
- Rage Tower: smoother stairs between floors. NPCs stuck on another floor with no way to reach you get moved to your floor.
- Settings are split into 4 tabs: Rage, Gore, NPCs, You.

## 0.48 — Polish
- Healthy NPCs no longer drop from ordinary hit reactions; only hurt NPCs (low HP) can be knocked down by normal hits. Knockouts, chokes, takedowns and other "meant" falls still work.
- Knocked-out / passed-out NPCs are no longer fought by the stand-up guard.
- NPCs and Magnet no longer pull weapons that are holstered or held with telekinesis.
- Small stability fixes.

## 0.47.0 - fewer things, done better
- We're trimming some content to focus on quality. Maps: Rage Tower, Skybar and Red Room Diner.
- Fixed: NPCs spawned from the book could collapse like they'd lost their bones (and sometimes bleed) a few spawns in.
- NPCs stay on their feet like they did in 0.31-0.38: healthy NPCs don't go down; hurt ones can, and the lower the HP the likelier. Strong-willed NPCs can still power through.
- Dynamic organs: deep cuts low on the belly spill the guts (they hang from the wound), deep chest cuts drop lung or heart, huge blasts throw organs out. New Mode setting: Rage Engine + dynamic organs / Rage Engine (no organs) / Dynamic organs only.
- NPC weapons (waves, dungeons, anywhere): Game default / Mixed / Anything - any melee weapon in the game, mods included.
- Smarter NPCs: skilled fighters step back out of big swings, disarmed NPCs pick weapons back up off the floor, and NPCs tell nearby friends where you are.
- The wave book is found and moved into the map even when the level hides it.

## 0.43.0 - new serums, super serums with powers, proficiency, desperation
- 7 new serums: Iron Skin (cuts do a third), Bloodhound (always knows where you are), Painless (feels nothing, then all of it at once), Static Charge (hit him with a weapon and it shocks you), Featherweight (fast, one good hit sends him flying), Rot (weakens and stains), Deep Sleep (puts him to sleep - never kills - until you tap his head or knock him).
- 11 new super serums: Juggernaut, Berserker King, Wraith (blinks behind you), Hivemind (allies share his eyes; kill him and they panic), Bone Lord (bones snap back), Magnet (weapons fly back to his hand), Lazarus (gets up once after dying), Gravity Well, Mirror (your hits come back), Vampire (drains you up close), Shockwave.
- Combinations: Painless + Berserker King, Iron Skin + Juggernaut, Featherweight + Wraith, Static Charge + Magnet.
- Weapon proficiency: every NPC is a Novice, Trained, Veteran or Master - parrying, attack tempo and recovery from an interrupted attack - independent of personality. Returning NPCs keep theirs.
- Desperation Response: badly hurt NPCs beg with their hands, signal stop, back off, hesitate, call allies in, run, or turn defiant - by personality and whether they're alone. New setting "Desperation" (Off / Subtle / Normal / Intense).

## 0.42.0 - eyes, jaws, knives, waves that keep their count, Rage Tower, lighting
- Eyes: a hard blow, a blade tip or a thumb in the eye pops it out. It hangs from the socket on the nerve, swings, then tears off and drops. A lost eye is gone for good; lose both and he is blind for good (loses you unless you're on top of him, swings wild, no parrying, hand on his face).
- Teeth and eyeballs always fly now (not only with "Face cave-in & skull bits" on).
- Broken jaws get held: a hand goes up to the jaw again and again.
- Daggers: an unarmed NPC who's taking a beating sometimes pulls a dagger. Rage Engine has no fist fighting of its own, so it doesn't clash with fist-fighting mods.
- More bone-break reactions: cradling and staring at a broken arm, going down holding a broken leg, gritting it out and charging (tough NPCs), being sick from the pain, passing out from the pain.
- Punching through the chest is removed.
- Waves keep their max: when Rage Engine keeps an NPC going after the game counted him dead (dying moments, adrenaline, passing out), he goes back into the wave's count, so breaking bones no longer pulls in extra enemies. New setting "Max NPCs at once" (Game default or 1-15).
- Sound effects are gore only: bones, flesh, blood, teeth, eyes, glass. No more punch / body-hit / whoosh / thud sounds on top of the game's.
- Fewer crashes and stutters: the per-NPC update no longer allocates thousands of small objects a second, each part runs on its own, and a part that keeps failing pauses for 20 s instead of erroring every frame.
- Map lighting on every map: maps are built in 12 m cells so every lamp lights its own area, lights are brighter and reach further, more lights per object while you're in a map, and fill lights are added automatically wherever a floor is left dark.
- Glass shows on Quest (frame + light streaks) and still breaks.
- Rage Block NYC: the subway train runs - pulls in from the tunnel, waits, pulls out. Don't stand on the tracks.
- New map Rage Tower: two empty floors of a skyscraper under construction, crates, pallets, glass all round (break it and throw them out), a bridge to the tower next door, the roof.
- 5 new waves in the Rage wave category: Saloon Showdown, Brutes, The Gauntlet, Ambush, Last Man Standing.

## 0.26.0 - combos, self-injection, bones and faces, tanks, maps built by the mod
- NPC boxing: planned combinations (1-2, 1-1-2, 1-2-3, body-body-head, 1-6-3-2 ...) with alternating hands, head/body targets and faster hand-offs inside a combination. Better fighters (aggressive, high HP, on a serum) throw the longer ones.
- Downed NPCs keep fighting: they swing at your legs when you're close and drag themselves after you. Only the truly terrified give up. Being unable to stand counts far less toward surrender.
- HP = toughness: the more HP, the higher the pain threshold (hits land lighter, pain builds slower).
- Super soldier durability: on any super serum, blunt hits land at about a third and 60% of blunt damage is shrugged off; no neck snaps; knockouts and knockdowns almost never; no punch-through. They still take damage.
- Blood cough needs ribs broken on BOTH sides or a long body beating (8+ solid body shots).
- Inject yourself: needle on your own body + trigger. Super serums buff you (regen, damage shrug, speed, hit power), the nasty serums hurt. Marker over your hands while active (~75 s).
- Gore: teeth come out easier (up to 12, floor clatter); finger bones through the skin on broken hands; rib ends through the chest; face cave-in in three stages with real mesh dents (also on corpses).
- 22 new gore textures, 26 new sounds (all cut from real recordings).
- White blood: the injection / toxin puff (added in 0.25.3) could render white - its colour is now baked into the material. Also new option Gore > "Rage Engine blood spray" for when other blood mods turn the game's spray white.
- Maps: new menu "Rage Engine: Maps". The mod builds the place itself in the sky above the level you loaded (no SDK bundle): Rage Burger (drive-thru, dining room, kitchen, two bathrooms with stalls, back lot with dumpsters, stairs to the roof, skylight) and the Red Room Diner. Breakable windows, swinging doors, loose chairs/tables/trash cans, runtime NavMesh, enemy waves (normal / super soldiers / mixed).

## 0.25.3 - playtest fixes
- Knockback toned way down: kicks and punches shove instead of launching (max 2.2 m/s for a kick, 1.4 m/s for a punch, only on the part you hit). A man only goes off his feet from a kick when he's under ~35% HP. No more one-kick deaths from slamming into walls.
- NPCs stand and take a beating again: integrity counts pain/HP/bleeding far less; "too broken to stand" only below 8 (was 22), stumbling only below 35 (was 55) and rarer. Uppercuts rock him; they only fold a hurt man.
- Stealth kills removed (the option is gone too).
- Neck snap: grab his neck or head, then press the spell/trigger button on that hand. Holding or twisting alone does nothing.
- Serums always work: no rejection, no partial/unstable/mutation rolls (those options are gone).
- Eye colour: every serum, super serum, custom serum, toxin (incl. oiled blades) and the Loyalty Serum tint his eyes while it's in him. They go back when it wears off.
- Status marker: a glowing marker above his head in that colour, pulsing while it works; it flares and a puff comes off the injection site the moment it goes in.
- Regeneration (Night Soldier, Hero X, Phoenix): step by step he heals spine, broken legs/arms, ribs, wounds and bleeding, jaw/nose/eyes, then the blood and bruises clear. He gets back up, with a green flash at each heal. While one of these is in him his spine can't be broken.
- Super soldier fights: Super Serums > "Super soldier fight" (duel / wave of 3 / wave of 5) + "Their serum" + "Spawn super soldiers". Enemies come from the game's own creature tables.
- Settings are remembered: every Rage Engine option is saved to RageEngine_settings.txt and restored on the next launch.
- Toys menu removed.
- Grips: syringes and injectors are sized so your hand lands on the barrel/flange, with the needle still at the piercing point. Vials and oils sit centred in the palm, 35% bigger.
- Red Room Diner anchoring: map colliders are moved to the game's Default layer, missing colliders are added, and a solid floor slab goes under the map. If you still fall through or float up, you're put back at the start. Player.log lists the collider layers, floor height and player position.

## 0.25.2: Book fix, punch-through, takedowns, stealth, integrity, Loyalty Serum
**Fixes**
- **The book.** Its tabs are a fixed list in the game data (Daggers, Potions, ...). The custom tab names never existed, so nothing showed, the toxin daggers included.
  - Syringes, vials, oils and custom serums are now in **Potions**; toxin daggers are in **Daggers**.
  - New Mod Options > Rage Engine: Serums > **Spawn / Spawn it** spawns any of them even if the book won't.
  - The game log now lists which Rage Engine items the game registered (`catalog: ...`).
- **Toxin coat buttons** removed from Mod Options. Use the toxin oils or toxin daggers.
- **Purple map.** The map was built with the 1.1 SDK and your game is 1.3.2, so its shaders don't match. When the diner loads, Rage Engine now rebuilds every material on the game's own shader with the map's textures (Textures/Map).
- **Weapons dropping for no reason:**
  - The "fair fight" disarm (NPC throws his weapon away when you're barehanded) is now an option, OFF by default.
  - Hold fatigue only drops a weapon when the grip is really ruined.
  - The "weapon came loose" watchdog no longer misfires on long weapons.
- **Easier grabs and throws:** a lower yank threshold (1.4 m/s), easier judo and lifts, and legs loosen while you hold them.

**New**
- **Punch-through:** a fist at 7+ m/s (setting), or a heavy blunt weapon, into the chest goes through and kills. Tough or high-HP or super-serum NPCs survive it. Option: Deaths > Punch-through kills.
- **Uppercuts:** a rising hit to the jaw snaps the head back and lifts the body; weak or low-HP NPCs fold and go up. Jaw crunch, daze, KO chance.
- **Knockback:** kicks and strong hits send NPCs flying, more the lower their HP.
- **Stealth:** an NPC who hasn't noticed you dies from a stab or slash to the heart, the back of the chest, the throat, or a stab to the head. A blow to the head knocks him out. Option: Deaths > Stealth kills.
- **Takedowns:** a hand over the mouth smothers him until he passes out; squeeze the trigger to break his jaw. Holding the head and wrenching it hard snaps the neck (option: Deaths > Neck snaps). Choking by the neck works as before.
- **Black eyes:** a dark ring that grows, and the lid swells shut over repeated hits.
- **Structural integrity:** bones, ribs, bleeds, pain and HP loss all subtract.
  - Low integrity means stumbles; very low means he can't stand.
  - Both legs broken, or a leg plus both arms broken: he can't get up.
  - Broken spine: he doesn't move at all.
- **Loyalty Serum** (Potions): inject an enemy and he becomes your teammate (player faction), super-serum soldiers included.

## 0.25.0: Throws, smarter fist fights, Serum Lab, toxin oils, real sounds, new gore
**Fixes from your playtest**
- **Syringes now show up in the book.** Two causes:
  - The syringes were built on the throwing-dagger item. They're now on the same dagger as the toxin daggers, which do show up.
  - The vials weren't flagged for the sandbox book. They're now potion bottles that are.
- **Names:** "Serums" and "Super Serums" in the book, menus and labels. No "Biogenesis" or "X-Series", and no sci-fi codes on the labels: they're plain apothecary labels now.
- **You can throw NPCs.** While they stand, the game's standing ragdoll plus our planted legs glued them to the floor. Now any of these takes them off their feet into a real physics fall that carries your hand's speed:
  - a hard yank;
  - lifting them;
  - a two-handed grab with a pull (judo).
- **NPCs walk around bodies.** A body lying on the floor becomes a navigation obstacle (Unity's NavMeshObstacle, the same component the game uses on its own sliding objects). Option: General > NPCs walk around bodies.
- **Tripping never breaks bones.** Running into a corpse, a dropped weapon or furniture counts as tripping, not as being hit.
- **They hold on to weapons.** Dropping from pain or a fall is rarer, and much rarer for high-HP NPCs and super-serum users. A second hand won't let go of a weapon just to clutch a wound.
- **Heads are easier to smash.** Lower thresholds, and a head lying on the floor takes 1.6x (stomps, a hammer on a downed head).

**Fist fighting**
- **Punches come from real boxing motion capture.** 158 punches from the CMU Motion Capture Database became jab, cross, hook and uppercut paths. The fist follows the recorded path from the NPC's own shoulder, scaled to his arm, and is steered onto you. Timing is the measured timing: about 0.15-0.27 s out.
- **More aggressive.** They step in to punching range instead of hanging back, throw longer combinations, and leave shorter gaps between them.
- **Blocking works.** A squared-up boxer used to keep his guard up but never actually blocked. Now the glove on the side your punch comes from moves into it (head and body), the other glove tucks in, and he can block the next punch of a combination.

**Toxin oils:** 7 bottles in Rage Engine Toxins. Touch one to your blade to coat it (3 coats per bottle). The Mod Options buttons still work too.

**Serum Lab:** combine serums, super serums and toxins into your own serums. There are 6 slots, a reaction registry with 8 combination reactions, and conflict rules. Details in SERUM_LAB.md.

**New gore interactions** (Wounds > Gore interactions):
- broken noses that cave in and pour;
- teeth knocked out (real tooth models);
- throat cuts with pulsing arterial spurts: both hands go to the throat, gurgling, he sinks to his knees;
- a squelch and a gush when you pull a blade out of the gut;
- eye gouging: hold a thumb or finger in an eye.

**Sounds: no more generated sounds**
- Swooshes, injection, serum surge, coating, flesh squelches, one slash and one stab were rebuilt from real CC0 recordings (Freesound and Kenney; see SOUND_CREDITS.md).
- Synthetic breathing and death gasps were removed. The game's own recorded voice lines play instead: effort shouts on punches, a cry when grabbed, a yell when thrown, and choking and muffled pain when badly hurt.

## 0.24.0: Biogenesis (injections, ailments, X-Series serums)
Full details are in **BIOGENESIS.md**.
- **Devices:** 31 new items.
  - Modelled syringes, injectors and vials, built from separate parts: barrel, plunger, stopper, hub, needle, cap, label and liquid.
  - The liquid is coloured, and the plunger and liquid animate during an injection.
- **Physical injection:**
  - The needle must really pierce the NPC. The plunger then drives down over 0.8 s, and the dose matches how far it got.
  - The device becomes empty and can't fire again. Works in either hand.
  - Empty syringes refill from vials.
- **6 ailments:** Neurostatic, Verdant Syndrome, Cryo-Sleep, Adrenal Surge, Hallucinatory Syndrome, Motor Shutdown.
  - They have duration and intensity settings, stack where compatible, and wear off.
- **9 X-Series serums:** Night Soldier, Hero X (with Super Serum Syndrome outcomes), Adrenal Overload (with Last Stand), Titan, Phantom, Phoenix, Ravager, Overmind, Colossus.
  - They show glowing eyes and an aura. The size increase is optional and **off by default**.
- **Research log:** `RageEngine_research.txt` records every injection.
- **Built on existing systems:** everything runs through Rage Engine's existing systems as reversible multipliers. Nothing permanently changes the game's AI.
- **Not possible:** a severed limb can't be re-attached, because Nomad has no un-slice API. Hero X restores limb function and seals the stump instead.
- **Build:** the BoxerCrystal stub members were restored (it compiles again in the shared build project).

## 0.23.0: Toxins, HP-scaled toughness, smoother grabbing
**Toxins (`Toxins.cs`).** A coated weapon shows a glossy bead of the toxin on its tip that keeps dripping.
- **Getting a coated weapon:**
  - **Item book, category "Rage Engine Toxins":** 7 daggers, each permanently coated.
  - **Mod Options > Rage Engine: Toxins:** coats the weapon you hold, good for 6 hits.
- **Dosing:** cuts and stabs deliver a dose, and more hits make it act faster.
- **What each one does:**
  - **Neurotoxin (purple):** purple spreads from the wound, the lips go purple, he grabs his throat, chokes and gasps, collapses convulsing, then suffocates.
  - **Paralytic (yellow):** tremors, then the arms go numb, then the arms go dead (weapon drops), then the legs fold. He's down and awake but can't move; breathing stops at 40 s.
  - **Hemotoxin (crimson):** massive bleeding, coughing blood, bleeding eyes, bruises blooming everywhere.
  - **Sedative (blue):** drowsy, eyelids drooping, punch-drunk stumbling, then asleep for about 35 s. Never lethal.
  - **Acid (green):** a burn that keeps spreading, screaming and clutching, flesh coming off, damage over time.
  - **Berserk (red):** no pain and 35% faster for 30 s, then a crash (exhausted and groggy).
  - **Terror (white):** drops everything, screams, runs and shakes.
- **"Toxins can kill"** (Deaths): when it's off, the lethal toxins stop at collapse and wear off.

**HP-scaled toughness:** HP from 150 up to 2500 scales:
- toughness, up to 1.9x;
- pain sensitivity, down to 0.6x;
- balance, up to 1.4x;
- grip, which resists losing the weapon up to 50% better;
- how long they hold out against toxins.

**Grabbing**
- A held NPC's grabbed limb and torso go looser.
- Balance assist, drunk sway and follow-body pause while you hold him.
- His struggle is a tug instead of a shove, so there's much less bouncing and slipping out of your hand.

**Punch-drunk is calmer:** about 40% less sway, half as many lurches, and smaller lurches.

**Red Room Diner** is now packaged inside the RageEngine mod folder.

## 0.22.0: Pressure Points, gore content, and the map-centre fix
**Fixed: NPCs dragged to the middle of the map (with 3+ NPCs)**
- Rage Engine steers NPC hands with invisible IK anchors. Some were never let go:
  - the "come here" beckon;
  - anchors on bodies the game recycles from its pool;
  - anchors that were parented to a despawned body.
- A hand pinned to a far-away point dragged the whole NPC there.
- New `IkSafety.cs` runs every frame for every creature in the level. It frees any Rage Engine anchor that is on an untracked body, destroyed, or more than 1.2 m from the chest.
- Bodies are cleaned when they are tracked and when they are released. The beckon now hands the arm back to the guard.

**Pressure Points (`PressurePoints.cs`, new menu "Rage Engine: Pressure Points")**
- **Zones:** upper torso, lower torso, abdomen, neck, each shoulder, each arm and each leg. Each one builds up damage, and repeated hits to a tired zone count more.
- **Stages:** impaired, then disabled, then critical.
- **Severity:** Mild, Severe or Extreme.
- **Persistence:** Temporary (heals), Entire encounter, or Persistent (named NPCs return still injured).
- **Smooth transitions:** going down sinks through the knees instead of snapping into ragdoll.

**One functional body state (always on).** Broken bones, cut muscles, dead arms, ribs and zones now combine:
- one leg out: the knee buckles for good, 40% speed;
- both legs out: down, and can't get up;
- a leg plus an arm out: the body gives out;
- a disabled arm: becomes a dead arm and drops its weapon;
- both arms out: drops everything and backs away;
- hurt shoulders or arms: the weapon sags and sometimes slips;
- abdomen: doubles over holding it;
- neck: the head lolls;
- upper torso: weaker swings.

**Critical condition:** the NPC knows it. He stops fighting, tries to get away, shows a fear face, and wheezes and gasps (new breathing sounds).

**Gore content (`Gore22.cs`)**
- **42 new wound textures** (`Textures/Gore`):
  - lacerations, deep lacerations and stab wounds;
  - gunshot entry and exit;
  - split skin, crush wounds, torn flesh, open fractures;
  - abrasions, a burn, skull fracture lines;
  - 4 bruise stages and a black eye;
  - blood splatter, drips, smear, pools and ground splats.
- **Wound classification:** every hit gets the matching texture, painted at a random angle.
- **Blood:**
  - wounds keep dripping down the body;
  - splatter goes onto clothes;
  - bleeders leave drops and a growing pool on the ground (bigger when they're down).
- **Bruises:** contusion, then deep contusion, then hematoma (the skin swells with real mesh deformation), then split skin.
- **Skull integrity:** heavy blunt head hits crack the skull first (fracture lines, a dent, a skull chip, dazed). A heavy blow on a cracked skull, or one massive blow, smashes it.
  - The weapon's mass and swing speed both count.
  - Fists never smash skulls.
  - Smashing throws skull fragments, brain, teeth and a jaw piece.
- **New Deaths options:** "Head smashing" (on/off) and "Head smash ease".
- **New models:** 6 skull fragments, 3 brain pieces, a tooth, a jaw fragment, a rib, a hand bone, a vertebra.
  - Ribs poke out on severe rib fractures.
  - The hand bone shows on open hand breaks.
  - A vertebra shows on a spine snap.

**Physics**
- **No snap-back:** for up to 0.8 s after a hit, the NPC's movement anchor follows where the body went, and hard hits keep the upper body loose a little longer.
- **No broken legs from trips:** hitting the floor or a wall never breaks a bone unless it's a huge fall.
- **Balance on the run:** stronger catch-yourself when they stumble mid-sprint.

**Blocking**
- Guarding is skill-based per NPC, and gets worse when punch-drunk, in pain or critical.
- They block body shots as well as head shots.
- Armed NPCs block punches with their free hand.
- The game's own parrying (`BrainModuleDefense`) is tuned per NPC: reaction timing, parry radius and weapon speed. It weakens as their arms get hurt.

## 0.21.0: Polish from the 0.20 playtest
**Deaths (new settings category "Rage Engine: Deaths")**
- **Shock can kill** (on by default). Off: no shock deaths.
- **Bleeding out can kill** (on). Off: wounds still bleed and weaken them, but bleeding never takes the last hit point.
- **Knockouts can kill** (on). Off: a knocked-out NPC comes round after 6-12 s, punch-drunk, and fights on.
- **Choke-outs can kill** (off), moved here from the hidden settings.

**Pain tolerance**
- A poke or nick no longer makes an NPC drop his weapon. Letting go from pain now needs 85% pain after each NPC's sensitivity (was 55% raw).
- Agony (dropping everything) needs 88% pain *and* a real hit.
- Pain sensitivity varies less between NPCs (0.8-1.15 instead of 0.75-1.35).

**Fist fighters keep fighting**
- An NPC who squared up, or who chose a fair fight, can't surrender for 30 s unless he's badly injured.
- Pain alone never makes anyone surrender, and nobody surrenders in their first 10 s.

**Punch-drunk (`Drunk.cs`)**
- Blunt head hits stack up (body hits a little), and it wears off over about a minute.
- **Tipsy:** confused face, lolling head, slower.
- **Drunk:** sways, lurches a step and catches himself with arms out, swings come late, groans.
- A very drunk NPC is knocked out by a lighter head shot.

**Steadier on their feet**
- **Active balance:** a standing NPC who's tipped pulls his upper body back over his feet.
- **Fewer stumbles:** they need a harder hit and lower balance to start, and are much less likely to fall at the end.
- **Legs:** a buckling knee bends to 50% instead of folding to 30%. Cut legs stay at 72% or more.

**Toys**
- **Pistol:** fitted to the hand that grabs it (barrel along your hand, slide on the thumb side, grip in the palm). It shoots along the modelled barrel.
- **Bat and pipe:** lined up with the base weapon's own handle axis, so the model is where the physics is.

## 0.20.0: The phantom-fist fix, working with vanilla, toys, brawling
**The game-breaking bug (fixed)**
- NPCs were getting the invisible fist tool at spawn, from the game's item pool, parked at the world origin. Their arm got dragged to the centre of the map and they died.
- Now: the fist tool is spawned fresh (never pooled) directly in the hand, never in the first 8 s after an NPC appears, never if the NPC holds any real weapon, and it is released from every hand before it is removed.
- A hand watchdog runs every frame: anything an NPC holds that is more than 0.6 m from its hand is let go at once.

**Working with vanilla (`Vanilla.cs`)**
- Weapon theft off: NPC weapons can't be stolen and the game's arm-grab and arm-stab disarms are off. Everything is restored when the mod is disabled.
- Electrocution: while an NPC is being shocked, Rage Engine releases every override (muscles, clutching, guard, punches) so the game's shock animation runs clean.
- Less leaning: dazed NPCs only loosen the head and neck; the wounded hunch is smaller.
- Facial expressions follow pain, fear, anger, confusion and fatigue.

**Health:** "NPC health" is an absolute 0-5000 HP slider (default 300).

**Toys (Mod Options > Rage Engine: Toys)**
- Pistol, dynamite bundle, baseball bat and lead pipe, each with its own textured model (Models/RE_Prop_*.obj, Textures/RE_Prop_*.png).
- They are built on game items for physics and damage; if a model can't load, the old tinted item is used instead.

**Brawling and reactions (`Brawl.cs`)**
- Back hits: they arch forward, stumble, reach back; a low back hit is a kidney shot that folds them.
- Legs: hurt legs buckle at the knee, and badly hurt legs stay bowed.
- Knockout: enough head trauma and they go limp with their eyes shut, then die 3.5-8 s later. This counts as a death for waves.
- Big chest or belly slash: they drop the weapon and stop fighting.
- Broken hands: they can't hold anything in that hand.
- Every bone break gets a scream and a shock jolt. Cracked ribs and heavy body blows make them cough blood.
- Impaled NPCs grab the weapon and pull it out, then toss it.
- Fists: an NPC who loses its weapon shouts and squares up. Some "fair fighters" throw their weapon aside and beckon you in if you're barehanded.
- An angry NPC you threw gets up and charges you.
- Groups: at most two attack at once; the rest keep 2.6 m away, flank and wait for an opening. Armed NPCs sidestep your swings.
- Spine breaks can be survived sometimes (the 0.18 change, which never actually deployed before).

**Two surprises**
- **Rage Moments:** a skull crush, spine snap or knockout drops the game into slow motion for a heartbeat (at most once every 20 s).
- **Old scars:** named NPCs who escaped you come back with the scars you gave them painted on, and a limp if you broke their leg.

**Gore:** the crater mesh is gone, replaced by red flesh chunks plus a painted wound and blood.

## 0.19.0: Both feature lists, all of it
**Senses and mind (`Awareness.cs`)**
- **Reactive flinching:** these sounds are broadcast with a loudness:
  - screams, deaths, metal clashes, body falls, gunshots and explosions
  - your weapon or fist whipping past an NPC's head (a near miss)

  Head and shoulders jerk away, and a hand comes up on big ones. It scales with the NPC's nerve, fades with repetition, and doesn't cancel what they're doing.
- **Awareness memory:**
  - Sight: field of view, range by awareness trait, real line of sight (walls block it), not when blind or eyes shut.
  - Last place they saw you, last thing they heard.
  - When they lose sight of you, they look where you were, then search. Old information fades.
- **Eye tracking:** their eyes turn toward you, a sound, your last known spot, or idle glances (including at other NPCs). Small darting movements, clamped to 28°. Destroyed or shut eyes don't track.
- **Procedural idle:** weight shifts and look-arounds when not fighting, timed by patience; they slump when tired. Every NPC is on its own rhythm.
- **Hidden traits per NPC:** confidence, aggression, awareness, loyalty, patience and risk, from personality plus randomness.
  - They drive flinch size, head-slip chance, punch rhythm, how close you can be before they risk fetching a weapon, surrender, and how they take an ally's death.
- **Relationships:** NPCs who fight side by side become friends, saved in the roster. When a friend (or any ally, for loyal NPCs) gets hurt or killed nearby, they get furious, or shaken if timid.
- **Surrender:** badly hurt, scared and not confident means they give up.
  - They drop their weapon and take one of three poses: hands up, hands out begging, or covering their head. Their hands shake. No walking, no swinging.
  - Hit them while they're surrendering and they either run or fight in desperation.
  - Leave them alone for 12 s and they pull themselves together.

**Physics and stability (`Stability.cs`)**
- **Hit recognition:**
  - One collision never reacts twice.
  - Each body region has its own cooldown.
  - Glancing blows only get a minor reaction.
- **Stumbling:** a solid hit while already off balance makes them stumble a few steps with their arms thrown out. Then a balance check: they catch themselves or go down. Walls catch them (existing reflex).
- **Dazed:** a high punch-drunk meter means drunken sway: random unsteady steps, knees dipping, covering up.
- **Ragdoll settling:** bodies at rest stop jittering. The original physics are restored on despawn.
- **Stuck and crowding:**
  - Walking into a wall: they sidestep. After 3 failed tries they pause that path briefly.
  - NPCs standing on each other get spaced out.
- **Get-up:** they won't try to stand while something is on top of them. Tired or slow-recovering NPCs stay down longer.
- **One fixer:** clip lifts, float drops and unstick nudges share one cooldown, so they can't fight each other.

**Combat and presentation**
- **Defense variety:** cover up, lean back out of it, or duck under it. Three guard styles per NPC: high guard, peek-a-boo, lead hand low.
- **Punch preferences per NPC** (jabbers versus hookers), effort grunts on punches.
- **Injured arm:** a dead arm shows a dark ring at the elbow; an impaired arm is cradled against the body when not fighting.
- **Voices:** every NPC has its own pitch. Personality changes volume, and a broken jaw lowers the voice.
- **Clothing:** blood soaks into clothes where they're cut or stabbed (painted into the clothing texture).
- **Face:** the head mesh is checked for pain or frown shapes. If one exists it follows pain and fear, and the log lists what's available.

**Diagnostics (`Monitor.cs`)**
- Every 30 s with Debug log on: NPC count, Rage Engine's CPU time per frame (average and worst), behavior changes, conflicts, navigation failures, physics fixes, glancing hits.
- An NPC flipping behavior 6+ times in 5 s is logged as an RNAS CONFLICT with the exact sequence.

**Can't be done from a code mod (honest list)**
- Authored animations, true foot placement or IK foot planting.
- Rewriting the game's own target selection and pathfinding.

Rage Engine works around these with physics pushes, IK-driven arms, its own awareness memory and the movement controls it owns.


## 0.18.0: Your playtest notes (includes all of 0.17)
- **Broken spine = instant death.** No getting up, no adrenaline, no dying moments.
- **NPC health slider is back** (Mod Options, General): 0.5x to 10x, default 3x. It applies to NPCs that spawn after you change it.
- **Cut legs stay down.** When a leg's big muscles are cut past 50%, the NPC drops and can't get back up; the game's get-up is cancelled for good. The brain treats them as unable to stand.
- **Blindness is permanent when the eye itself is hit.** A stab near the eye, a deep wound, or a solid slash right across it destroys that eye for good: shut and blind. Blood in the eyes is still temporary. Your 0.16 log showed no eye was ever destroyed; the old rule was too strict.
- **Punch to the throat:** they recoil, both hands go to the neck, they gag and stop swinging for a moment. Your log had neck punches up to severity 0.94 with no reaction.
- **Boxer stance:** a disarmed NPC squared up with you keeps both hands up at the chin. It slips and weaves its head and uses footwork: circling, stepping in to close the gap, stepping out when crowded. During a punch the other hand stays up.
- **Broken bones look like breaks:**
  - The limb visibly kinks at the break (real mesh deformation), and the skin there is painted swollen and dark red-purple.
  - Open fractures: a ragged dark-red wound is painted around the exit. Only the jagged broken end (the last 8 cm of the real bone) pokes out 1.5-2.5 cm; the rest of the bone was trimmed off, so nothing long sticks out along the limb.


## 0.17.0: From your 0.16 playtest log
**Found in the log and fixed**
- **Retrieval spam:** after an NPC picked its weapon back up, RNAS logged "gives up on the weapon" about 10 times and blacklisted the weapon it was holding (67 lines in your log). Picking it up now counts as success, and it logs once.
- **Forced pickups:** they now only go back for their own weapon (within 3.5 m), or another weapon right at their feet. Rocks and throwables don't count as weapons. Brawlers and aggressive NPCs would rather use their fists.
- **Ribs:** ribs re-cracked 37 times. A torso is now capped at 4 cracked ribs.
- **Fewer per-frame allocations** in the weapon scan.

**Visuals**
- **Bruises are painted into the skin texture.** No 3D patches. Each NPC's skin material gets its own copy of its color texture on the GPU, and bruises are stamped in at the exact spot under the hit. Fresh bruises are reddish-purple and darken with more hits. The original texture is restored on despawn. If a device can't do it, the old flat decal is the fallback, and the log says which one is used.
- **Black eyes:** dark purple painted all around the socket, real swelling under the eye and on the lid, and a squint.
- **Real mesh deformation:** swelling (skin pushed out), heavy rib breaks cave the chest wall in, and skull crush. The log reports once whether this game build's meshes can be deformed ("mesh deformation check").
- **Jaw:** it now finds the rig's real hinge axis, always rotates from the stored rest pose, and is clamped to 32° with smoothing, so it can't spin or rotate wildly.

**Behavior**
- **Stabs depend on the weapon:**
  - Dagger: a sharp flinch away and a hand slapped over the wound, then they keep fighting. Three quick shanks fold them over.
  - Spear: drives the whole body back along the shaft. A hard thrust takes non-brave NPCs off their feet. Impalement and pinning as before.
  - Arrow: a jolt.
  - Sword: as before.
  - Unknown modded weapons are judged by their length.
- **Real punches:** NPCs had no punch animation, so punches are procedural. The arm IK drives the fist through a jab, cross, hook or uppercut, with wind-up, snap, follow-through and recovery. The torso turns into it and they step in.
  - Combos depend on personality: aggressive NPCs throw 1-3, cautious ones single shots.
  - A missed punch leaves them off balance. A connecting one damages you.
- **Broken ribs change how they fight:** they guard that side with a hand, hesitate before swinging, grunt and move slower, more so with more ribs broken.
- **Combat adaptation:**
  - If you keep going for the head, they start slipping head shots (the head ducks aside and they step back).
  - If they're losing the exchange, nervous NPCs get scared (hesitate) and brave ones get angry (no hesitation, faster punches).
- **Grabs:** a grabbed limb goes loose and follows your hand instead of snapping back. They struggle to tear free, their free hand reaches to pry your hand off, and they need a moment to recover when you let go.
- **Persistent named NPCs:**
  - Every NPC has a name and a record, saved to `RageEngine_roster.txt` only when something changes.
  - Someone who escapes alive after a fight can come back later as the same person: same name and temperament, braver, and holding a grudge.
  - Killed NPCs never return.
- **Metal heat:** hard metal-on-metal clashes heat a weapon (warming, then hot, then cooling over about 30 s). It glows through the weapon's own emission and is restored when cold. The game's own sparks are unchanged.


## 0.16.0: Foundation: RMSS + RNAS, stability, simple settings
**Architecture**
- **RMSS** (`Rnas.cs`): every NPC has a fixed table of condition tags (no strings, no allocations).
  - Each tag has a shared template: a lifetime (temporary, refreshable, stacking or persistent), a stacking rule (refresh, extend, add intensity, ignore or replace) and a default duration.
  - Physical conditions are persistent tags synced from the systems that own them: dead arm, ruined hand, impaired leg, both legs gone, broken spine, blind, eyes shut, impaled, pinned, weaponless, terminal.
- **Capability checker:** turns the tags into Normal, Impaired or Unavailable for HoldWeapon, UseHands, Move, RecoverBalance, ContinueCombat, Retreat, RetrieveWeapon, Defend and See.
- **RNAS arbiter:** one behavior at a time, by priority: Terminal > Pinned > Incapacitated > Recovering > Adrenaline > Blind > Disengage > Unarmed > RetrieveWeapon > Normal.
  - It owns the movement switch, the game's melee switch and fists versus fetching a weapon, so systems no longer fight over the same NPC.
  - It switches up the priority list instantly and down only after 0.6 s, so the NPC doesn't flicker between behaviors.
  - It re-evaluates on events, with a 0.5 s validation pass. Every change is logged with its reason.
- It does not replace the game's AI. It only steps in where the current action is impossible.

**Fixes**
- **Snap-back (ground adhesion):** the ragdoll is pinned to the locomotion body, which never moved, so hits got yanked back. Solid hits now move the locomotion body too. How far depends on the hit and the NPC's balance stability.
- **Ground clipping:** if an NPC's hips are under a floor-like surface with nothing below them for 0.5 s, the parts below the surface are lifted out. This is logged.
- **Weapon-retrieval loop:** RNAS only goes for a weapon that is within 3 m and safe to reach (you're not on top of them). It has 3.5 s and 2 tries. Then that weapon is blacklisted for 12 s and they fight with fists.
- **Blind NPCs no longer escape:** a blind NPC, or one with eyes shut from blood, shuffles a few uncertain steps at 30% speed, stops, turns and listens. No confident run to an exit.

**New**
- **Pinning:** any deep stab is tracked, not only torso stabs.
  - If the weapon is also stuck in the world (nobody holds it, it doesn't move, and its blade overlaps static geometry), the NPC is pinned. It can't walk, struggles, grabs at the weapon and screams.
  - Adrenaline can let a pinned NPC swing, never walk away.
  - Pulling the weapon out releases the pin, and RNAS re-evaluates.
- **Adrenaline is opt-in per NPC.** Most NPCs have no profile and never get it. Each profile triggers once:
  - Relentless: at the killing blow.
  - Last Stand: longer, at the killing blow.
  - Unyielding: a live rush on a deep stab.
  - Adrenaline never repairs anything. When it ends: Exhausted (slower), then RNAS re-evaluates.
- **Individual physical profiles:** every NPC rolls durability, balance stability and pain sensitivity on top of its personality. Pain sensitivity changes agony and how long they hold wounds; balance stability changes sway and travel. There are no sliders for any of this.
- **Face:** a blunt hit near an eye gives a black eye (darker stacked bruise). A hit on the nose starts a nosebleed that drips for 15-40 s.
- **Audio:** a shuffle bag, so every variant plays before any repeats and never the same one twice in a row. Capped at 14 simultaneous sounds.
- **Developer mode:** Regenerate nearby NPCs (heals, resets everything and brings back the dead), Log NPC conditions (behavior, tags and capabilities to the log), plus the test pistol and dynamite.

**Settings: 70 down to 4 plus developer mode**
- Rage Engine enabled, Effect volume, Debug log, Leave deaths to other mods, Developer mode.
- Everything else is internal, tuned defaults. Old saved slider values (like a low punch strength) no longer apply.


## 0.15.0: Hit director: they react to the injury
Every blade hit is read for where it landed, slash or stab, how deep and how hard, and the NPC reacts to that injury instead of playing the game's small stagger.
- **Face slash:** the head snaps away and the spine bends back with the hips braced. They step back, then a hand goes to the face and they keep fighting.
- **Throat:** recoil, both hands to the throat, choking and coughing blood.
- **Gut stab:** instant. They double over the blade, drop what they hold, both hands go to it, and they stop attacking.
- **Chest stab:** they fold around it, gasp and cough blood.
- **Blade left in them:** they writhe and thrash in pain, standing or on the ground, screaming, hands on the blade. When it comes out: a scream and a gush of blood.
- **Body slash:** the trunk twists away along the cut and a hand goes to it.
- **Arm or leg:** the limb jerks away; a leg wound buckles them with a stagger.
- **Adrenaline on the stab:** a deep stab can trigger an adrenaline rush right there. They ignore the wound and keep attacking, a little faster. When it wears off, all the pain hits at once.
- **Fixed: floating after adrenaline.** A revived NPC now gets up from where the body lies. A watchdog also drops any living NPC that ends up hovering.
- **Skull crushing:** the head's real mesh is now deformed, caved in around the blow with a jagged rim, plus skull fragments with the flesh chunks. The old head-bone scaling is gone; it could leave pooled NPCs with misshapen heads. The original mesh is always restored when the NPC despawns.


## 0.14.0: Muscles, agony, eyes, wound holding
- **Real muscles.** Each NPC has the main muscles of the body on each side, and a cut damages the one under the blade. Damage never heals during a fight.
  - Forearm flexors and hand tendons control grip. A weak grip shakes and lets the weapon slip; a ruined grip drops it, and that hand can't pick anything up again.
  - Biceps, deltoid and triceps control arm strength and how long a weapon can be held up. Cut arms get tired, tremble and drop the weapon.
  - Quadriceps, hamstrings, calves and glutes control the legs: slower, then limping, then down if both thighs are cut.
  - Neck muscles: the head lolls.
- **Agony.** Pain bad enough makes an NPC let go of everything and stop fighting for a few seconds while he screams.
- **No forced pickups.** NPCs only grab weapons or throwables with a hand that can actually hold something. Both arms cut means no grabbing. This also blocks the game's own AI from picking things up with a useless hand.
- **Holding wounds is automatic.** The time comes from pain, not a setting: a few seconds for a scratch, about 16 for agony. They use whichever hand is free. In bad pain they drop the off-hand item to hold the wound. They stop swinging while they get a hand on it, and keep going back to it while it hurts.
- **Eyes.**
  - Blood in the eyes squeezes both eyes shut; they open a crack when he wipes. He can barely see until it clears.
  - A stab to an eye shuts that eye for good and blinds it.
  - Both eyes gone means fully blind.
- **Settings removed** (these follow the injury now): Clutch time, Cut arms go dead, Leg wounds drop them, Break chance, Open fracture chance.


## 0.13.0: Your 0.12 feedback
- **Falling after two face punches:** a punch now bends the spine instead of buckling the knees. The hips and legs stiffen (2.2×) for a moment while the chest, neck and head go loose, so the head snaps back and the body absorbs it. Hits on the game's top push level no longer drop a standing NPC. The knockdown guard now also catches the game's "go limp" state.
- **Base HP on spawn:** NPC health is now applied again whenever the game re-uses an NPC, and retried until it sticks. The default is 3× health.
- **Deformed heads on spawn:** the mod now notices when the game re-spawns a pooled NPC (by its spawn time) and fully restores the body before tracking it again.
- **Eye cuts:** a cut or stab near the eyes now blinds the NPC. He screams, his head jerks back, and he wipes or covers his eyes. He stops attacking (the game's melee is switched off) until the clutch time ends.
- **Bones:** real human bones replace the single generic shard: humerus, radius and ulna, femur, tibia and fibula, and clavicle. They come from the BodyParts3D anatomy database (CC BY-SA). Each limb gets its own bone, with a jagged break and marrow. The bone sits on the actual skin surface and points along the limb.
- **Bruises:** they are now flat colour patches lying on the skin, not 3D welts. They are placed on the real skin of the body you hit, including the face, and are skipped where clothes cover the skin.
- **Gore craters:** these are now placed on the real skin surface.
- **Head hits daze:** this is now off by default.

## 0.12.0: Fixes from the 0.11 playtest log
Everything in this release comes from reading the game log of your session. It covered 32 NPCs, 3,787 Rage Engine lines and 0 errors from Rage Engine.

**Fixed:**
- **NPCs fell after 2-3 punches.** Most knockdowns in the log had no Rage Engine fall behind them. Rage Engine weakened the trunk of hurt NPCs ("hunch"), and coughing and rib winces weakened it further. B&S's balance-driven ragdoll then gave out. Coughing, deaths nearby and winces also pinned balance at 0.05.
  - The trunk and legs now never lose strength while the NPC is standing.
  - Those balance drains are gone, and balance recovers in a second or two.
  - New knockdown guard: if the game itself drops a standing NPC from an ordinary hit, they're put back on their feet in the same frame.
- **Punches knocked NPCs around like crazy.** Hits used to add a fixed speed of up to 6 m/s to the struck part. Now a real impulse is applied at the contact point, so the result depends on the part's weight: heads snap and twist, chests rock, arms swing.
- **No stagger.** Solid hits now make them take the game's own small stagger step. Option: **Stagger on solid hits**.
- **Fist fighting did nothing.** The log showed "fists up" 307 times on one NPC. The game's own behavior tree ("HumanUnarmed") forces disarmed NPCs into the Flee stance every update, and Nomad has no unarmed stance or punch animations at all. Disarmed NPCs with no weapon nearby now get an invisible, harmless phantom one-handed weapon, so the game's melee AI fights instead of fleeing: real jab and swing animations with an empty fist, plus real parries. Their fists hitting you does punch damage (option: **NPC punch damage**).
- **A snatched weapon broke every limb.** The log showed four broken limbs in 0.1 s. A weapon snapping into the hand read as a 30 m/s impact. Contacts above 25 m/s, and items the NPC holds or just grabbed, are now ignored.
- **Gore everywhere.** Swung weapons were measured by hand speed, which tore chunks out of hands and feet and stuck bone shards in heads. Now:
  - Swung weapons only tear flesh from real damage (**Weapon gore damage**, default 45).
  - Momentum gore applies only to thrown or falling objects and slams.
  - No chunks out of hands or feet, and no bone in head craters.
  - Higher default thresholds for head crush and spine break.
- **Textures.** The textures now ship as files in the `Textures` folder (bone, flesh, bruise, plus normal maps), loaded at runtime. The material now comes from the game's own weapon/prop materials, then URP Lit, then an unlit fallback. A **Gore material** option lets you switch in-game, and the log shows which one is in use.
- **"Cannot find dialog" spam on coughs.** Coughing no longer calls a missing voice line.

**Added:**
- 141 real recorded sounds. All licenses allow use in a mod; see SOUND_CREDITS.md.
  - Punches: 30.
  - Body hits: 19.
  - Stabs into flesh: 12.
  - Bullet hits on a body: 10.
  - Bone cracks: 4.
  - Male and female pain shouts: 19.
  - Male and female screams: 26.
  - Heavy impacts: 6.
  - Thrown-object hits: 15.
- Screams and pain shouts now use these recordings, by gender, instead of the game's "falling" voice line.


## 0.11.0: Stability overhaul
**Fixed:**
- **NPCs fell after one or two hits.** The stock brains turn many head and leg hits into a fall. The hit-reaction table of every NPC's brain is now retuned at runtime (grounded).
- **Random tripping.** Leg muscles are no longer loosened while standing. Hurt legs limp instead of going to zero strength. Always-on full-body physics is now off by default. Gore chunks no longer collide with people.
- **Deformed heads, droopy eyes and hanging jaws on new spawns.** Every body change is now undone when the game re-uses an NPC.
- **Bones broke from ordinary hits.** Bone severity ignores the push level now, and breaks need one very heavy hit or repeated heavy hits on the same limb. Bone shows only on severe breaks.
- **Gore and bone looked untextured.** All gore now uses procedural textures and normal maps. A white metallic map no longer makes things look chrome.
- **Clutching stopped too soon.** It now persists and repeats while the wound still hurts.
- **Decapitated NPCs could stay alive for a few seconds.**
- **Bar Brawl fist fighters stood around.** The wave is removed. Fisticuffs now works on every NPC.
- **Compatibility:** Realistic Deaths is detected automatically. Brain and creature files are no longer replaced.

**Added:**
- Fisticuffs: fists up when disarmed, snatch weapons, throw small objects, barehanded blocking.

**Changed:**
- Waves are now only Civilians (endless) and Duel (endless).
- Renamed to Rage Engine (folder `RageEngine`).

## 0.10.0
Punch-drunk knockdowns, gore (flesh tearing, head crush, spine breaks), Haymaker breaks, death gasp with eyes closing, test pistol fixes.

## 0.9.0
Grounded tuning, ribs, test pistol and dynamite, 41 new sounds.
