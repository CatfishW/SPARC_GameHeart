using System.Collections.Generic;
using UnityEngine;

namespace GameHeart
{
    public class NoteView
    {
        public GameObject root;
        public Transform head, body, tail, glow;
        public MeshRenderer headR, bodyR, tailR, glowR;
        public NoteType type;
        public int lane;
    }

    /// <summary>The 3D note highway: glass track, receptors, SA-node judgement line, pooled notes and hit effects.</summary>
    public class HighwayView : MonoBehaviour
    {
        const int L = RhythmGame.Lanes;
        const float W = RhythmGame.LaneW;
        Material trackMat, bodyMat, glowMat, columnMat, ringMat, lineMat, receptorMat, sparkMat;
        readonly Material[] laneNoteMat = new Material[L];
        Material pulseMat, holdHeadMat;
        readonly Dictionary<string, Stack<NoteView>> pool = new Dictionary<string, Stack<NoteView>>();
        Transform noteRoot;
        readonly Transform[] receptors = new Transform[L];
        readonly MeshRenderer[] receptorR = new MeshRenderer[L];
        readonly Transform[] columns = new Transform[L];
        readonly MeshRenderer[] columnR = new MeshRenderer[L];
        readonly float[] press = new float[L], flash = new float[L], holdGlow = new float[L];
        readonly Color[] flashColor = new Color[L];
        Transform line, node, pulseRing;
        MeshRenderer lineR, nodeR, pulseRingR;
        float pulseRingT = 9, lineKick;
        readonly List<(Transform t, MeshRenderer r, float age, Color c, float size)> rings = new List<(Transform, MeshRenderer, float, Color, float)>();
        ParticleSystem sparks;
        MaterialPropertyBlock mpb;
        Color laneOuter, laneInner;
        bool built;

        public void Build(Chapter ch)
        {
            mpb ??= new MaterialPropertyBlock();
            laneOuter = ch != null ? ch.Color : UI.Crimson;
            laneInner = ch != null ? Color.Lerp(ch.Accent, Color.white, 0.4f) : Color.white;
            if (!built) Construct();
            for (int l = 0; l < L; l++)
            {
                var c = (l == 0 || l == L - 1) ? laneOuter : laneInner;
                laneNoteMat[l].SetColor("_Color", c * 1.05f);
                laneNoteMat[l].SetColor("_Rim", Color.Lerp(c, Color.white, 0.55f) * 1.5f);
            }
            trackMat.SetColor("_Edge", laneOuter * 1.1f);
            trackMat.SetColor("_Line", Color.Lerp(laneOuter, Color.white, 0.4f) * 0.55f);
            receptorMat.SetColor("_Color", Color.Lerp(laneOuter, Color.white, 0.35f) * 0.9f);
            lineMat.SetColor("_Color", Color.Lerp(laneOuter, Color.white, 0.55f) * 1.5f);
        }

