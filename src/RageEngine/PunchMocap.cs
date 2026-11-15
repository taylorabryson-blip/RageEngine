// Generated from CMU Graphics Lab Motion Capture Database (mocap.cs.cmu.edu), subjects 13 and 14 boxing takes
// (13_17, 13_18, 14_01, 14_02, 14_03; BVH conversion by B. Hahne/cgspeed). 158 real punches were detected and
// classified; each template = median fist path relative to the punching shoulder, in arm-lengths, for a RIGHT hand
// (x = right, y = up, z = forward). Left hand = mirror x. Durations are the measured medians (seconds).
using UnityEngine;
namespace RageEngine
{
    internal static class PunchMocap
    {
        // Jab: 55 punches
        internal static readonly Vector3[] JabOut = { new Vector3(-0.005f, -0.262f, 0.336f), new Vector3(-0.006f, -0.254f, 0.344f), new Vector3(-0.011f, -0.233f, 0.360f), new Vector3(-0.021f, -0.212f, 0.387f), new Vector3(-0.040f, -0.168f, 0.419f), new Vector3(-0.065f, -0.113f, 0.467f), new Vector3(-0.097f, -0.062f, 0.523f), new Vector3(-0.137f, 0.023f, 0.601f), new Vector3(-0.141f, 0.071f, 0.677f), new Vector3(-0.173f, 0.107f, 0.759f), new Vector3(-0.161f, 0.135f, 0.805f), new Vector3(-0.139f, 0.143f, 0.823f) };
        internal static readonly Vector3[] JabBack = { new Vector3(0.002f, 0.064f, 0.777f), new Vector3(0.084f, -0.014f, 0.703f), new Vector3(0.126f, -0.116f, 0.628f), new Vector3(0.155f, -0.182f, 0.566f), new Vector3(0.190f, -0.220f, 0.510f), new Vector3(0.189f, -0.250f, 0.461f), new Vector3(0.199f, -0.246f, 0.430f), new Vector3(0.182f, -0.232f, 0.421f) };
        internal const float JabOutTime = 0.167f, JabBackTime = 0.267f;
        // Cross: 75 punches
        internal static readonly Vector3[] CrossOut = { new Vector3(-0.059f, -0.252f, 0.273f), new Vector3(-0.077f, -0.225f, 0.284f), new Vector3(-0.089f, -0.176f, 0.301f), new Vector3(-0.100f, -0.140f, 0.331f), new Vector3(-0.126f, -0.087f, 0.373f), new Vector3(-0.137f, -0.018f, 0.430f), new Vector3(-0.149f, 0.060f, 0.505f), new Vector3(-0.180f, 0.134f, 0.596f), new Vector3(-0.199f, 0.171f, 0.677f), new Vector3(-0.230f, 0.233f, 0.752f), new Vector3(-0.268f, 0.270f, 0.798f), new Vector3(-0.294f, 0.249f, 0.817f) };
        internal static readonly Vector3[] CrossBack = { new Vector3(-0.281f, 0.164f, 0.788f), new Vector3(-0.185f, 0.028f, 0.737f), new Vector3(-0.104f, -0.106f, 0.648f), new Vector3(-0.056f, -0.210f, 0.562f), new Vector3(-0.040f, -0.263f, 0.465f), new Vector3(-0.021f, -0.295f, 0.386f), new Vector3(-0.012f, -0.280f, 0.343f), new Vector3(-0.024f, -0.259f, 0.333f) };
        internal const float CrossOutTime = 0.150f, CrossBackTime = 0.283f;
        // Hook: 5 punches
        internal static readonly Vector3[] HookOut = { new Vector3(0.007f, -0.414f, 0.193f), new Vector3(-0.052f, -0.351f, 0.208f), new Vector3(-0.126f, -0.232f, 0.234f), new Vector3(-0.195f, -0.193f, 0.313f), new Vector3(-0.241f, -0.142f, 0.334f), new Vector3(-0.252f, -0.002f, 0.409f), new Vector3(-0.230f, 0.155f, 0.502f), new Vector3(-0.206f, 0.305f, 0.568f), new Vector3(-0.199f, 0.403f, 0.617f), new Vector3(-0.218f, 0.408f, 0.637f), new Vector3(-0.324f, 0.365f, 0.654f), new Vector3(-0.379f, 0.314f, 0.663f) };
        internal static readonly Vector3[] HookBack = { new Vector3(-0.373f, 0.214f, 0.648f), new Vector3(-0.280f, 0.072f, 0.591f), new Vector3(-0.239f, -0.099f, 0.513f), new Vector3(-0.167f, -0.218f, 0.482f), new Vector3(-0.087f, -0.320f, 0.420f), new Vector3(-0.018f, -0.398f, 0.360f), new Vector3(0.023f, -0.431f, 0.331f), new Vector3(0.014f, -0.408f, 0.307f) };
        internal const float HookOutTime = 0.217f, HookBackTime = 0.300f;
        // Uppercut: 21 punches
        internal static readonly Vector3[] UppercutOut = { new Vector3(-0.092f, -0.833f, -0.134f), new Vector3(-0.085f, -0.835f, -0.125f), new Vector3(-0.138f, -0.830f, -0.076f), new Vector3(-0.224f, -0.785f, -0.009f), new Vector3(-0.335f, -0.674f, 0.067f), new Vector3(-0.515f, -0.517f, 0.170f), new Vector3(-0.618f, -0.205f, 0.327f), new Vector3(-0.586f, 0.071f, 0.504f), new Vector3(-0.423f, 0.274f, 0.629f), new Vector3(-0.238f, 0.358f, 0.710f), new Vector3(-0.028f, 0.361f, 0.757f), new Vector3(0.062f, 0.317f, 0.777f) };
        internal static readonly Vector3[] UppercutBack = { new Vector3(0.165f, 0.213f, 0.755f), new Vector3(0.238f, 0.084f, 0.707f), new Vector3(0.246f, -0.027f, 0.662f), new Vector3(0.252f, -0.079f, 0.603f), new Vector3(0.207f, -0.131f, 0.575f), new Vector3(0.171f, -0.160f, 0.548f), new Vector3(0.126f, -0.182f, 0.518f), new Vector3(0.130f, -0.201f, 0.504f) };
        internal const float UppercutOutTime = 0.267f, UppercutBackTime = 0.233f;
        internal static Vector3 Sample(Vector3[] a, float u)
        {
            float f = Mathf.Clamp01(u) * (a.Length - 1); int i = Mathf.Min((int)f, a.Length - 2);
            return Vector3.LerpUnclamped(a[i], a[i + 1], f - i);
        }
    }
}
