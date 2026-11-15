# Rage Engine: Sound credits

Every sound was converted (trimmed, loudness-normalised, mono 44.1 kHz WAV) for use in Rage Engine.

**C-Dogs SDL (github.com/cxong/cdogs-sdl), each with its own license:**
- `NRE_Punch_1-18`: adapted from "Punch, slap, n' kick" by CGEffex (freesound.org/people/CGEffex/sounds/98341). CC BY 4.0.
- `NRE_BodyHit_11-19`: derived from "Bullet_Impact_3" by toxicwafflezz (freesound.org/people/toxicwafflezz/sounds/150839). CC BY 3.0.
- `NRE_Stab_1-9`: derived from "1_Knife_Slash_A" by lmbubec (freesound.org). CC0.
- `NRE_BoneCrunch_13`: derived from "knuckles cracking" by borq1 (freesound.org/people/borq1/sounds/475965). CC0.
- `NRE_PainM_9-12`, `NRE_PainF_6-7`, `NRE_ScreamM_9-16`, `NRE_ScreamF_6-10`: C-Dogs character voices from freesound.org. Among them:
  - "Helmut Scream" by creativeheroes. CC BY 4.0.
  - "Male_Thijs_loud_scream" and "women_scream_AAA" by thanvannispen. CC BY 4.0.
  - The rest are CC0 or CC BY 3.0, as listed in the C-Dogs license.txt files.

**Blip (github.com/bliporg/blip), Apache License 2.0:**
- `NRE_PainM_1-8`, `NRE_PainF_1-5`, `NRE_ScreamM_1-8`, `NRE_ScreamF_1-5`: hurt and death screams.
- `NRE_Punch_29-30`, `NRE_ThrowHit_1-5`.

**Secret Histories (github.com/Mooses2k/SecretHistories), freesound recordings. Attribution required; CC BY / CC BY-SA 4.0:**
- `NRE_BulletHit_1-10`: "bullet hit - body" 1-10 by FilmmakersManual (freesound.org/people/FilmmakersManual).
- `NRE_Stab_10-12`: "stabs" by nicktermer (259542); "nasty knife stab 2" by aris621 (478145); blade to flesh.
- `NRE_BoneCrunch_10-12`:
  - "Bone Crunch/Crack 1 (Mixed)" by EFlexMusic (369600). CC BY.
  - "bone crack" by daniramo5 (469250).
  - "bone click" by angel_perez_grandi (69396).
- `NRE_HeavyImpact_1`: "low big boom" by jedimichael (491119).

**Kenney (kenney.nl), CC0:**
- `NRE_Punch_19-28`, `NRE_BodyHit_1-10`, `NRE_HeavyImpact_2-6`, `NRE_ThrowHit_6-15`.
- Earlier Rage Engine sounds (0.9): see FEATURES.md. They're CC0 or made for this mod.

# Bone models (Models folder)
The `RE_Bone_*.obj` files are cut and decimated from BodyParts3D, © The Database Center for Life Science, licensed under CC BY-SA 2.1 Japan (lifesciencedb.jp/bp3d). Sourced via github.com/Kevin-Mattheus-Moerman/BodyParts3D. These modified meshes are shared under the same license (CC BY-SA 2.1 JP).

# 0.25 sounds (real recordings only)
- `NRE_Swoosh_1-4`: "Swosh Sword Swing" by qubodup (freesound.org/s/59992). CC0. Pitch variations.
- `NRE_Squelch_1-4`, `NRE_Stab_1`, part of `NRE_Slash_3`: "Sword hits the body" by vdovitsky (freesound.org/s/411122). CC0.
- `NRE_Inject_1-2`: Kenney RPG Audio "metalClick" (CC0) layered with "Drink / Drinking liquid" by Breviceps (freesound.org/s/445970). CC0.
- `NRE_Coat_1-3`, part of `NRE_Slash_3`: Kenney RPG Audio "cloth" and "knifeSlice". CC0.
- `NRE_SerumSurge_1`: a heartbeat made of Kenney Impact Sounds "impactSoft_heavy" thumps. CC0.
- Freesound files were taken from the Secret Histories repository (github.com/Mooses2k/SecretHistories). Licences were checked on freesound.org.
- The 0.9-era synthesized breathing and death gasps are no longer played. NPC breathing, gasps, effort and pain come from the game's own voice acting.

# Punch motion
`PunchMocap.cs` is derived from the CMU Graphics Lab Motion Capture Database (mocap.cs.cmu.edu), subjects 13 and 14 boxing takes, in the BVH conversion by B. Hahne (cgspeed). The database "may be included in commercially-sold products"; only derived punch paths are included, not the raw data.

# 0.26 sounds (cut and layered from real recordings already credited above)
- `NRE_Teeth_1-4`, `NRE_GlassBreak_1-3`, `NRE_MetalBang_1-3`: Kenney Impact Sounds (plate, glass, metal, tin). CC0.
- `NRE_FingerSnap_1-4`, `NRE_RibCrack_4-6`, `NRE_FaceCrush_1-4`, `NRE_WetPunch_1-5`: layers of the bone-crack, squelch and punch recordings credited above (same licenses), plus Kenney punch/soft impacts (CC0).