        void Construct()
        {
            built = true;
            var add = Shader.Find("GameHeart/Additive");
            trackMat = new Material(Shader.Find("GameHeart/Highway")) { name = "Track" };
            trackMat.SetFloat("_Lanes", L);
            trackMat.SetFloat("_Length", RhythmGame.HighwayLength);
            glowMat = new Material(add) { name = "NoteGlow", mainTexture = StageTex.Glow };
            bodyMat = new Material(add) { name = "HoldBody", mainTexture = StageTex.Flow };
            columnMat = new Material(add) { name = "Column", mainTexture = StageTex.Column };
            ringMat = new Material(add) { name = "Ring", mainTexture = StageTex.Ring };
            lineMat = new Material(add) { name = "Line", mainTexture = StageTex.Line };
            receptorMat = new Material(add) { name = "Receptor", mainTexture = StageTex.ReceptorFrame };
            sparkMat = new Material(add) { name = "Spark", mainTexture = StageTex.Spark };
            var noteSh = Shader.Find("GameHeart/Note");
            for (int l = 0; l < L; l++) laneNoteMat[l] = new Material(noteSh) { name = "Note" + l };
            pulseMat = new Material(noteSh) { name = "Pulse" };
            pulseMat.SetColor("_Color", new Color(1.5f, 1.1f, 0.55f));
            pulseMat.SetColor("_Rim", new Color(1.8f, 1.6f, 1.2f));

            // track
            var track = Child("Track", Meshes.Quad(true), trackMat);
            float tw = L * W + 0.5f;
            track.localScale = new Vector3(tw, 1, RhythmGame.HighwayLength + 4);
            track.localPosition = new Vector3(0, 0, (RhythmGame.HighwayLength + 4) / 2f - 4);
            // fix uv so v=0 at judgement line: shader uses uv.y as far fraction; offset handled by fade
            noteRoot = new GameObject("Notes").transform;
            noteRoot.SetParent(transform, false);

            for (int l = 0; l < L; l++)
            {
                float x = (l - (L - 1) / 2f) * W;
                receptors[l] = Child("Receptor" + l, Meshes.Quad(true), receptorMat);
                receptors[l].localPosition = new Vector3(x, 0.03f, 0);
                receptors[l].localScale = new Vector3(W * 0.9f, 1, 0.75f);
                receptorR[l] = receptors[l].GetComponent<MeshRenderer>();
                columns[l] = Child("Column" + l, Meshes.Quad(false), columnMat);
                columns[l].localPosition = new Vector3(x, 2.4f, 0.1f);
                columns[l].localScale = new Vector3(W * 0.95f, 4.8f, 1);
                columns[l].localRotation = Quaternion.Euler(0, 0, 180);
                columnR[l] = columns[l].GetComponent<MeshRenderer>();
                columnR[l].enabled = false;
            }
            line = Child("JudgeLine", Meshes.Quad(true), lineMat);
            line.localPosition = new Vector3(0, 0.05f, 0);
            line.localScale = new Vector3(L * W + 1.6f, 1, 0.5f);
            lineR = line.GetComponent<MeshRenderer>();
            node = Child("SANode", Meshes.Quad(false), sparkMat);
            node.localPosition = new Vector3(0, 0.35f, -0.05f);
            node.localScale = Vector3.one * 1.6f;
            nodeR = node.GetComponent<MeshRenderer>();
            pulseRing = Child("PulseRing", Meshes.Quad(true), ringMat);
            pulseRingR = pulseRing.GetComponent<MeshRenderer>();
            pulseRingR.enabled = false;

            // sparks
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            var em = sparks.emission; em.enabled = false;
            var shape = sparks.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 0.2f;
            shape.rotation = new Vector3(-90, 0, 0);
            var col = sparks.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var size = sparks.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.material = sparkMat;
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.lengthScale = 2.5f;
            pr.velocityScale = 0.03f;
            sparks.Play();
        }

        Transform Child(string n, Mesh mesh, Material mat)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        // ------------------------------------------------------------------ pool
        public NoteView Get(NoteType type, int lane)
        {
            string key = type == NoteType.Pulse ? "pulse" : type + "_" + lane;
            if (!pool.TryGetValue(key, out var st)) pool[key] = st = new Stack<NoteView>();
            NoteView v = st.Count > 0 ? st.Pop() : Create(type, lane);
            v.root.SetActive(true);
            return v;
        }

        public void Release(NoteView v)
        {
            if (v == null) return;
            v.root.SetActive(false);
            string key = v.type == NoteType.Pulse ? "pulse" : v.type + "_" + v.lane;
            pool[key].Push(v);
        }

