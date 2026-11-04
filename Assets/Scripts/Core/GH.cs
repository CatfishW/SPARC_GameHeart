using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameHeart
{
    /// <summary>Small shared helpers: colours, easing, tweens, URL query flags.</summary>
    public static class GH
    {
        public static Color Hex(string hex, float alpha = 1f)
        {
            if (string.IsNullOrEmpty(hex) || !ColorUtility.TryParseHtmlString(hex, out var c)) c = Color.white;
            c.a = alpha;
            return c;
        }

        public static Color WithA(this Color c, float a) { c.a = a; return c; }
        public static Color Mul(this Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        public static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); return 1 - Mathf.Pow(1 - t, 3); }
        public static float EaseInCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float EaseInOutCubic(float t) { t = Mathf.Clamp01(t); return t < .5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2; }
        public static float EaseOutExpo(float t) { t = Mathf.Clamp01(t); return t >= 1 ? 1 : 1 - Mathf.Pow(2, -10 * t); }
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
        }
        public static float EaseOutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0 || t >= 1) return t;
            return Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - 0.75f) * (2 * Mathf.PI / 3)) + 1;
        }
        public static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        /// <summary>Frame-rate independent exponential approach.</summary>
        public static float Damp(float current, float target, float rate, float dt) => Mathf.Lerp(current, target, 1 - Mathf.Exp(-rate * dt));
        public static Vector3 Damp(Vector3 current, Vector3 target, float rate, float dt) => Vector3.Lerp(current, target, 1 - Mathf.Exp(-rate * dt));

        public static IEnumerator Tween(float duration, Action<float> step, Func<float, float> ease = null)
        {
            ease ??= EaseOutCubic;
            float t = 0;
            if (duration <= 0) { step(1); yield break; }
            while (t < duration)
            {
                step(ease(t / duration));
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            step(1);
        }

        public static IEnumerator Wait(float seconds)
        {
            float t = 0;
            while (t < seconds) { yield return null; t += Time.unscaledDeltaTime; }
        }

        // ---------- URL query (QA hooks) ----------
        static Dictionary<string, string> query;

        public static string Query(string key)
        {
            if (query == null)
            {
                query = new Dictionary<string, string>();
                string url = Application.absoluteURL ?? "";
                int q = url.IndexOf('?');
                if (q >= 0)
                {
                    string s = url.Substring(q + 1);
                    int hash = s.IndexOf('#');
                    if (hash >= 0) s = s.Substring(0, hash);
                    foreach (var part in s.Split('&'))
                    {
                        if (part.Length == 0) continue;
                        int eq = part.IndexOf('=');
                        string k = eq >= 0 ? part.Substring(0, eq) : part;
                        string v = eq >= 0 ? Uri.UnescapeDataString(part.Substring(eq + 1)) : "1";
                        query[k] = v;
                    }
                }
#if UNITY_EDITOR
                foreach (var arg in Environment.GetCommandLineArgs())
                    if (arg.StartsWith("-gh-"))
                    {
                        var kv = arg.Substring(4).Split('=');
                        query[kv[0]] = kv.Length > 1 ? kv[1] : "1";
                    }
#endif
            }
            return query.TryGetValue(key, out var val) ? val : null;
        }

        public static void QueryOverride(string key, string value) { Query(key); query[key] = value; }

        public static bool Flag(string key) => Query(key) != null && Query(key) != "0";
        public static bool QA => Flag("qa");

        public static void Log(string evt, string detail = "")
        {
            Debug.Log($"GH_EVENT {evt} {detail}");
        }

        public static string FormatScore(int score) => score.ToString("0,000,000").Replace(',', ' ');
        public static string Pad2(int n) => n.ToString("00");
    }

    /// <summary>Persistent coroutine host so non-MonoBehaviour code can run tweens.</summary>
    public class Runner : MonoBehaviour
    {
        static Runner instance;

        public static Runner I
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("GH Runner");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Runner>();
                }
                return instance;
            }
        }

        public static Coroutine Go(IEnumerator routine) => I.StartCoroutine(routine);
        public static void Stop(Coroutine c) { if (c != null && instance != null) instance.StopCoroutine(c); }
    }
}
