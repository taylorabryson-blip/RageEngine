// Rage Engine 0.28.1 - the Skybar comes alive.
//   MUSIC       lounge jazz from speakers behind the bar (real recordings, Kevin MacLeod, CC-BY - see SOUND_CREDITS),
//               a playlist that moves on to the next track. Elevator music inside the elevator.
//   AMBIENCE    the city far below all the time (quieter indoors), wind when you're out on the terrace or the roof.
//   ELEVATOR    a working lift: the doors slide open when someone comes near (ding), close behind them. Enemy waves
//               in the Skybar can arrive by elevator - you hear the ding and the doors open on them.
//   DRESS CODE  NPCs who come to the Skybar wear dark suits (their clothes are recoloured black, shirts white).
// Map file lines: MUSIC x y z volume | AMBIENT name volume [outside] | E x0 x1 doorZ backZ | DRESS suit
// Audio files: Music/lounge_*.ogg, Music/elevator.ogg, Ambience/<name>.ogg (also .wav).
using System.Collections.Generic;
using System.IO;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Networking;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        public static float mapMusic = 0.5f;
        public static float mapAmbience = 0.7f;
        public static bool mapSuits = true;

        sealed class Speaker28 { public Vector3 pos; public float vol; public AudioSource src; }
        sealed class Amb28 { public string name; public float vol; public bool outside; public AudioSource src; public float cur; }
        sealed class Lift28
        {
            public float x0, x1, zDoor, zBack;
            public Transform doorL, doorR; public Vector3 lClosed, rClosed;
            public float open, holdUntil, arriveAt; public bool arriving, wasOpen;
            public AudioSource music; public Light light;
            public Vector3 Inside(Vector3 origin) => origin + new Vector3((x0 + x1) * 0.5f, 0.05f, (zDoor + zBack) * 0.5f + 0.2f);
        }
        static readonly List<Speaker28> speakers28 = new List<Speaker28>();
        static readonly List<Amb28> ambs28 = new List<Amb28>();
        static readonly List<Lift28> lifts28 = new List<Lift28>();
        static bool dressSuit28;
        static readonly List<AudioClip> lounge28 = new List<AudioClip>();
        static AudioClip elevatorClip28;
        static readonly Dictionary<string, AudioClip> ambClips28 = new Dictionary<string, AudioClip>();
        static readonly List<KeyValuePair<UnityWebRequest, string>> loads28 = new List<KeyValuePair<UnityWebRequest, string>>();
        static bool audioRequested28; static int track28 = -1; static bool loungeOn28;
        static readonly List<KeyValuePair<Creature, float>> toDress28 = new List<KeyValuePair<Creature, float>>();
        static bool suitNamesLogged;

        static void ResetLounge28()
        {
            StopLounge28();
            speakers28.Clear(); ambs28.Clear(); lifts28.Clear(); dressSuit28 = false;
        }

        static void ParseLounge28(string[] p)
        {
            switch (p[0])
            {
                case "MUSIC": speakers28.Add(new Speaker28 { pos = new Vector3(PF(p[1]), PF(p[2]), PF(p[3])), vol = PF(p[4]) }); break;
                case "AMBIENT": ambs28.Add(new Amb28 { name = p[1], vol = PF(p[2]), outside = p.Length > 3 && p[3] == "outside" }); break;
                case "E": lifts28.Add(new Lift28 { x0 = PF(p[1]), x1 = PF(p[2]), zDoor = PF(p[3]), zBack = PF(p[4]) }); break;
                case "DRESS": dressSuit28 = p.Length > 1 && p[1] == "suit"; break;
            }
        }

        /// After the map geometry: elevator doors, light, speakers.
        static void BuildLounge28(Transform root)
        {
            int def = root.gameObject.layer;
            foreach (var L in lifts28)
            {
                float w = L.x1 - L.x0, half = w * 0.5f, h = 2.4f;
                var dl = Block(root, "LiftDoorL", new Vector3(L.x0 + half * 0.5f, h * 0.5f, L.zDoor), new Vector3(half, h, 0.06f), "steel", true, def);
                var dr = Block(root, "LiftDoorR", new Vector3(L.x1 - half * 0.5f, h * 0.5f, L.zDoor), new Vector3(half, h, 0.06f), "steel", true, def);
                foreach (var d in new[] { dl, dr }) { var rb = d.AddComponent<Rigidbody>(); rb.isKinematic = true; }
                L.doorL = dl.transform; L.doorR = dr.transform; L.lClosed = dl.transform.localPosition; L.rClosed = dr.transform.localPosition;
                var lg = new GameObject("LiftLight"); lg.transform.SetParent(root, false);
                lg.transform.localPosition = new Vector3((L.x0 + L.x1) * 0.5f, 2.45f, (L.zDoor + L.zBack) * 0.5f);
                L.light = lg.AddComponent<Light>(); L.light.type = LightType.Point; L.light.range = 3.5f; L.light.intensity = 1.2f; L.light.color = new Color(1f, 0.93f, 0.8f); L.light.shadows = LightShadows.None;
                L.music = lg.AddComponent<AudioSource>(); L.music.loop = true; L.music.spatialBlend = 1f; L.music.rolloffMode = AudioRolloffMode.Linear; L.music.minDistance = 0.8f; L.music.maxDistance = 4.5f; L.music.dopplerLevel = 0f;
                L.open = 0f; L.arriving = false;
            }
            foreach (var sp in speakers28)
            {
                var go = new GameObject("Speaker"); go.transform.SetParent(root, false); go.transform.localPosition = sp.pos;
                sp.src = go.AddComponent<AudioSource>(); sp.src.loop = false; sp.src.spatialBlend = 0.85f; sp.src.rolloffMode = AudioRolloffMode.Linear;
                sp.src.minDistance = 3f; sp.src.maxDistance = 24f; sp.src.dopplerLevel = 0f;
            }
            foreach (var a in ambs28)
            {
                var go = new GameObject("Ambience_" + a.name); go.transform.SetParent(root, false);
                a.src = go.AddComponent<AudioSource>(); a.src.loop = true; a.src.spatialBlend = 0f; a.src.volume = 0f; a.cur = 0f;
            }
        }

        // ------------------------------------------------------------------ audio files (streamed ogg, or wav)
        static void RequestAudio28()
        {
            if (audioRequested28 || string.IsNullOrEmpty(modFolder)) return;
            audioRequested28 = true;
            foreach (var sub in new[] { "Music", "Ambience" })
            {
                string dir = Path.Combine(modFolder, sub);
                if (!Directory.Exists(dir)) { Debug.Log("[RageEngine] lounge: no " + sub + " folder"); continue; }
                var files = new List<string>(Directory.GetFiles(dir));
                files.Sort();
                foreach (var f in files)
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant(), key = sub + "/" + Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    if (ext == ".wav")
                    {
                        try { var c = Sfx.FromWav(File.ReadAllBytes(f), key); if (c != null) AddClip28(key, c); } catch (System.Exception e) { Debug.Log("[RageEngine] lounge wav " + f + ": " + e.Message); }
                    }
                    else if (ext == ".ogg")
                    {
                        var req = UnityWebRequestMultimedia.GetAudioClip("file://" + f, AudioType.OGGVORBIS);
                        ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = true;
                        req.SendWebRequest();
                        loads28.Add(new KeyValuePair<UnityWebRequest, string>(req, key));
                    }
                }
            }
        }

        static void AddClip28(string key, AudioClip c)
        {
            c.name = key;
            if (key.StartsWith("music/lounge")) lounge28.Add(c);
            else if (key.StartsWith("music/elevator")) elevatorClip28 = c;
            else if (key.StartsWith("ambience/")) ambClips28[key.Substring(9)] = c;
        }

        static void PollLoads28()
        {
            for (int i = loads28.Count - 1; i >= 0; i--)
            {
                var req = loads28[i].Key;
                if (!req.isDone) continue;
                string key = loads28[i].Value;
                loads28.RemoveAt(i);
                try
                {
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var c = DownloadHandlerAudioClip.GetContent(req);
                        if (c != null) { AddClip28(key, c); Debug.Log($"[RageEngine] lounge audio ready: {key} ({c.length:0}s)"); }
                    }
                    else Debug.Log($"[RageEngine] lounge audio failed: {key}: {req.error}");
                }
                catch (System.Exception e) { Debug.Log($"[RageEngine] lounge audio {key}: {e.Message}"); }
                req.Dispose();
            }
        }

        // ------------------------------------------------------------------ start / stop
        static void StartLounge28()
        {
            loungeOn28 = true;
            RequestAudio28();
        }

        static void StopLounge28()
        {
            loungeOn28 = false;
            foreach (var sp in speakers28) if (sp.src != null) sp.src.Stop();
            foreach (var a in ambs28) if (a.src != null) { a.src.Stop(); a.cur = 0f; }
            foreach (var L in lifts28) if (L.music != null) L.music.Stop();
        }

        // ------------------------------------------------------------------ every frame while the map exists
        void TickLounge28()
        {
            if (loads28.Count > 0) PollLoads28();
            if (map26Root == null) return;
            float dt = Time.deltaTime, now = Time.time;
            var pl = Player.local; var pc = pl != null ? pl.creature : null;
            // ---- music: one playlist, all speakers in sync
            if (loungeOn28 && speakers28.Count > 0 && lounge28.Count > 0 && speakers28[0].src != null)
            {
                var master = speakers28[0].src;
                if (!master.isPlaying)
                {
                    track28 = (track28 + 1 + Random.Range(0, Mathf.Max(1, lounge28.Count - 1))) % lounge28.Count;
                    foreach (var sp in speakers28) if (sp.src != null) { sp.src.clip = lounge28[track28]; sp.src.timeSamples = 0; sp.src.Play(); }
                    Debug.Log("[RageEngine] lounge: now playing " + lounge28[track28].name);
                }
                foreach (var sp in speakers28) if (sp.src != null) sp.src.volume = sp.vol * mapMusic;
            }
            // ---- ambience
            if (loungeOn28 && ambs28.Count > 0 && pc != null)
            {
                Vector3 head = pc.ragdoll != null && pc.ragdoll.headPart != null ? pc.ragdoll.headPart.transform.position : pc.transform.position + Vector3.up * 1.6f;
                bool outside = !Physics.Raycast(head + Vector3.up * 0.3f, Vector3.up, 40f, ~0, QueryTriggerInteraction.Ignore);
                foreach (var a in ambs28)
                {
                    if (a.src == null) continue;
                    if (a.src.clip == null && ambClips28.TryGetValue(a.name, out var clip)) { a.src.clip = clip; a.src.Play(); }
                    float target = a.vol * mapAmbience * (a.outside ? (outside ? 1f : 0.12f) : (outside ? 1f : 0.45f));
                    a.cur = Mathf.MoveTowards(a.cur, target, dt * 0.5f);
                    a.src.volume = a.cur;
                    if (a.src.clip != null && !a.src.isPlaying) a.src.Play();
                }
            }
            // ---- elevators
            foreach (var L in lifts28)
            {
                if (L.doorL == null) continue;
                Vector3 lo = map26Origin + new Vector3(L.x0 - 0.6f, -0.5f, L.zDoor - 1.8f), hi = map26Origin + new Vector3(L.x1 + 0.6f, 2.5f, L.zBack);
                bool someone = pc != null && Inside(pc.transform.position, lo, hi);
                if (!someone)
                    foreach (var m in pool)
                        if (m != null && m.active && m.creature != null && !m.creature.isKilled && Inside(m.creature.transform.position, lo, hi)) { someone = true; break; }
                bool want;
                if (L.arriving) { want = now >= L.arriveAt; if (want) { L.arriving = false; L.holdUntil = now + 6f; } }
                else { if (someone) L.holdUntil = now + 3f; want = now < L.holdUntil; }
                float before = L.open;
                L.open = Mathf.MoveTowards(L.open, want ? 1f : 0f, dt / 1.3f);
                if (before <= 0f && L.open > 0f) { Sfx.PlayAt("NRE_ElevatorDing", L.doorL.position, 0.8f); Sfx.PlayAt("NRE_ElevatorDoor", L.doorL.position, 0.5f); }
                if (before >= 1f && L.open < 1f) Sfx.PlayAt("NRE_ElevatorDoor", L.doorL.position, 0.45f);
                float k = L.open * L.open * (3f - 2f * L.open), slide = (L.x1 - L.x0) * 0.5f * 0.95f;
                L.doorL.localPosition = L.lClosed + Vector3.left * slide * k;
                L.doorR.localPosition = L.rClosed + Vector3.right * slide * k;
                if (L.music != null)
                {
                    if (loungeOn28 && elevatorClip28 != null && !L.music.isPlaying) { L.music.clip = elevatorClip28; L.music.Play(); }
                    L.music.volume = 0.45f * mapMusic;
                }
            }
            // ---- dress code
            if (toDress28.Count > 0)
                for (int i = toDress28.Count - 1; i >= 0; i--)
                {
                    if (now < toDress28[i].Value) continue;
                    var c = toDress28[i].Key; toDress28.RemoveAt(i);
                    if (c != null && !c.isKilled) Guard("suit", () => DressSuit28(c));
                }
        }

        static bool Inside(Vector3 p, Vector3 lo, Vector3 hi) => p.x > lo.x && p.x < hi.x && p.y > lo.y && p.y < hi.y && p.z > lo.z && p.z < hi.z;

        /// For MapWave: some of the wave arrives by elevator. Returns a spawn position or null.
        static Vector3? LiftArrival28(int index)
        {
            if (lifts28.Count == 0 || index > 1) return null;
            var L = lifts28[index % lifts28.Count];
            L.arriving = true; L.arriveAt = Time.time + 3f; L.holdUntil = 0f;
            Vector3 p = L.Inside(map26Origin);
            p.x += index == 0 ? -0.35f : 0.35f;
            return p;
        }

        static void QueueSuit28(Creature c)
        {
            if (dressSuit28 && mapSuits && c != null) toDress28.Add(new KeyValuePair<Creature, float>(c, Time.time + 1.5f));
        }

        // ------------------------------------------------------------------ suits
        static readonly string[] skinWords28 = { "body", "head", "eye", "hair", "lash", "brow", "teeth", "tooth", "tongue", "mouth", "skin", "face", "beard", "mustache", "nail", "hand" };
        static void DressSuit28(Creature c)
        {
            int dressed = 0;
            var names = new System.Text.StringBuilder();
            foreach (var r in c.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r == null || r.sharedMaterial == null) continue;
                string n = (r.name + " " + r.sharedMaterial.name).ToLowerInvariant();
                if (!suitNamesLogged) names.Append(r.name).Append('/').Append(r.sharedMaterial.name).Append("; ");
                bool skin = false; foreach (var w in skinWords28) if (n.Contains(w)) { skin = true; break; }
                if (skin) continue;
                bool shirt = n.Contains("shirt") && !n.Contains("over") && !n.Contains("coat") && !n.Contains("jacket");
                Color tint = shirt ? new Color(0.92f, 0.92f, 0.9f, 1f) : new Color(0.07f, 0.07f, 0.085f, 1f);
                foreach (var m in r.materials)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
                }
                dressed++;
            }
            if (!suitNamesLogged) { suitNamesLogged = true; Debug.Log("[RageEngine] suit: renderers on " + c.name + ": " + names); }
            Debug.Log($"[RageEngine] suit: {c.name} dressed ({dressed} pieces)");
        }
    }
}