        NoteView Create(NoteType type, int lane)
        {
            var v = new NoteView { type = type, lane = lane };
            v.root = new GameObject($"Note {type} {lane}");
            v.root.transform.SetParent(noteRoot, false);
            Transform Part(string n, Mesh m, Material mat, out MeshRenderer r)
            {
                var go = new GameObject(n);
                go.transform.SetParent(v.root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return go.transform;
            }
            if (type == NoteType.Pulse)
            {
                v.head = Part("Head", Meshes.RoundedBar(L * W - 0.12f, 0.42f, 0.16f, 0.2f), pulseMat, out v.headR);
                v.glow = Part("Glow", Meshes.Quad(true), glowMat, out v.glowR);
                v.glow.localScale = new Vector3(L * W + 1.2f, 1, 2.2f);
                var heart = Part("Heart", Meshes.Quad(false), new Material(Shader.Find("GameHeart/Additive")) { mainTexture = StageTex.Heart }, out var hr);
                hr.sharedMaterial.SetColor("_Color", new Color(1.6f, 0.35f, 0.45f));
                heart.localPosition = new Vector3(0, 0.75f, 0);
                heart.localScale = Vector3.one * 1.1f;
            }
            else
            {
                var mat = laneNoteMat[lane];
                v.head = Part("Head", Meshes.RoundedBar(W * 0.86f, 0.46f, 0.2f, 0.16f), mat, out v.headR);
                v.glow = Part("Glow", Meshes.Quad(true), glowMat, out v.glowR);
                v.glow.localScale = new Vector3(W * 1.5f, 1, 1.6f);
                if (type == NoteType.Hold)
                {
                    v.body = Part("Body", Meshes.Quad(true), bodyMat, out v.bodyR);
                    v.tail = Part("Tail", Meshes.RoundedBar(W * 0.6f, 0.24f, 0.12f, 0.1f), mat, out v.tailR);
                }
            }
            return v;
        }

        public void Place(NoteView v, NoteType type, int lane, float zHead, float zTail, bool holding, bool missed, float fade)
        {
            float x = type == NoteType.Pulse ? 0 : (lane - (L - 1) / 2f) * W;
            v.root.transform.localPosition = new Vector3(x, 0, 0);
            float far = Mathf.Clamp01((RhythmGame.HighwayLength - zHead) / 10f);
            float a = fade * far;
            Color c = missed ? new Color(0.35f, 0.35f, 0.4f) : Color.white;
            v.head.localPosition = new Vector3(0, 0.02f, zHead);
            v.head.gameObject.SetActive(!holding || type != NoteType.Hold);
            mpb.Clear();
            mpb.SetFloat("_Alpha", a);
            if (missed) { mpb.SetColor("_Color", c * 0.6f); mpb.SetColor("_Rim", c); }
            v.headR.SetPropertyBlock(mpb);
            if (v.glow != null)
            {
                v.glow.localPosition = new Vector3(0, 0.01f, zHead);
                mpb.Clear();
                var gc = type == NoteType.Pulse ? new Color(1.1f, 0.75f, 0.35f) : ((lane == 0 || lane == L - 1) ? laneOuter : laneInner) * 0.8f;
                mpb.SetColor("_Color", (missed ? new Color(0.2f, 0.2f, 0.2f) : gc).WithA(a * 0.35f));
                v.glowR.SetPropertyBlock(mpb);
                v.glow.gameObject.SetActive(!holding);
            }
            if (type == NoteType.Hold && v.body != null)
            {
                float z0 = Mathf.Max(zHead, holding ? 0 : zHead);
                float z1 = Mathf.Min(zTail, RhythmGame.HighwayLength);
                float len = Mathf.Max(0.01f, z1 - z0);
                v.body.localPosition = new Vector3(0, 0.015f, (z0 + z1) / 2f);
                v.body.localScale = new Vector3(W * 0.62f, 1, len);
                v.tail.localPosition = new Vector3(0, 0.02f, zTail);
                v.tail.gameObject.SetActive(zTail < RhythmGame.HighwayLength);
                mpb.Clear();
                var bc = (lane == 0 || lane == L - 1) ? laneOuter : laneInner;
                mpb.SetColor("_Color", (missed ? new Color(0.25f, 0.25f, 0.28f) : bc * (holding ? 1.25f : 0.7f)).WithA(fade * (holding ? 0.95f : 0.75f)));
                mpb.SetVector("_MainTex_ST", new Vector4(1, len / 3f, 0, -Time.time * (holding ? 3f : 0.6f)));
                v.bodyR.SetPropertyBlock(mpb);
                mpb.Clear();
                mpb.SetFloat("_Alpha", a);
                v.tailR.SetPropertyBlock(mpb);
            }
        }

        // ------------------------------------------------------------------ input / fx
        public void Press(int lane) { if (lane >= 0) press[lane] = 1; }

        public void HoldStart(int lane) { holdGlow[lane] = 1; }

        public void HoldEnd(int lane, bool ok)
        {
            holdGlow[lane] = 0;
            if (ok) { Burst(lane, Color.white, 14); Ring(lane, Color.white * 2f, 1.6f); }
        }

        public void HitFx(int lane, Judgment j)
        {
            var c = JudgeColor(j);
            if (lane < 0)
            {
                for (int l = 0; l < L; l++) { flash[l] = 0.8f; flashColor[l] = new Color(1.1f, 0.8f, 0.45f); Burst(l, new Color(1f, 0.85f, 0.5f), 10); }
                pulseRingT = 0;
                lineKick = 1;
                return;
            }
            flash[lane] = 1;
            flashColor[lane] = c * 0.9f;
            Burst(lane, c, j == Judgment.Perfect ? 18 : 10);
            Ring(lane, c * 1.2f, j == Judgment.Perfect ? 1.9f : 1.5f);
            lineKick = Mathf.Max(lineKick, 0.5f);
        }

        public void MissFx(int lane)
        {
            if (lane < 0) return;
            flash[lane] = 0.6f;
            flashColor[lane] = new Color(1.2f, 0.15f, 0.15f);
        }

        public static Color JudgeColor(Judgment j) => j switch
        {
            Judgment.Perfect => new Color(1f, 0.86f, 0.45f),
            Judgment.Great => new Color(0.45f, 0.95f, 1f),
            Judgment.Good => new Color(0.55f, 1f, 0.6f),
            _ => new Color(1f, 0.3f, 0.3f),
        };

        void Burst(int lane, Color c, int n)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = transform.TransformPoint(new Vector3((lane - (L - 1) / 2f) * W, 0.2f, 0)),
                startColor = c,
                applyShapeToPosition = true,
            };
            sparks.Emit(ep, n);
        }

