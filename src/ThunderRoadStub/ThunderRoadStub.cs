// COMPILE-ONLY stand-in for the game's ThunderRoad.dll (written by hand for Nomad Rage Engine; contains no game code).
// It declares only the members Rage Engine uses, with the exact names, signatures, field/property/event kinds and enum
// values of the real game API, so the compiled RageEngine.dll binds to the real ThunderRoad.dll at runtime.
// NEVER ship the ThunderRoad.dll built from this file - the game provides the real one.
#pragma warning disable 0067, 0649, 0414
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThunderRoad
{
    public enum Side { Right = 0, Left = 1 }
    public enum EventTime { OnStart = 0, OnEnd = 1 }
    public enum DamageType { Unknown = 0, Pierce = 1, Slash = 2, Blunt = 3, Energy = 4, Fire = 5, Lightning = 6, UnBlockable = 7 }

    public class ThunderBehaviour : MonoBehaviour { }
    public class OutOfBoundsFader : ThunderBehaviour { public Collider boundsCollider; public bool constrainPlayer; }
    public abstract class ThunderEntity : ThunderBehaviour { }
    public class Interactable : ThunderBehaviour { public enum Action { UseStart = 0, UseStop = 1, AlternateUseStart = 2, AlternateUseStop = 3, Grab = 4, Ungrab = 5 } }
    public enum Category { Text = 0, Wave = 1, Creature = 2, CreatureTable = 3, LootTable = 4, Container = 5, Item = 6, Brain = 7, Effect = 8 }
    public abstract class ContentCustomData { }
    public class InteractableData : CatalogData { }
    public class HandleData : InteractableData { public bool allowSteal; }
    public class Handle : Interactable { public Item item; public PhysicBody physicBody; public HandleData data; public Vector3 GetDefaultAxisPosition(Side side) => default; }
    public class FaceAnimator : ThunderBehaviour { public enum Expression { Angry = 0, Attack = 1, Confusion = 2, Death = 3, Fear = 4, Happy = 5, Intrigue = 6, Neutral = 7, Pain = 8, Sad = 9, Surprise = 10, Tired = 11 } }
    public class TimeManager : ThunderBehaviour
    {
        public enum SlowMotionState { Disabled = 0, Starting = 1, Running = 2, Stopping = 3 }
        public static SlowMotionState slowMotionState { get; private set; }
        public static void SetSlowMotion(bool active, float scale, AnimationCurve curve, EffectData effectData = null, bool snapshotTransition = true, bool playEndEffect = true) { }
        public static void StopSlowMotion() { }
    }
    public class HandleRagdoll : Handle { public RagdollPart ragdollPart; }
    public class ColliderGroup : ThunderBehaviour { }
    public class Holder : Interactable { }
    public class PlayerHead : MonoBehaviour { public Camera cam; }
    public class Player : ThunderBehaviour { public static bool fallDamage, invincibility; public static Player local; public PlayerHead head; public Creature creature; public Locomotion locomotion; public void Teleport(Vector3 position, Quaternion rotation, bool keepVelocity = false, bool useHeadPosition = true) { } }

    public class CatalogData { public string id; }
    public class SkillData : CatalogData { public string shardId; }   // Boxer Crystal skill tree
    public class EntityData : CatalogData { }
    public class CreatureData : EntityData
    {
        public string brainId; public int factionId;
        public void SpawnAsync(Vector3 position, float rotationY, Transform parent = null, bool pooled = true, WayPoint[] waypoints = null, Action<Creature> callback = null) { }
    }
    public class WayPoint : MonoBehaviour { }
    public class CreatureTable : CatalogData
    {
        public class Drop { public string referenceID; public bool overrideFaction; public int factionID; }
        public List<Drop> drops;
        public bool TryPick(ref CreatureData creatureData, System.Random randomGen = null) => false;
    }
    public class ItemModule { }
    public class ItemModuleAI : ItemModule
    {
        public enum WeaponClass { None, Melee, Bow, Arrow, Shield, Wand, Crossbow, Bolt, Firearm, Throwable }
        public enum WeaponHandling { None, OneHanded, TwoHanded }
        public WeaponClass primaryClass, secondaryClass; public WeaponHandling weaponHandling;
    }
    public class RevealDecal : MonoBehaviour { public void Reset() { } }
    public class ItemData : EntityData
    {
        public enum Type { Misc = 0, Weapon = 1, Quiver = 2, Potion = 3, Prop = 4, Body = 5, Shield = 6, Wardrobe = 7, Spell = 8, Crystal = 9, Valuable = 10, Tool = 11, Food = 12, Note = 13 }
        public string displayName;
        public Type type;
        public string slot, category; public int tier;
        public ItemModuleAI moduleAI;
        public void SpawnAsync(Action<Item> callback, Vector3? position = null, Quaternion? rotation = null, Transform parent = null, bool pooled = true, List<ContentCustomData> customDataList = null, Item.Owner owner = Item.Owner.None) { }
    }
    public class LevelData : CatalogData { public class Mode { } public string name; }
    public class Module
    {
        protected Module() { }
        public virtual System.Collections.IEnumerator OnLoadCoroutine() { yield break; }
        public virtual void Update() { }
        public virtual void OnUnload() { }
    }
    public class LevelModule : Module
    {
        public Level level;
        public virtual System.Collections.IEnumerator OnPlayerSpawnCoroutine() { yield break; }
    }
    public class Level : MonoBehaviour { public static Level current; public static Level master; public LevelData data; }
    public static class LevelManager { public static string GetCurrentLevel() => null; }
    public class WaveSpawner : MonoBehaviour
    {
        public class SpawnPoint { public Transform transform; public SpawnPoint(Transform transform, Area area = null) { } }
        public List<Transform> spawns; public List<SpawnPoint> spawnPoints; public bool ignoreDungeonLimits; public bool isRunning;
        public static List<WaveSpawner> instances;
        public List<Creature> spawnedCreatures;
        public Dictionary<WaveData.Group, int> groupAliveCounts;
        public WaveData currentLoadedData;
    }
    public class WaveData : CatalogData
    {
        public class Group { }
        public class WaveFaction { public int factionMaxAlive; }
        public int totalMaxAlive;
        public List<WaveFaction> factions;
    }
    public class Area : MonoBehaviour { }
    public class UIWaveSpawner : MonoBehaviour { public WaveSpawner waveSpawner; }
    public class EffectData : CatalogData
    {
        public EffectInstance Spawn(Transform parent, bool pooled = true, ColliderGroup colliderGroup = null, bool fromPlayer = false) => null;
    }
    public class EffectInstance { public void Play(int stepCustomHashId = 0, bool skipStarts = false, bool skipLoops = false) { } }
    public enum MessageAnchorType { }
    public class DisplayMessage : MonoBehaviour
    {
        public static DisplayMessage instance;
        public class MessageData
        {
            public string text; public int priority; public bool dismissAutomatically; public float dismissTime; public bool isSkippable;
            public MessageData(string text, int priority, float showDelay = 0f, Texture image = null, UnityEngine.Video.VideoClip videoClip = null, bool isTutorialMessage = false, bool isSkippable = true, bool fitVideoHorizontally = false, bool warnPlayer = false, MessageAnchorType anchorType = default, Transform anchorTargetTransform = null, bool dismissAutomatically = false, float dismissTime = 0f, UnityEngine.Events.UnityEvent<MessageData> onMessageSkip = null, bool releaseAssetsAtTheEnd = false, object handler = null) { }
        }
        public void ShowMessage(MessageData messageData) { }
    }
    public class GameData
    {
        public class Faction { public enum AttackBehaviour { Default, Ignored, Passive, Agressive } public int id; public string name; public AttackBehaviour attackBehaviour; }
        public List<Faction> factions;
    }
    public static class Catalog
    {
        public static GameData gameData;
        public static T GetData<T>(string id, bool logWarning = true) where T : CatalogData => default;
        public static List<CatalogData> GetDataList(Category category) => null;
    }

    public abstract class ValueHandler<T>
    {
        public bool Add(object handler, T value) => false;
        public bool Remove(object handler) => false;
    }
    public class FloatHandler : ValueHandler<float> { }
    public class BoolHandler : ValueHandler<bool> { public BoolHandler(bool defaultValue) { } public void Add(object handler) { } }
    public class CreatureEye : MonoBehaviour { public float closeAmount; public string eyeTag; public void SetClose() { } }

    public class PhysicBody
    { public void AddTorque(Vector3 torque, ForceMode mode) { }
        public Rigidbody rigidBody;
        public Vector3 velocity { get => default; set { } }
        public Vector3 angularVelocity { get => default; set { } }
        public float mass { get => 1f; set { } }
        public void AddForce(Vector3 force, ForceMode mode) { }
        public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode) { }
    }

    public struct DamageStruct
    {
        public enum Penetration { None = 0, Pressure = 1, Hit = 2, Skewer = 3 }
        public DamageType damageType;
        public float damage;
        public int pushLevel;
        public RagdollPart hitRagdollPart;
        public Penetration penetration;
        public bool penetrationDeepReached;
    }

    public class CollisionInstance
    {
        public Collider sourceCollider;
        public Vector3 impactVelocity;
        public Vector3 contactPoint;
        public Vector3 contactNormal;
        public DamageStruct damageStruct;
        public MaterialData sourceMaterial, targetMaterial;
        public float intensity;
    }

    public class RagdollPart : ThunderBehaviour
    {
        [Flags]
        public enum Type
        {
            Head = 1, Neck = 2, Torso = 4, LeftArm = 8, RightArm = 16, LeftHand = 32, RightHand = 64,
            LeftLeg = 128, RightLeg = 256, LeftFoot = 512, RightFoot = 1024, LeftWing = 2048, RightWing = 4096, Tail = 8192
        }
        public Type type;
        public RagdollPart parentPart;
        public PhysicBody physicBody;
        public Ragdoll ragdoll;
        public bool isSliced;
        public CharacterJoint characterJoint;
        public Ragdoll.Bone bone;
        public Transform meshBone;
        public float breakForceMultiplier;
    }

    public class RagdollHand : RagdollPart
    {
        public class Finger { public Transform tip; }
        public List<Finger> fingers;
        public Side side;
        public Creature creature;
        public Handle grabbedHandle;
        public PlayerHand playerHand;
        public Transform grip;
        public Vector3 PalmDir => default; public Vector3 PointDir => default; public Vector3 ThumbDir => default;
        public void UnGrab(bool throwing) { }
        public void Grab(Handle handle) { }
        public void Grab(Handle handle, bool teleportToHand, bool withTrigger = false) { }
        public void SetBlockGrab(bool active, bool blockTK = true) { }
    }

    public class IkController : ThunderBehaviour
    {
        public Transform handLeftTarget, handRightTarget;
        public void SetHandAnchor(Side side, Transform anchor) { }
        public void SetHandState(Side side, bool positionEnabled, bool rotationEnabled) { }
        public void SetShoulderAnchor(Side side, Transform anchor) { }
        public void SetShoulderState(Side side, bool positionEnabled, bool rotationEnabled) { }
        public void SetShoulderWeight(Side side, float positionWeight, float rotationWeight) { }
    }

    public class Ragdoll : ThunderBehaviour
    {
        public enum State { Inert = 0, Destabilized = 1, Frozen = 2, Standing = 3, Kinematic = 4, NoPhysic = 5, Disabled = 6 }
        public class Region { }
        public class Bone { public Transform mesh; public ConfigurableJoint animationJoint; public RagdollPart part; }
        public List<RagdollPart> parts;
        public RagdollPart headPart;
        public RagdollPart rootPart;
        public Creature creature;
        public State state;
        public IkController ik;
        public List<RagdollHand> handlers;
        public float preStandUpDuration;
        public bool standingUp;
        public bool physicToggle;
        public BoolHandler forcePhysic;
        public delegate void ContactEvent(CollisionInstance collisionInstance, RagdollPart ragdollPart);
        public event ContactEvent OnContactStartEvent;
        public void SetState(State newState) { }
        public void CancelGetUp(bool resetState = true) { }
        public void SetPinForceMultiplier(float springMultiplier, float damperMultiplier, float posMaxForceMult, float rotMaxForceMult,
            bool jointLimits = true, bool noRoot = false, RagdollPart.Type partTypes = (RagdollPart.Type)0, Region affectedRegion = null) { }
        public void ResetPinForce(bool jointLimits = true, bool noRoot = false, RagdollPart.Type partTypes = (RagdollPart.Type)0) { }
    }

    public class Locomotion : ThunderBehaviour { public LayerMask groundMask; public CapsuleCollider capsuleCollider; public FloatHandler globalMoveSpeedMultiplier; public bool isGrounded; public bool allowMove; public PhysicBody physicBody; public Vector3 moveDirection; }
    public class CollisionHandler : ThunderBehaviour
    {
        public List<CollisionHandler> penetratedObjects; public RagdollPart ragdollPart; public Item item;
        public delegate void CollisionEvent(CollisionInstance collisionInstance);
        public event CollisionEvent OnCollisionStartEvent;
    }
    public class MaterialData : CatalogData { }

    public class BrainData : CatalogData
    {
        public abstract class Module { }
        public bool isActive;
        public T GetModule<T>(bool autoAddModuleIfMissing = true) where T : Module => default;
    }
    public class PlayerHand : ThunderBehaviour { public PlayerControl.Hand controlHand; public RagdollHand ragdollHand; }
    public class Brain : ThunderBehaviour
    {
        public enum State { Idle = 0, Follow = 1, Patrol = 2, Investigate = 3, Alert = 4, Combat = 5, Grappled = 6, Custom = 7 }
        public State state;
        public BrainData instance;
        public void Load(string brainId) { }
        public void Stop() { }
    }
    public class BrainModuleSpeak : BrainData.Module
    {
        public bool moveMouth;
        public void Play(string dialogId, bool abnormalNoise, bool checkMuffled = true, int forceHash = -1) { }
    }
    public class BrainModuleStance : BrainData.Module
    {
        public enum Stance { Idle = 0, Fists = 1, MeleeShield = 2, Melee1H = 3, Melee2H = 4, DualWield = 5, Bow = 6, Flee = 7, Staff = 8, PoleWeapon = 9 }
        public Stance stance;
        public void SetStance(Stance stance) { }
    }
    public class BrainModuleRanged : BrainData.Module { }
    public class BrainModuleAttack : BrainData.Module { }
    public class BrainModuleMelee : BrainModuleAttack { public bool meleeEnabled; public float animationSpeedMultiplier, lastAttackStartTime, lastAttackEndTime, nextAttackDelay; }
    public class Imbue : ThunderBehaviour { public static bool infiniteImbue; }
    public class RagdollHandClimb : ThunderBehaviour { public static bool climbFree; }
    public enum LayerName { }
    public class GameManager : ThunderBehaviour
    {
        public static void SetFreeClimb(bool active) { } public static void SetPlayerFallDamage(bool active) { } public static void SetInfiniteFocus(bool active) { }
        public static void SetInfiniteImbue(bool active) { } public static void SetPlayerInvincibility(bool active) { }
    }
    public class Mana : ThunderBehaviour { public static bool infiniteFocus; public float currentFocus { get; set; } public float MaxFocus => 0f; }
    public class BrainModuleThrow : BrainModuleRanged
    {
        public void StartThrow(Transform target, bool grabRock, ThunderRoad.AI.Action.AttackThrow node = null, float aimDelay = -1f) { }
    }
    public class BrainModuleDefense : BrainData.Module { public bool parryEnabled; public float aboutToBeHitTiming, parryDetectionRadius, positionSpeed, rotationSpeed, parryAdjustSpeed, armSpringMultiplier; }
    public class BrainModuleEquipment : BrainData.Module { public bool allowArmGrabDisarm; public bool allowArmStabDisarm; }
    public class BrainModuleElectrocute : BrainData.Module { public Coroutine coroutine; public float lastElectrocuteTime; }
    public class BrainModuleExpression : BrainData.Module { public bool SetFaceExpression(FaceAnimator.Expression expression, int faceIndex = 0, float? neutralExpressionTime = null) => false; }
    public class BrainModuleFear : BrainData.Module { public void Panic(float duration = -1f) { } }
    public class BrainModuleHitReaction : BrainData.Module
    {
        public float recoverySpeed;
        public bool cancelGetUp;
        public void TestStun(float duration) { }
        public class PushBehaviour
        {
            public enum Effect { None = 0, StaggerLight = 1, StaggerMedium = 2, StaggerFull = 3, Destabilize = 4 }
            public int level;
            public List<RagdollPart.Type> onlyForBodyParts;
            public Effect duringIdle, duringCombat, duringStagger, duringStaggerFull, duringAttack, duringAttackJump;
        }
        public List<PushBehaviour> pushHitBehaviors;
        public List<PushBehaviour> pushGrabThrowBehaviors;
    }

    public class Item : ThunderEntity
    {
        public enum Owner { None = 0, Player = 1, Shopkeeper = 2 }
        public delegate void HeldActionDelegate(RagdollHand ragdollHand, Handle handle, Interactable.Action action);
        public delegate void GrabDelegate(Handle handle, RagdollHand ragdollHand);
        public event GrabDelegate OnGrabEvent;
        public RagdollHand mainHandler;
        public Transform flyDirRef;
        public List<CollisionHandler> collisionHandlers;
        public CollisionHandler mainCollisionHandler;
        public bool isPenetrating;
        public PhysicBody physicBody;
        public ItemData data;
        public event HeldActionDelegate OnHeldActionEvent;
        public static List<Item> allActive;
        public RagdollHand lastHandler;
        public List<Handle> handles;
        public Handle mainHandleLeft;
        public Handle mainHandleRight;
        public Handle GetMainHandle(Side side) => null;
        public List<RagdollHand> handlers;
        public Holder holder; public bool isTelekinesisGrabbed;
        public bool isBrokenPiece;
        public bool isFlying;
        public bool IsHanded() => false;
        public void Despawn() { }
        public bool FullyUnpenetrate() => false;
    }

    public class Creature : ThunderEntity
    {
        public Mana mana;
        public void Despawn(float delay) { }
        public void Teleport(Vector3 position, Quaternion rotation) { }
        public bool HasSkill(string id) => false;
        public bool HasSkill(SkillData skill) => false;
        public enum PushType { Magic = 0, Grab = 1, Hit = 2, Parry = 3 }
        public enum ColorModifier { Hair = 0, HairSecondary = 1, HairSpecular = 2, EyesIris = 3, EyesSclera = 4, Skin = 5 }
        public delegate void DamageEvent(CollisionInstance collisionInstance, EventTime eventTime);
        public static List<Creature> allActive;
        public WaveData.Group spawnGroup;
        public bool countTowardsMaxAlive;
        public int factionId;
        public Transform jaw;
        public Transform centerEyes;
        public float spawnTime;
        public bool autoEyeClipsActive;
        public List<CreatureEye> allEyes;
        public FloatHandler detectionFOVModifier;
        public FloatHandler healthModifier;
        public float currentHealth { get => 0f; set { } }
        public float maxHealth { get => 0f; set { } }
        public void Heal(float healing) { }
        public List<RevealDecal> revealDecals;
        public void SetHeight(float height) { }
        public float GetHeight() => 0f;
        public Dictionary<object, float> damageMultipliers;
        public void Damage(CollisionInstance collisionInstance) { }
        public Ragdoll ragdoll;
        public Brain brain;
        public Locomotion locomotion;
        public RagdollHand handLeft;
        public RagdollHand handRight;
        public bool isPlayer;
        public CreatureData data;
        public bool isKilled;
        public bool loaded;
        public event DamageEvent OnDamageEvent;
        public void Damage(float amount) { }
        public void TryPush(PushType type, Vector3 direction, int pushLevel, RagdollPart.Type bodyPart = (RagdollPart.Type)0) { }
        public void Kill() { }
        public void SetFaction(int factionId) { }
        public void Resurrect(float newHealth, Creature resurrector) { }
        public void SetColor(Color color, ColorModifier colorModifier, bool updateProperties = false) { }
        public Color GetColor(ColorModifier colorModifier) => default;
        public void ForceStagger(Vector3 direction, BrainModuleHitReaction.PushBehaviour.Effect pushType, RagdollPart.Type bodyPart = RagdollPart.Type.Torso) { }
    }

    public class PlayerControl : ThunderBehaviour
    {
        public class Hand { public bool gripPressed, pinchPressed, usePressed, alternateUsePressed, castPressed; public void HapticShort(float intensity, bool oneFrameCooldown = false) { } }
        public static Hand GetHand(Side side) => null;
    }

    public class EventManager : MonoBehaviour
    {
        public delegate void CreatureKillEvent(Creature creature, Player player, CollisionInstance collisionInstance, EventTime eventTime);
        public delegate void RagdollSlicedEvent(RagdollPart part, EventTime eventTime);
        public delegate void LevelLoadEvent(LevelData levelData, LevelData.Mode mode, EventTime eventTime);
        public static event CreatureKillEvent onCreatureKill;
        public static event RagdollSlicedEvent onRagdollSliced;
        public static event LevelLoadEvent onLevelUnload;
    }

    public class ModManager { public class ModData { public string fullPath; public string Name; public List<ModOption> modOptions; } public static bool gameModsLoaded; public static void RefreshModOptionsUI() { } }

    public abstract class ThunderScript
    {
        public virtual void ScriptLoaded(ModManager.ModData modData) { }
        public virtual void ScriptEnable() { }
        public virtual void ScriptDisable() { }
        public virtual void ScriptFixedUpdate() { }
        public virtual void ScriptUpdate() { }
        public virtual void ScriptLateUpdate() { }
        public virtual void ScriptUnload() { }
    }

    // ---- Mod Options attributes (real shapes: ModOption has properties, not fields) ----
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public abstract class ModOptionAttribute : Attribute { }
    public class ModOption : ModOptionAttribute
    {
        public enum InteractionType { ArrowList = 0, ButtonList = 1, Slider = 2, Text = 3 }
        public ModOption() { }
        public ModOption(string name) { }
        public ModOption(string name, string tooltip) { }
        public string name { get; set; }
        public string tooltip { get; set; }
        public bool saveValue { get; set; }
        public int order { get; set; }
        public string category { get; set; }
        public int categoryOrder { get; set; }
        public string valueSourceName { get; set; }
        public InteractionType interactionType { get; set; }
        public int defaultValueIndex { get; set; }
        public int currentValueIndex { get; }
        public void Apply(int index) { }
        public void RefreshUI() { }
        public ModOptionParameter[] parameterValues;
        public object GetValue(int index) => null;
    }
    public class ModOptionCategory : ModOptionAttribute { public ModOptionCategory(string category, int categoryOrder) { } }
    public class ModOptionOrder : ModOptionAttribute { public ModOptionOrder(int order) { } }
    public class ModOptionUI : ModOptionAttribute { protected ModOptionUI() { } public ModOptionUI(ModOption.InteractionType interactionType) { } }
    public class ModOptionSlider : ModOptionUI { public ModOptionSlider() { } }
    public class ModOptionButton : ModOptionUI { public ModOptionButton() { } }
    public abstract class ModOptionParameter { public string title; public object value; }
    public class ModOptionString : ModOptionParameter { public ModOptionString(string title, string titleLocalizationId, string value) { } }
    public class ModOptionFloatValues : ModOptionAttribute { public ModOptionFloatValues(float startRange, float endRange, float step) { } }
}

namespace ThunderRoad.AI.Action
{
    public class AttackThrow { }
}
