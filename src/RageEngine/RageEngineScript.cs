// Nomad Rage Engine - runtime core (ThunderScript, loaded automatically from RageEngine.dll).
// Compiled against the game's real ThunderRoad API. Every subsystem runs inside Guard(): if one call doesn't exist
// in this game build, that one feature switches itself off (logged once) and everything else keeps running.
using System;
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace RageEngine
{
    public sealed partial class RageEngineScript : ThunderScript
    {
        public const string Version = "0.49";
        const int MaxTracked = 20;
        const float ScanInterval = 0.5f;
        const float TickInterval = 0.05f;
        const float WitnessRadius = 12f;
        const float MinBalance = 0.05f;

        internal sealed class NpcState
        {
            public Creature creature;
            public Personality personality;
            public bool active;
            // balance & muscles
            public float balance = 1f, fatigue;
            public float looseUntil, looseAmount, looseDuration; public RagdollPart.Type looseParts;
            public RagdollPart.Type limpParts;            // permanently dead limbs (broken / cut / stump)
            public bool pinOverridden;
            // pain & injuries
            public float pain; public RagdollPart.Type brokenParts; public bool jawBroken; public float jawSkew;
            public float bleedRate, bleedAccum; public bool bloodInAirway; public float nextCoughTime;
            public List<GameObject> boneMeshes = new List<GameObject>();
            // wounds
            public bool leftArmDead, rightArmDead, legWounded; public float eyeBloodUntil;
            public Transform clutchAnchor; public Vector3 clutchBase; public Side clutchSide;
            public bool clutching, clutchWipe; public float clutchUntil, nextReclutch;
            // choke
            public float chokeTime, knockedOutUntil, nextChokeFx;
            // trauma
            public bool dismembered, fleeing, brave, lostLeg, revivedFromSlice; public float nextScream, dismemberTime;
            public bool adrenalineUsed, onAdrenaline, finalKill; public float adrenalineEnd;
            public bool witnessShocked;
            // physical state system
            public float lastReactTime = -10f;
            public Posture posture;
            public float downSince, stayDown, shakyUntil, shakyDuration = 1f, nextWrithe, lastSpeedMult = -1f;
            public bool cancelGetUpSet;
            public float braceUntil, flailUntil, nextReflex;
            public bool reflexLeft, reflexRight;
            public Transform reflexAnchorL, reflexAnchorR;
            // toughness (most NPCs can take a punch; a few have a glass jaw)
            public float toughness = 1f; public bool glassJaw;
            // dying moments
            public bool dying, dyingUsed; public float dyingStart, dyingEnd; public Vector3 lastHitDir;
            // bruises
            public List<Bruise> bruises = new List<Bruise>();
            // muscles / pain / shock
            public HashSet<RagdollPart> scaledParts = new HashSet<RagdollPart>();
            public bool musclesActive;
            public float weakArmLUntil, weakArmRUntil, nextTremble, nextWince;
            public float headHits, bodyHits, exchangeStart, nextMoodShift, nextSlip, nextFearPause, nextStruggle; public int hitsTaken, hitsLanded; public RagdollPart grabbedPart; public bool pryAnchor; public Side prySide;
            public bool cantStand, stanceOn; public float nextFootwork, nextNeckGrab;
            // 0.19 senses / traits / surrender / stability / diagnostics
            public float trConfidence, trAggression, trAwareness, trLoyalty, trPatience, trRisk, voicePitch = 1f, punchPrefJab = 0.45f, idlePhase; public int guardStyle;
            public Vector3 heardPos, lookAt, seenPos, gazeTarget, idleLookPos, saccade, stumbleDir; public Stim heardKind;
            public float heardAt, lookUntil, habituation, lastStartle, nextSight, seenAt, nextNearMiss, nextIdle, nextIdleLook, nextFriendCheck, nextSaccade;
            public bool lostSight, inCombat, surrendered, surrenderBroken, settled, cradling, limbPainted;
            public Quaternion[] eyeRest; public Vector3[] eyeAxis, eyeAim;
            public float surrenderSince, aloneSince; public int surrenderPose; public Transform surrenderL, surrenderR, cradleAnchor;
            public Collider lastSrcCol; public float lastSrcTime; public float[] regionReactUntil = new float[4]; public int glancing;
            public float stumbleUntil, nextStumbleStep, nextDazeSway, downQuietSince, stuckTime, lastNavFail, navPauseUntil, nextSpacing, fixerUntil, nextConflictLog, nextCradle;
            public int stumbleSteps, navFails, switchIdx; public List<(Rigidbody, float, float)> settleOrig;
            public float[] switchLog = new float[8]; public Behavior[] switchSeq = new Behavior[8];
            public float nextBlockCheck; public bool getUpBlocked;
            public SkinnedMeshRenderer faceSmr; public int faceShape = -1; public Side cradleSide;
            public float trackedAt;
            // 0.20 brawl / vanilla integration
            public RagdollPart.Type buckleLegs; public float buckleUntil, koDeathAt, pullDoneAt, nextPullTry, beckonUntil, nextInviteCheck, nextFlankStep, chargeUntil, nextDodge, pointAtUntil, nextFace;
            public bool knockedOut, announcedFists, invited, fairFighter, waiting, shocked, origArmGrabDisarm, origArmStabDisarm, disarmPatched; public RagdollHand pullHand; public Transform pointAnchor, beckonAnchor;
            public FaceAnimator.Expression lastFace = FaceAnimator.Expression.Neutral; public int scarMask;
            // 0.21
            // 0.24 biogenesis
            public List<BioFx> bioFx = new List<BioFx>(); public bool bioDirty, bioActive, bioNoQuit, bioSetCantStand; public int bioSealed, bioIndicatorComp = -1, bioXVisual = -1;
            public float bioLastTick, bioTough = 1f, bioPain = 1f, bioBalance = 1f, bioReact = 1f, bioStrength = 1f, bioFlinch = 1f, bioSpeed = 1f, bioDmg = 1f, bioHeight = 1f, bioBaseHeight, bioNextPaint, bioNextHalluc, bioLastStandUntil, bioNextRestore, bioAllyBoostUntil;
            public GameObject bioIndicator; public List<GameObject> bioVisualObjs = new List<GameObject>(); public RagdollPart bioSite; public Vector3 bioSiteLocal; public Color irisOrig = Color.white;
            // 0.23 toxins / HP scaling / held
            public int tox = -1, toxStage, toxFlags; public float toxDose, toxStart, toxNext, hpK, heldUntil, grabY;
            // 0.25 gore interactions
            public List<RageEngineScript.ActiveRx> labRx = new List<RageEngineScript.ActiveRx>(); public string labSerum = ""; public float labUntil; public bool labRebirthUsed;
            public bool stealthKilled, punchedThrough, neckSnapped, paralysed, ally, castPrevL, castPrevR, neckGrabL, neckGrabR; public float neckGrabAt, neckSnapUsedAt, eyeTintKey = -1f, injectFlashUntil, nextRegenStep, regenGlowUntil; public Color labColor, injectColor; public int ribsL, ribsR, bodyShots; public float faceDepth, nextShrugLog, groundSince, nextGroundAct, groundSwingUntil, faceDmg, nextFaceCave; public bool crawlFlip; public int faceStage, fingersOut, ribsOut; public float powerHitAt = -10f, hpT = 1f, bigHitAt = -10f; public Transform shoulderAnchor; public Side shoulderSide; public bool shoulderOn; public float occiput, gutShots, nextVomit, vomitUntil, nextVomitSpurt, chokeBloodSince, nextChokeCough, convulseUntil, nextConvulse, hpOverride; public float[] eyeDmg = new float[2]; public bool hasKnife, knifeOut, knifePending; public List<RageEngineScript.Knot> knots = new List<RageEngineScript.Knot>(); public bool passedOut; public int headSaves; public float saltsT; public int[] comboPlan, comboLast; public int comboIdx; public bool punchFirst; public GameObject fxMark; public bool spineShrugged, regenClean; public float nextSpineShrug; public float mouthT, integrity = 100f, nextIntegrityTrip; public int[] blackEye = new int[4];
            public bool noseBroken, gutImpaled; public int teethLost, gouged; public float throatCutUntil, nextSpurt, gougeT; public RagdollPart throatPart, gutPart; public Vector3 throatLocal; public Vector3[] eyeScale; public bool hpScaled; public RagdollPart toxPart; public Vector3 toxLocal;
            // 0.22 gore / physics
            public List<BruiseSpot> bruiseSpots = new List<BruiseSpot>(); public List<WoundSpot> woundSpots = new List<WoundSpot>(); public int swellCount; public float nextGroundBlood, skull = 100f, followUntil, defSkill = 0.6f, nextDefense; public GameObject pool; public Vector3 followBase; public float[] defOrig; public bool defPatched;
            // 0.22 pressure points
            public float[] ppZone = new float[(int)PZ.Count], ppFatigue = new float[(int)PZ.Count], ppLastHit = new float[(int)PZ.Count]; public int[] ppStage = new int[(int)PZ.Count];
            public bool ppCritical, ppDirty; public float ppSpeed = 1f, ppLastTick, ppNextClutch, ppNextFumble, ppNextBreath, ppSinkStart, ppSinkUntil;
            public float drunk, drunkPhase, lastDrunkTick, nextLurch, nextDrunkGroan, koWakeAt, fistsAt = -100f; public int drunkStage; public RagdollPart.Type koLimbs;
            public Item ownWeapon; internal NpcRecord identity; public bool diedFinal;
            public int punchPhase, comboLeft, blocks, looseL, looseR; public float punchT, nextPunch, punchTempo, blockUntil; public Side punchSide; public PunchType punchType; public Vector3 punchFrom; public Transform punchAnchor; public bool punchLanded, punchAimHead;
            public Vector3 jawAxis; public float jawOpenCur, lastShank; public int shanks;
            public int ribsBroken; public float nextRibWince, nextWinded, ribHesitateAt, ribGuardAt; public RagdollPart ribPart; public Vector3 ribLocal;
            public float shock, shockDeathAt;
            public Quaternion jawRest; public bool jawTorn; public float jawSwing, jawSwingVel; public Vector3 lastHeadPos;
            public float nextHitSfx;
            // 0.10
            public float deathTime, daze, physUntil, lastSlamTime = -10f, nextTear, nextEyeWipe;
            public bool physForced, spineBroken, headCrushed, headPopped49, noGasp, brawler;
            public int craters;
            // 0.11
            public Transform headMesh; public Vector3 headScale;
            public bool scleraTinted; public Color scleraOrig;
            public float stressLA, stressRA, stressLL, stressRL;
            public float unarmedSince, nextFistThink, nextThrow, nextGuard, guardUntil, rearmUntil, throwAt;
            public Item throwItem; public Side throwSide; public bool throwing;
            // 0.14 musculature, agony, eyes
            public float[] muscles = new float[2 * (int)Mu.Count]; public bool musclesDirty, gripBlockL, gripBlockR, eyesWereShut;
            public float holdFatigueL, holdFatigueR, agonyUntil, eyesShutUntil; public int blindEyes;
            public bool impaled49; public int stuckReach49; public float nextReach49, nextTaunt49, nextFearVoice49, nextKnife49, forceFall47, mustFall49, downSince49, lowDrop49; public bool loadoutDone47; public float nextDodge47, nextPickup47, nextShare47;   // 0.47
            public bool will44, wearOn44, vomitHold44, gutsOut44; public float nextNeckGrab44; public float wear44, wearSpeed44 = 1f, nextWear44;   // 0.44
            public bool hound43, deepSleep43; public float next43, rage43, burnout43, rageMaxAt43, magnetNext43, prof43, profAnim43, profDelay43, profRec43, despUntil43, despNext43; public int despPose43; public Item magnetItem43; public BrainModuleMelee profMelee43; public BrainModuleHitReaction profHit43; public Transform despL43, despR43;   // 0.43
            public int beatHits42; public float beatAt42, nextJawHold42, nextBlindSwing42; public bool blind42; public WaveSpawner wave42;   // 0.42
            // 0.15 hit director
            public float nextDirected, clutchAt, impaleSince, nextThrash, nextImpaleScream, rushUntil, airTime;
            public RagdollPart clutchAtPart, impalePart; public Vector3 clutchAtPoint; public bool clutchAtBoth, rushUsed, clutching2;
            public Item impaleItem; public Transform clutchAnchor2;
            // 0.16 RMSS / RNAS / physical profile / pinning / ground watchdogs
            public float[] tagUntil = new float[(int)Tag.Count], tagIntensity = new float[(int)Tag.Count];
            public Ability[] caps = new Ability[(int)Cap.Count];
            public bool rnasDirty, blindMoving, pinned; public float rnasNext, behaviorSince, retrieveUntil, retrieveFirstTry, badWeaponUntil, blindMoveFlip, pinCheckAt, clipTime;
            public Behavior behavior; public int retrieveTries; public Item retrieveTarget, badWeapon; public Collider[] impaleCols;
            public float noseBleedUntil, nextNoseDrip; public int blackEyes, bruisesPainted;
            public AdrenProfile adrenProfile; public float balanceStability = 1f, painSensitivity = 1f;
            public Transform guardL, guardR;
            // 0.12
            public float lastHitTime = -10f, nextStagger, allowFallUntil, lastGrabTime = -10f, phantomAskedAt = -10f;
            public Item phantom, lastGrabbed; public bool phantomPending;
            public Ragdoll.State prevState = Ragdoll.State.Standing; public int blockedFalls; public float blockWindowStart;
            // 0.13
            public float coreUntil, spawnTime = -1f, meleeOffUntil, healthCheckAt; public bool healthApplied, meleeOff;
            public Mesh baked; public float bakedAt = -10f;
        }

        readonly NpcState[] pool = new NpcState[MaxTracked];
        readonly Dictionary<Creature, NpcState> byCreature = new Dictionary<Creature, NpcState>(MaxTracked);
        float nextScan, nextTick;
        internal static RageEngineScript instance;
        internal static string modFolder;

        // ---------------- feature guards ----------------
        static readonly HashSet<string> disabled = new HashSet<string>();
        internal static void Guard(string feature, Action a)
        {
            if (disabled.Contains(feature) || Muted42(feature)) return;
            try { a(); }
            catch (Exception e) { Fail42(feature, e); }
        }
        /// 0.42: hot loops call features directly (no closure per call) - On() before, Fail42() in the catch.
        internal static bool On(string feature) => !disabled.Contains(feature) && !Muted42(feature);
        internal static void Fail42(string feature, Exception e)
        {
            if (e is MissingMethodException || e is MissingFieldException || e is TypeLoadException || e is MissingMemberException)
            {
                disabled.Add(feature);
                Debug.LogWarning($"[RageEngine] '{feature}' disabled: not available in this game version ({e.Message})");
            }
            else
            {
                NoteError42(feature);
                if (debugLog) Debug.LogWarning($"[RageEngine] {feature}: {e.GetType().Name} {e.Message}");
            }
        }
        internal static void Log(string msg) { if (debugLog) Debug.Log("[RageEngine] " + msg); }

        // ---------------- lifecycle ----------------
        public override void ScriptLoaded(ModManager.ModData modData)
        {
            base.ScriptLoaded(modData);
            instance = this;
            try { modFolder = modData.fullPath; } catch { }
            myModData = modData;
            Guard("sounds", () => Sfx.LoadAll(modFolder));
            Guard("textures", () => TexGen.LoadFiles(modFolder));
            Guard("models", () => BoneModels.Load(modFolder));
        }

        public override void ScriptEnable()
        {
            base.ScriptEnable();
            instance = this;
            for (int i = 0; i < MaxTracked; i++) pool[i] = new NpcState();
            Guard("events.kill", () => EventManager.onCreatureKill += OnCreatureKill);
            Guard("events.slice", () => EventManager.onRagdollSliced += OnRagdollSliced);
            Guard("events.unload", () => EventManager.onLevelUnload += OnLevelUnload);
            Debug.Log("[RageEngine] Rage Engine " + Version + " enabled (grounded=" + groundedReactions + ", deaths by other mod=" + DeathsHandledElsewhere + ")");
        }

        public override void ScriptDisable()
        {
            Guard("events.kill", () => EventManager.onCreatureKill -= OnCreatureKill);
            Guard("events.slice", () => EventManager.onRagdollSliced -= OnRagdollSliced);
            Guard("events.unload", () => EventManager.onLevelUnload -= OnLevelUnload);
            ReleaseAll();
            Guard("steal", RestoreStealFlags);
            Guard("slowmo", () => { if (TimeManager.slowMotionState != TimeManager.SlowMotionState.Disabled) TimeManager.StopSlowMotion(); });
            Guard("roster", () => SaveRosterIfDirty(true));
            Guard("grounded", UngroundAll);
            base.ScriptDisable();
        }

        void UpdateCore()
        {
            if (!modEnabled) { if (byCreature.Count > 0) ReleaseAll(); return; }
            if (OrgansOnly) { if (byCreature.Count > 0) ReleaseAll(); Guard("settings", TickSettingsSave); return; }                  // 0.47: only the organs (they run from the hit event)
            float now = Time.time;
            // muscles every frame while a reaction is running, so the game's own pin updates can't cancel it
            for (int i = 0; i < MaxTracked; i++)
            {
                var m = pool[i];
                if (!m.active || m.creature == null) continue;
                if ((now < m.looseUntil || m.limpParts != 0 || m.musclesActive || m.pain > 0.05f || now < m.weakArmLUntil || now < m.weakArmRUntil || m.dying) && On("muscles"))
                    try { TickMuscles(m); } catch (Exception e) { Fail42("muscles", e); }
                if (true) try { GuardKnockdown(m); } catch (Exception e) { Fail42("antifall", e); }
                if (On("handwatch")) try { HandWatchdog(m); } catch (Exception e) { Fail42("handwatch", e); }
                if (now < m.followUntil && now >= m.heldUntil && On("follow")) try { FollowBody(m); } catch (Exception e) { Fail42("follow", e); }
                if (fistFighting && m.punchPhase != 0 && !m.shocked && On("punch")) try { TickPunch(m); } catch (Exception e) { Fail42("punch", e); }
            }
            Guard("ikwatch", IkWatchdog);                                                  // 0.22: map-centre fix
            Guard("rage", TickRageMoment);
            if (now >= nextScan) { nextScan = now + ScanInterval; Guard("scan", ScanForNpcs); Guard("roster", () => SaveRosterIfDirty()); Guard("heat", TickHeat); }
            Guard("tools", TickTools);
            Guard("coats", TickCoats);
            Guard("devices", TickDevices);
            Guard("corpses", TickCorpses);
            Guard("mapfix", TickMapFix);
            Guard("castinput", PollCastInput);
            Guard("self", TickSelf);
            Guard("mixed", TickMixed28);
            Guard("cheats", TickCheats28);
            Guard("map26", TickMap26);
            Guard("waves42", TickWaves42);
            Guard("settings", TickSettingsSave);
            Guard("duelspawn", TickDuelSpawns);
            Guard("pending", TickPending);
            if (now >= nextTick)
            {
                float dt = TickInterval;
                nextTick = now + TickInterval;
                for (int i = 0; i < MaxTracked; i++)
                {
                    var s = pool[i];
                    if (!s.active || s.creature == null) continue;
                    TickNpc42(s, dt);
                    if (s.daze > 0f) s.daze = Mathf.Max(0f, s.daze - 0.08f * recoverySpeed * dt);
                }
            }
        }

        public override void ScriptLateUpdate()
        {
            base.ScriptLateUpdate();
            if (!modEnabled) return;
            Guard("jaw", LateUpdateJaws);
            Guard("eyes", LateUpdateCorpses);
            Guard("gaze", LateUpdateGaze);
            Guard("faces", LateUpdateFaces);
            Guard("eyes2", LateUpdateLivingEyes);
            Guard("gore", LateUpdateCrushedHeads);
            Guard("frame49", Frame49);
        }

        // ---------------- tracking ----------------
        void ScanForNpcs()
        {
            var list = Creature.allActive;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c == null || c.isPlayer || !c.loaded) continue;
                if (c.isKilled && !byCreature.ContainsKey(c)) continue;
                if (!byCreature.ContainsKey(c)) Track(c);
            }
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = pool[i];
                if (!s.active) continue;
                if (!InWorld(s.creature)) Release(s);           // despawned / pooled: put the body back first
                else if (Respawned(s)) { var rc = s.creature; Log($"{rc.name} was re-used by the game: fresh state"); Release(s); Track(rc); }
                else if (!s.healthApplied && Time.time >= s.healthCheckAt) ApplyHealth(s);
            }
        }

        void Track(Creature c)
        {
            NpcState s = null;
            int active = 0; for (int i = 0; i < MaxTracked; i++) if (pool[i].active) active++;
            if (active >= PerfTracked) return;                                         // 0.34: performance mode
            for (int i = 0; i < MaxTracked; i++) if (!pool[i].active) { s = pool[i]; break; }
            if (s == null) return;
            Reset(s);
            Guard("ik", () => ClearOurIk(c, null));                                       // 0.22: a recycled body may carry an old anchor
            s.active = true; s.creature = c;
            s.personality = personalitiesEnabled ? Personality.Pick(UnityEngine.Random.value) : Personality.All[0];
            s.brave = UnityEngine.Random.value < s.personality.bravery;
            float glassChance = s.personality.type == PersonalityType.Nervous ? 0.25f : s.personality.type == PersonalityType.Aggressive ? 0.05f : 0.12f;
            s.glassJaw = false;                                                       // 0.34: nobody drops from one punch
            s.toughness = s.glassJaw ? 0.55f : UnityEngine.Random.Range(0.9f, 1.5f)
                          * (s.personality.type == PersonalityType.Aggressive || s.personality.type == PersonalityType.Confident ? 1.2f : 1f) * npcToughness;
            // 0.16: individual physical profile (no player sliders - every NPC is different)
            s.balanceStability = UnityEngine.Random.Range(0.75f, 1.3f) * s.personality.postureStrength;
            s.painSensitivity = UnityEngine.Random.Range(0.8f, 1.15f) * (s.personality.type == PersonalityType.Nervous ? 1.1f : s.personality.type == PersonalityType.Aggressive ? 0.85f : 1f);   // 0.21: narrower - a poke shouldn't floor anyone
            s.adrenProfile = PickAdrenProfile(s);
            Guard("identity", () => AssignIdentity(s));
            Guard("meshcheck", () => SkinPaint.LogReadable(c));
            Guard("traits", () => RollTraits(s));
            s.fairFighter = (s.brave || s.personality.type == PersonalityType.Confident || s.personality.type == PersonalityType.Aggressive) && UnityEngine.Random.value < 0.35f;
            Guard("vanilla", () => VanillaOnTrack(s));
            Guard("scars", () => ShowOldScars(s));
            Guard("pressure", () => ApplyPersistentZones(s, s.identity));
            Guard("defense", () => DefenseOnTrack(s));
            Guard("prof", () => ProfOnTrack(s));
            Guard("iris", () => s.irisOrig = c.GetColor(Creature.ColorModifier.EyesIris));
            Guard("enhanced", () => MaybeSpawnEnhanced(s));
            Guard("face", () => SkinPaint.FindFace(s));
            s.rnasDirty = true;
            s.trackedAt = Time.time;
            byCreature[c] = s;
            Guard("events.damage", () => c.OnDamageEvent += OnCreatureDamaged);
            Guard("events.contact", () => c.ragdoll.OnContactStartEvent += OnContact);
            Guard("spawn", () => s.spawnTime = c.spawnTime);
            ApplyHealth(s);
            Guard("personality", () => c.locomotion.globalMoveSpeedMultiplier.Add(SpeedHandler, s.personality.moveSpeed));
            s.brawler = IsBrawler(c);
            Guard("grounded", () => GroundBrain(s));
            Guard("eyes", () => { s.scleraOrig = c.GetColor(Creature.ColorModifier.EyesSclera); s.scleraTinted = true; });
            if (tougherLimbs) ToughenLimbs(s, true);
            Log($"tracking {c.name} as {s.personality.type}{(s.brave ? " (brave)" : "")} durability {s.toughness:0.00} balance {s.balanceStability:0.00} pain {s.painSensitivity:0.00} adrenaline {s.adrenProfile}{(s.glassJaw ? " GLASS JAW" : "")}");
        }

        /// The game re-uses NPC bodies: a body that died and came back (new spawn time) is a NEW person - before 0.13 it
        /// kept the old one's broken jaw pose / crushed head / injuries (the "deformed heads" on new spawns).
        static bool Respawned(NpcState s)
        {
            bool r = false;
            Guard("spawn", () => { float t = s.creature.spawnTime; r = s.spawnTime >= 0f && t > s.spawnTime + 0.01f; });
            return r;
        }

        /// Health multiplier on every NPC, applied once their health is set up (retried until it sticks).
        static void ApplyHealth(NpcState s)
        {
            var c = s.creature;
            s.healthCheckAt = Time.time + 1f;
            float npcHealth = s.hpOverride > 0f ? s.hpOverride : RageEngineScript.npcHealth;      // 0.28: mixed waves give each NPC his own HP
            if (npcHealth <= 0f || c.isKilled) { s.healthApplied = true; Guard("hpscale", () => ScaleByHp(s)); return; }
            Guard("health", () =>
            {
                // 0.20: absolute HP (0-5000) - work out the multiplier from the game's own base health
                c.healthModifier.Remove(HealthHandler);
                float baseMax = c.maxHealth;
                if (baseMax <= 1f) return;
                c.healthModifier.Add(HealthHandler, npcHealth / baseMax);
                if (c.maxHealth > 1f) { c.currentHealth = c.maxHealth; s.healthApplied = Mathf.Abs(c.maxHealth - npcHealth) < npcHealth * 0.05f + 1f; }
                Log($"{c.name} health {baseMax:0} -> {c.currentHealth:0}/{c.maxHealth:0} HP");
                if (s.healthApplied) ScaleByHp(s);
            });
        }

        static void Reset(NpcState s)
        {
            Guard("gouged", () => RestoreGouged(s));
            s.stealthKilled = s.punchedThrough = s.neckSnapped = s.paralysed = s.ally = s.castPrevL = s.castPrevR = s.neckGrabL = s.neckGrabR = s.spineShrugged = s.regenClean = s.crawlFlip = s.passedOut = s.knifeOut = s.knifePending = false; s.hasKnife = UnityEngine.Random.value < 0.65f; s.nextKnife49 = 0f; s.occiput = s.gutShots = s.nextVomit = s.vomitUntil = s.nextVomitSpurt = s.chokeBloodSince = s.nextChokeCough = s.convulseUntil = s.nextConvulse = s.hpOverride = 0f; s.hpT = 1f; s.eyeDmg[0] = s.eyeDmg[1] = 0f; s.headSaves = 0; s.saltsT = 0f; ClearKnots(s); s.ribsL = s.ribsR = s.bodyShots = s.faceStage = s.fingersOut = s.ribsOut = 0; s.faceDepth = s.nextShrugLog = s.groundSince = s.nextGroundAct = s.groundSwingUntil = s.faceDmg = s.nextFaceCave = 0f; s.comboPlan = s.comboLast = null; s.nextSpineShrug = 0f; s.neckGrabAt = s.neckSnapUsedAt = s.injectFlashUntil = s.nextRegenStep = s.regenGlowUntil = 0f; s.eyeTintKey = -1f; if (s.fxMark != null) UnityEngine.Object.Destroy(s.fxMark); s.fxMark = null; s.mouthT = s.nextIntegrityTrip = 0f; s.integrity = 100f; System.Array.Clear(s.blackEye, 0, s.blackEye.Length);
            s.impaled49 = false; s.stuckReach49 = 0; s.nextReach49 = 0f; s.nextTaunt49 = s.nextFearVoice49 = 0f; s.forceFall47 = 0f; s.mustFall49 = 0f; s.downSince49 = 0f; s.lowDrop49 = 0f; s.loadoutDone47 = false; s.nextDodge47 = s.nextPickup47 = s.nextShare47 = 0f;
            s.will44 = s.wearOn44 = s.vomitHold44 = s.gutsOut44 = false; s.nextNeckGrab44 = 0f; s.wear44 = 0f; s.wearSpeed44 = 1f; s.nextWear44 = 0f;
            s.hound43 = s.deepSleep43 = false; s.next43 = s.rage43 = s.burnout43 = s.rageMaxAt43 = s.magnetNext43 = s.despUntil43 = s.despNext43 = 0f; s.despPose43 = 0; s.magnetItem43 = null;
            if (s.despL43 != null) UnityEngine.Object.Destroy(s.despL43.gameObject); if (s.despR43 != null) UnityEngine.Object.Destroy(s.despR43.gameObject); s.despL43 = s.despR43 = null;
            s.beatHits42 = 0; s.beatAt42 = s.nextJawHold42 = s.nextBlindSwing42 = 0f; s.blind42 = false; s.wave42 = null;
            s.noseBroken = s.gutImpaled = false; s.teethLost = 0; s.throatCutUntil = s.nextSpurt = 0f; s.throatPart = s.gutPart = null;
            s.balance = 1f; s.fatigue = 0f; s.looseUntil = 0f; s.looseAmount = 0f; s.looseParts = 0; s.limpParts = 0; s.pinOverridden = false;
            s.pain = 0f; s.brokenParts = 0; s.jawBroken = false; s.bleedRate = 0f; s.bleedAccum = 0f; s.bloodInAirway = false; s.nextCoughTime = 0f;
            s.leftArmDead = s.rightArmDead = s.legWounded = false; s.eyeBloodUntil = 0f; s.clutching = false; s.clutchWipe = false; s.nextReclutch = 0f;
            s.chokeTime = 0f; s.knockedOutUntil = 0f; s.nextChokeFx = 0f;
            s.dismembered = s.fleeing = s.lostLeg = s.revivedFromSlice = false; s.nextScream = 0f; s.dismemberTime = -10f; s.adrenalineUsed = s.onAdrenaline = s.finalKill = false; s.adrenalineEnd = 0f;
            s.witnessShocked = false;
            s.lastReactTime = -10f; s.posture = Posture.Steady; s.downSince = 0f; s.stayDown = 0f; s.shakyUntil = 0f; s.shakyDuration = 1f;
            s.nextWrithe = 0f; s.lastSpeedMult = -1f; s.cancelGetUpSet = false; s.braceUntil = s.flailUntil = s.nextReflex = 0f;
            s.reflexLeft = s.reflexRight = false;
            s.scaledParts.Clear(); s.musclesActive = false; s.weakArmLUntil = s.weakArmRUntil = s.nextTremble = s.nextWince = 0f;
            s.headHits = s.bodyHits = s.exchangeStart = s.nextMoodShift = s.nextSlip = s.nextFearPause = s.nextStruggle = 0f; s.hitsTaken = s.hitsLanded = 0; s.grabbedPart = null; s.pryAnchor = false;
            s.cantStand = s.stanceOn = false; s.nextFootwork = s.nextNeckGrab = 0f;
            s.trConfidence = s.trAggression = s.trAwareness = s.trLoyalty = s.trPatience = s.trRisk = 0.5f; s.voicePitch = 1f; s.guardStyle = 0;
            s.heardAt = s.lookUntil = s.habituation = s.lastStartle = s.nextSight = s.seenAt = s.nextNearMiss = s.nextIdle = s.nextIdleLook = s.nextFriendCheck = s.nextSaccade = 0f;
            s.lostSight = s.inCombat = s.surrendered = s.surrenderBroken = s.settled = s.cradling = s.limbPainted = false; s.eyeRest = null;
            s.surrenderSince = s.aloneSince = 0f; s.lastSrcCol = null; s.lastSrcTime = 0f; for (int k = 0; k < 4; k++) s.regionReactUntil[k] = 0f; s.glancing = 0;
            s.stumbleUntil = s.nextStumbleStep = s.nextDazeSway = s.downQuietSince = s.stuckTime = s.lastNavFail = s.navPauseUntil = s.nextSpacing = s.fixerUntil = s.nextConflictLog = s.nextCradle = 0f;
            s.stumbleSteps = s.navFails = s.switchIdx = 0; s.settleOrig?.Clear(); s.faceSmr = null; s.faceShape = -1;
            s.buckleLegs = 0; s.buckleUntil = s.koDeathAt = s.pullDoneAt = s.nextPullTry = s.beckonUntil = s.nextInviteCheck = s.nextFlankStep = s.chargeUntil = s.nextDodge = s.pointAtUntil = s.nextFace = 0f;
            s.bioFx.Clear(); s.bioDirty = s.bioActive = s.bioNoQuit = s.bioSetCantStand = false; s.bioSealed = 0; s.bioIndicatorComp = s.bioXVisual = -1; s.bioVisualObjs.Clear(); s.bioIndicator = null; s.bioSite = null;
            s.bioLastTick = s.bioBaseHeight = s.bioNextPaint = s.bioNextHalluc = s.bioLastStandUntil = s.bioNextRestore = s.bioAllyBoostUntil = 0f; s.bioTough = s.bioPain = s.bioBalance = s.bioReact = s.bioStrength = s.bioFlinch = s.bioSpeed = s.bioDmg = s.bioHeight = 1f;
            s.tox = -1; s.toxStage = s.toxFlags = 0; s.toxDose = s.toxStart = s.toxNext = s.hpK = s.heldUntil = s.grabY = 0f; s.hpScaled = false; s.toxPart = null;
            s.bruiseSpots.Clear(); s.woundSpots.Clear(); s.swellCount = 0; s.nextGroundBlood = 0f; s.skull = SkullIntegrity; s.followUntil = 0f; s.nextDefense = 0f; s.pool = null; s.defOrig = null; s.defPatched = false;
            ClearDrunk(s); ClearPressure(s); s.koWakeAt = 0f; s.fistsAt = -100f; s.koLimbs = 0;
            s.knockedOut = s.announcedFists = s.invited = s.fairFighter = s.waiting = s.shocked = s.disarmPatched = false; s.pullHand = null; s.lastFace = FaceAnimator.Expression.Neutral; s.scarMask = 0;
            s.ownWeapon = null; s.identity = null; s.diedFinal = false;
            s.punchPhase = s.comboLeft = s.blocks = s.looseL = s.looseR = 0; s.punchT = s.nextPunch = s.punchTempo = s.blockUntil = 0f; s.punchLanded = false;
            s.jawAxis = Vector3.zero; s.jawOpenCur = s.lastShank = 0f; s.shanks = 0;
            s.ribsBroken = 0; s.nextRibWince = s.nextWinded = s.ribHesitateAt = s.ribGuardAt = 0f; s.ribPart = null; s.shock = 0f; s.shockDeathAt = 0f;
            s.jawTorn = false; s.jawSwing = s.jawSwingVel = 0f; s.nextHitSfx = 0f;
            s.deathTime = 0f; s.daze = 0f; s.physUntil = 0f; s.lastSlamTime = -10f; s.nextTear = 0f; s.nextEyeWipe = 0f;
            s.physForced = s.spineBroken = s.headCrushed = s.headPopped49 = s.noGasp = s.brawler = false; s.craters = 0;
            s.coreUntil = 0f; s.spawnTime = -1f; s.meleeOffUntil = 0f; s.healthCheckAt = 0f; s.healthApplied = false; s.meleeOff = false; s.bakedAt = -10f;
            s.lastHitTime = -10f; s.nextStagger = 0f; s.allowFallUntil = 0f; s.lastGrabTime = -10f; s.phantomAskedAt = -10f; s.phantom = null; s.lastGrabbed = null; s.phantomPending = false; s.prevState = Ragdoll.State.Standing; s.blockedFalls = 0; s.blockWindowStart = 0f;
            s.headMesh = null; s.headPopped49 = false; s.scleraTinted = false; s.stressLA = s.stressRA = s.stressLL = s.stressRL = 0f;
            s.unarmedSince = s.nextFistThink = s.nextThrow = s.nextGuard = s.guardUntil = s.rearmUntil = s.throwAt = 0f; s.throwItem = null; s.throwing = false;
            for (int i = 0; i < s.muscles.Length; i++) s.muscles[i] = 0f;
            s.musclesDirty = s.gripBlockL = s.gripBlockR = s.eyesWereShut = false; s.holdFatigueL = s.holdFatigueR = s.agonyUntil = s.eyesShutUntil = 0f; s.blindEyes = 0;
            s.nextDirected = s.clutchAt = s.impaleSince = s.nextThrash = s.nextImpaleScream = s.rushUntil = s.airTime = 0f;
            s.clutchAtPart = s.impalePart = null; s.clutchAtBoth = s.rushUsed = s.clutching2 = false; s.impaleItem = null;
            ClearTags(s);
            s.rnasDirty = s.blindMoving = s.pinned = false; s.rnasNext = s.behaviorSince = s.retrieveUntil = s.retrieveFirstTry = s.badWeaponUntil = s.blindMoveFlip = s.pinCheckAt = s.clipTime = 0f;
            s.behavior = Behavior.Normal; s.retrieveTries = 0; s.retrieveTarget = s.badWeapon = null; s.impaleCols = null;
            s.noseBleedUntil = s.nextNoseDrip = 0f; s.blackEyes = 0; s.bruisesPainted = 0;
            s.adrenProfile = AdrenProfile.None; s.balanceStability = s.painSensitivity = 1f;
            s.toughness = 1f; s.glassJaw = false; s.dying = s.dyingUsed = false; s.dyingStart = s.dyingEnd = 0f; s.lastHitDir = Vector3.zero;
        }

        static readonly object HealthHandler = new object();
        static readonly object SpeedHandler = new object(), InjuryHandler = new object(), WoundHandler = new object(), PinHandler = new object();

        void Release(NpcState s)
        {
            var c = s.creature;
            Guard("identity", () => CloseIdentity(s));
            if (c != null)
            {
                Guard("events.damage", () => c.OnDamageEvent -= OnCreatureDamaged);
                Guard("events.contact", () => c.ragdoll.OnContactStartEvent -= OnContact);
                Guard("health", () => c.healthModifier.Remove(HealthHandler));
                Guard("posture", () => { ReleaseReflexes(s); SetCancelGetUp(s, false); });
                Guard("personality", () =>
                {
                    c.locomotion.globalMoveSpeedMultiplier.Remove(PostureHandler);
                    c.locomotion.globalMoveSpeedMultiplier.Remove(SpeedHandler);
                    c.locomotion.globalMoveSpeedMultiplier.Remove(InjuryHandler);
                    c.locomotion.globalMoveSpeedMultiplier.Remove(WoundHandler);
                    c.locomotion.globalMoveSpeedMultiplier.Remove(RibHandler);
                    c.locomotion.globalMoveSpeedMultiplier.Remove(DrunkHandler);
                    c.locomotion.globalMoveSpeedMultiplier.Remove(PPHandler);
                    c.locomotion.globalMoveSpeedMultiplier.Remove(ToxHandler);
                    c.locomotion.globalMoveSpeedMultiplier.Remove(BioHandler);
                    c.locomotion.globalMoveSpeedMultiplier.Remove(RnasMoveHandler);
                    c.locomotion.globalMoveSpeedMultiplier.Remove(RushHandler);
                    c.locomotion.allowMove = true;
                });
                Guard("wounds", () => ClearWounds(s));
                Guard("musculature", () => ClearMusculature(s));
                if (s.physForced) SetForcePhysic(s, false);
                Guard("fists", () => { if (s.punchPhase != 0) EndPunch(s, true); ReleaseGuard(s); DropPhantom(s); });
                Guard("ik", () => ClearOurIk(c, s));
                Guard("defense", () => DefenseOnRelease(s));
                Guard("prof", () => ProfOnRelease(s));
                Guard("bio", () => ClearBio(s));
                Guard("lab", () => ClearLab(s));
                Guard("eyetint", () => { if (s.eyeTintKey > -1.5f && s.eyeTintKey != -1f) c.SetColor(s.irisOrig, Creature.ColorModifier.EyesIris, true); s.eyeTintKey = -1f; if (s.fxMark != null) UnityEngine.Object.Destroy(s.fxMark); s.fxMark = null; });
                Guard("eyes-rest", () => RestoreEyes(s));
                Guard("vanilla", () => VanillaOnRelease(s));
                Guard("face", () => { if (s.faceSmr != null && s.faceShape >= 0) s.faceSmr.SetBlendShapeWeight(s.faceShape, 0f); });
                Guard("settle", () => Unsettle(s));
                Guard("surrender", () => { if (s.surrendered || s.behavior == Behavior.Surrender) ExitSurrender(s); });
                Guard("pry", () => { if (s.pryAnchor && c.ragdoll.ik != null) { c.ragdoll.ik.SetHandAnchor(s.prySide, null); c.ragdoll.ik.SetHandState(s.prySide, false, false); } s.pryAnchor = false; });
                if (s.meleeOff) SetMelee(s, true, 0f);
                ForgetSkin(c);
                RestoreBody(s);
                ToughenLimbs(s, false);
                Guard("muscles", () => RestoreMuscles(s));
                byCreature.Remove(c);
            }
            foreach (var go in s.boneMeshes) if (go != null) UnityEngine.Object.Destroy(go);
            foreach (var b in s.bruises) if (b.go != null) UnityEngine.Object.Destroy(b.go);
            s.bruises.Clear();
            if (s.guardL != null) UnityEngine.Object.Destroy(s.guardL.gameObject);
            if (s.guardR != null) UnityEngine.Object.Destroy(s.guardR.gameObject);
            s.guardL = s.guardR = null;
            if (s.reflexAnchorL != null) UnityEngine.Object.Destroy(s.reflexAnchorL.gameObject);
            if (s.reflexAnchorR != null) UnityEngine.Object.Destroy(s.reflexAnchorR.gameObject);
            s.reflexAnchorL = s.reflexAnchorR = null;
            s.boneMeshes.Clear();
            s.active = false; s.creature = null;
        }

        void ReleaseAll()
        {
            for (int i = 0; i < MaxTracked; i++) if (pool[i] != null && pool[i].active) Release(pool[i]);
            byCreature.Clear();
        }

        internal bool TryGet(Creature c, out NpcState s) { s = null; return c != null && byCreature.TryGetValue(c, out s); }

        // ---------------- events ----------------
        void OnContact(CollisionInstance hit, RagdollPart part)
        {
            if (!modEnabled || OrgansOnly || hit == null || part == null || part.ragdoll == null) return;
            Guard("contact", () => HandleContact(hit, part));
        }

        void OnCreatureDamaged(CollisionInstance hit, EventTime time)
        {
            if (!modEnabled || time != EventTime.OnEnd || hit == null) return;
            if (OrgansOnly) { Guard("organs", () => OrganOnlyHit47(hit)); return; }
            Guard("hit", () => HandleHit(hit));
        }

        void HandleHit(CollisionInstance hit)
        {
            var part = hit.damageStruct.hitRagdollPart;
            if (part == null || part.ragdoll == null) return;
            var c = part.ragdoll.creature;
            lastHandled = hit;
            ClassifyHit34(hit);
            if (!TryGet(c, out var s) || s.knockedOutUntil > 0f) return;
            { bool ig = false; Guard("ignore47", () => ig = Ignore47(s, hit)); if (ig) return; }
            var t = part.type;
            float severity = HitSeverity(hit);
            { float sv = severity; Guard("toughen", () => sv = Toughen(s, hit, sv)); severity = sv; }
            Guard("reactions", () => ApplyImpactReaction(s, hit, part, t, severity));
            Guard("injuries", () => ApplyInjuries(s, hit, part, t, severity));
            Guard("pressure", () => PressureHit(s, part, hit.contactPoint, hit.damageStruct.damageType, severity));
            Guard("woundpaint", () => PaintWound(s, hit, part, severity));
            Guard("toxin", () => ToxinHit(s, hit, part));
            Guard("device", () => DeviceHit(s, hit, part));
            Guard("gore25", () => Gore25Hit(s, hit, part, t, severity));
            Guard("gore42", () => Gore42Hit(s, hit, part, t, severity));
            Guard("bio43", () => Bio43Hit(s, hit, part, t, severity));
            Guard("combat25", () => Combat25Hit(s, hit, part, t, severity));
            if (!c.isKilled) Guard("follow", () => StartFollow(s, severity));
            Guard("wounds", () => ApplyWounds(s, hit, part, t));
            Guard("director", () => DirectReaction(s, hit, part, t, severity));
            s.rnasDirty = true;
            NoteHit(s, t);
            if (hit.damageStruct.damageType != DamageType.Blunt || severity > 1.2f) s.scarMask |= (int)t;
            Guard("allies", () => AlliesSeeHurt(s, severity));
            if (s.surrendered) Guard("surrender", () => SurrenderBetrayed(s));
            Guard("agony", () => AgonyCheck(s, severity));
            Guard("sound", () => HitSound(s, hit, part));
            if (hit.damageStruct.damageType == DamageType.Blunt) Guard("drunk", () => AddDrunk(s, t, severity));
            if (hit.damageStruct.damageType == DamageType.Blunt) Guard("bruises", () => AddBruise(s, part, hit.contactPoint, hit.contactNormal, severity));
            if (hit.damageStruct.damageType == DamageType.Blunt && Has(t, RagdollPart.Type.Head)) Guard("face", () => FaceInjury(s, part, hit.contactPoint, severity));
            Guard("tool27", () => ToolHit27(s, hit, part, severity));
            if (hit.damageStruct.damageType == DamageType.Blunt) Guard("vitals28", () => Vitals28(s, hit, part, severity));
            if (hit.damageStruct.damageType == DamageType.Blunt) { Guard("swell27", () => Swell27(s, part, hit.contactPoint, severity)); Guard("passout", () => MaybePassOut(s, hit, severity)); }
            if (hit.damageStruct.damageType == DamageType.Blunt && Has(t, RagdollPart.Type.Head)) Guard("facecave", () => FaceCave(s, part, hit.contactPoint, severity, hit));
            if (hit.damageStruct.damageType == DamageType.Blunt && Has(t, RagdollPart.Type.Neck)) Guard("neck", () => NeckBlow(s, part, hit.contactPoint, severity));
            Guard("trauma", () => OnHurt(s, severity));
            // a swung blunt weapon hard enough tears flesh (held-weapon momentum is ignored on contact - this is the path)
            float bdmg = hit.damageStruct.damage * BluntGoreScale(s);
            if (hit.damageStruct.damageType == DamageType.Blunt && Has(t, RagdollPart.Type.Head) && (bdmg < bluntGoreDamage || !fleshTearing))
            {
                Vector3 sdir = hit.impactVelocity.sqrMagnitude > 0.1f ? hit.impactVelocity.normalized : -hit.contactNormal;
                Guard("skull", () => SkullHit(s, hit, part, bdmg, sdir));
            }
            if (hit.damageStruct.damageType == DamageType.Blunt && bdmg >= bluntGoreDamage && fleshTearing)
                Guard("gore", () =>
                {
                    Vector3 dir = hit.impactVelocity.sqrMagnitude > 0.1f ? hit.impactVelocity.normalized : -hit.contactNormal;
                    if (Has(t, RagdollPart.Type.Head) && SkullHit(s, hit, part, bdmg, dir)) return;          // 0.22: skull integrity
                    TearFlesh(s, part, hit.contactPoint, dir, fleshTearForce * bdmg / bluntGoreDamage);
                });
            Log($"{c.name} hit {hit.damageStruct.damageType} part={t} dmg={hit.damageStruct.damage:0.0} push={hit.damageStruct.pushLevel} vel={hit.impactVelocity.magnitude:0.0} sev={severity:0.00} bal={s.balance:0.00}");
        }

        /// 0 = nothing .. ~1 a solid hit .. 3+ massive. Combines push level, damage and impact speed, so thrown objects count.
        static float HitSeverity(CollisionInstance hit)
        {
            float push = hit.damageStruct.pushLevel;
            float dmg = hit.damageStruct.damage;
            float vel = hit.impactVelocity.magnitude;
            return Mathf.Max(push * 0.8f, dmg / 15f, (vel - 1.5f) / 4f, 0.15f);
        }

        void OnCreatureKill(Creature victim, Player player, CollisionInstance hit, EventTime time)
        {
            if (!modEnabled || OrgansOnly || victim == null || victim.isPlayer) return;
            if (time == EventTime.OnEnd) Guard("death", () => HandleKill(victim, hit));
        }

        void HandleKill(Creature victim, CollisionInstance hit)
        {
            TryGet(victim, out var dead);
            if (dead != null && !dead.dying) { bool turned = false; Guard("rules27", () => turned = DeathRules27(dead, victim, hit)); if (turned) return; }
            if (dead != null && dead.dying) { dead.dying = false; dead.finalKill = true; }            // second blow while dying: down now
            else if (dead != null && !dead.finalKill && !Tank(dead) && !HeavyPlayerBlow(hit) && !DeathsHandledElsewhere && !HeadOrNeckBlow(victim, hit)
                     && (TryAdrenaline(dead, hit) || TryDyingMoments(dead, hit))) return;

            if (dead != null)
            {
                // death voice comes from the game's own voice acting; add a body-fall thud
                var root = victim.ragdoll != null && victim.ragdoll.rootPart != null ? victim.ragdoll.rootPart.transform : victim.transform;
                Sfx.Play("NRE_BodyFall", root, 0.7f);
                Guard("muscles", () => RestoreMuscles(dead));
                Guard("wounds", () => ReleaseClutch(dead));
                dead.deathTime = Time.time;
            }
            Guard("death", () => OnFinalDeath(victim, dead));
            Guard("stim", () => Emit(victim.transform.position + Vector3.up, 10f, Stim.Death, victim));
            if (awarenessEnabled) Guard("awareness", () => Witness(victim));
        }

        /// Head or neck killing blow (decapitation, skull crush, throat): always instant, no revives.
        static bool HeadOrNeckBlow(Creature c, CollisionInstance hit)
        {
            var part = hit != null ? hit.damageStruct.hitRagdollPart : null;
            if (part != null && Has(part.type, RagdollPart.Type.Head | RagdollPart.Type.Neck)) return true;
            var rag = c.ragdoll;
            if (rag != null && rag.parts != null)
                foreach (var p in rag.parts) if (p != null && p.isSliced && Has(p.type, RagdollPart.Type.Head | RagdollPart.Type.Neck)) return true;
            return false;
        }

        void Witness(Creature victim)
        {
            int faction = victim.factionId;
            Vector3 pos = victim.transform.position;
            for (int i = 0; i < MaxTracked; i++)
            {
                var s = pool[i];
                if (!s.active || s.creature == null || s.creature == victim || s.creature.isKilled || s.creature.factionId != faction) continue;
                if ((s.creature.transform.position - pos).sqrMagnitude > WitnessRadius * WitnessRadius) continue;
                // fighters, not cowards: seeing an ally die doesn't rock them physically
                // fighters, not cowards: no panic from seeing an ally die
            }
        }

        void OnLevelUnload(LevelData data, LevelData.Mode mode, EventTime time)
        {
            if (time == EventTime.OnStart) { Guard("unload", ReleaseAll); Guard("roster", () => SaveRosterIfDirty(true)); }
        }

        // ---------------- shared helpers ----------------
        internal static Transform Mouth(Creature c)
        {
            if (c == null) return null;
            if (c.jaw != null) return c.jaw;
            var head = c.ragdoll != null ? c.ragdoll.headPart : null;
            return head != null ? head.transform : c.transform;
        }

        internal static RagdollHand Hand(Creature c, Side side) => side == Side.Left ? c.handLeft : c.handRight;
        internal static Side Other(Side s) => s == Side.Left ? Side.Right : Side.Left;

        internal const RagdollPart.Type LeftArmParts = RagdollPart.Type.LeftArm | RagdollPart.Type.LeftHand;
        internal const RagdollPart.Type RightArmParts = RagdollPart.Type.RightArm | RagdollPart.Type.RightHand;
        internal const RagdollPart.Type LeftLegParts = RagdollPart.Type.LeftLeg | RagdollPart.Type.LeftFoot;
        internal const RagdollPart.Type RightLegParts = RagdollPart.Type.RightLeg | RagdollPart.Type.RightFoot;
        internal const RagdollPart.Type ArmParts = LeftArmParts | RightArmParts;
        internal const RagdollPart.Type LegParts = LeftLegParts | RightLegParts;
        internal const RagdollPart.Type UpperBody = RagdollPart.Type.Head | RagdollPart.Type.Neck | RagdollPart.Type.Torso | ArmParts;
        internal const RagdollPart.Type WholeBody = UpperBody | LegParts;

        internal static bool Has(RagdollPart.Type mask, RagdollPart.Type bits) => (mask & bits) != 0;

        internal static RagdollPart.Type LimbOf(RagdollPart.Type t)
        {
            if (Has(t, LeftArmParts)) return LeftArmParts;
            if (Has(t, RightArmParts)) return RightArmParts;
            if (Has(t, LeftLegParts)) return LeftLegParts;
            if (Has(t, RightLegParts)) return RightLegParts;
            return 0;
        }
    }
}