        void Ring(int lane, Color c, float size)
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Meshes.Quad(true);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = ringMat;
            go.transform.localPosition = new Vector3((lane - (L - 1) / 2f) * W, 0.06f, 0);
            rings.Add((go.transform, r, 0, c, size));
        }

        public void Tick(float t, float speed, bool[] lanesHeld)
        {
            float dt = Time.unscaledDeltaTime;
            mpb ??= new MaterialPropertyBlock();
            var pressV = new Vector4();
            for (int l = 0; l < L; l++)
            {
                press[l] = Mathf.Max(press[l] - dt * 5f, lanesHeld[l] ? 0.55f : 0);
                flash[l] = Mathf.Max(0, flash[l] - dt * 4.5f);
                pressV[l] = Mathf.Max(press[l], holdGlow[l] * 0.9f);
                mpb.Clear();
                mpb.SetColor("_Color", Color.Lerp(receptorMat.GetColor("_Color"), Color.white * 1.4f, press[l] * 0.6f).WithA(0.5f + press[l] * 0.4f + holdGlow[l] * 0.3f));
                receptorR[l].SetPropertyBlock(mpb);
                receptors[l].localScale = new Vector3(W * 0.9f * (1 + press[l] * 0.06f), 1, 0.75f);
                columnR[l].enabled = flash[l] > 0.01f || holdGlow[l] > 0;
                if (columnR[l].enabled)
                {
                    mpb.Clear();
                    float k = Mathf.Max(flash[l], holdGlow[l] * (0.45f + 0.15f * Mathf.Sin(Time.time * 30)));
                    var fc = holdGlow[l] > 0 && flash[l] < 0.3f ? ((l == 0 || l == L - 1) ? laneOuter : laneInner) * 1.6f : flashColor[l];
                    mpb.SetColor("_Color", fc.WithA(k * (Save.S.reduceFlash ? 0.25f : 0.5f)));
                    columnR[l].SetPropertyBlock(mpb);
                    columns[l].localScale = new Vector3(W * (0.6f + 0.2f * k), 4.2f * (0.7f + 0.3f * k), 1);
                }
            }
            trackMat.SetVector("_Press", pressV);
            trackMat.SetFloat("_BeatSpacing", Mathf.Max(4f, speed * 60f / Mathf.Max(60f, RhythmGame.I != null && RhythmGame.I.Chart != null ? RhythmGame.I.Chart.bpm : 120f)));
            lineKick = Mathf.Max(0, lineKick - dt * 3f);
            float beat = Shader.GetGlobalFloat("_GH_Beat");
            line.localScale = new Vector3(L * W + 1.6f, 1, 0.5f + lineKick * 0.5f + beat * 0.15f);
            node.localScale = Vector3.one * (1.3f + beat * 0.5f + lineKick * 0.9f);
            node.localRotation = Quaternion.Euler(0, 0, Time.time * 40f);
            mpb.Clear();
            mpb.SetColor("_Color", new Color(1.5f, 1.1f, 1.2f) * (0.6f + beat * 0.4f + lineKick * 0.6f));
            nodeR.SetPropertyBlock(mpb);

            // pulse shockwave
            pulseRingT += dt;
            pulseRingR.enabled = pulseRingT < 0.6f;
            if (pulseRingR.enabled)
            {
                float k = pulseRingT / 0.6f;
                pulseRing.localPosition = new Vector3(0, 0.08f, 0);
                pulseRing.localScale = Vector3.one * (2 + GH.EaseOutCubic(k) * 16f);
                mpb.Clear();
                mpb.SetColor("_Color", new Color(1.3f, 0.9f, 0.55f).WithA((1 - k) * 0.8f));
                pulseRingR.SetPropertyBlock(mpb);
            }
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                var r = rings[i];
                r.age += dt;
                float k = r.age / 0.38f;
                if (k >= 1) { Destroy(r.t.gameObject); rings.RemoveAt(i); continue; }
                r.t.localScale = Vector3.one * r.size * (0.6f + GH.EaseOutCubic(k) * 1.2f);
                mpb.Clear();
                mpb.SetColor("_Color", r.c.WithA(1 - k));
                r.r.SetPropertyBlock(mpb);
                rings[i] = r;
            }
        }
    }
}
