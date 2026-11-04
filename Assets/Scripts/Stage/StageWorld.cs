using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameHeart
{
    /// <summary>Per-chapter look of the 3D stage.</summary>
    public class StageTheme
    {
        public Color ringA, veinB, fog, tint, cell, cellRim, particle, heart;
        public float exposure = 1f, bloom = 1f, saturation = 10f, contrast = 12f, ca = 0.08f, cellSpeed = 1f, particleRate = 30f;
        public int cells = 48;
        public string particles = "motes";
        public bool germs;

        public static StageTheme For(string theme)
        {
            var t = new StageTheme();
            switch (theme)
            {
                case "cold":
                    t.ringA = new Color(0.45f, 1.2f, 2.4f); t.veinB = new Color(0.6f, 1.6f, 2.0f); t.fog = new Color(0.04f, 0.09f, 0.16f);
                    t.tint = new Color(0.92f, 1.0f, 1.12f); t.cell = new Color(0.65f, 0.12f, 0.2f); t.cellRim = new Color(0.6f, 1.2f, 1.8f);
                    t.particle = new Color(0.85f, 0.95f, 1.2f); t.heart = new Color(0.4f, 1.1f, 2.2f); t.particles = "snow"; t.particleRate = 60; t.saturation = 4;
                    break;
                case "fire":
                    t.ringA = new Color(2.6f, 0.9f, 0.25f); t.veinB = new Color(2.2f, 0.55f, 0.2f); t.fog = new Color(0.16f, 0.05f, 0.02f);
                    t.tint = new Color(1.12f, 1.0f, 0.9f); t.cell = new Color(0.95f, 0.12f, 0.1f); t.cellRim = new Color(2.2f, 0.9f, 0.3f);
                    t.particle = new Color(2.4f, 1.1f, 0.35f); t.heart = new Color(2.4f, 0.8f, 0.25f); t.particles = "embers"; t.particleRate = 70; t.cellSpeed = 1.9f; t.cells = 64;
                    t.bloom = 1.15f; t.saturation = 18;
                    break;
                case "toxic":
                    t.ringA = new Color(0.6f, 2.2f, 0.7f); t.veinB = new Color(1.6f, 0.3f, 0.6f); t.fog = new Color(0.04f, 0.1f, 0.05f);
                    t.tint = new Color(0.95f, 1.08f, 0.95f); t.cell = new Color(0.8f, 0.1f, 0.14f); t.cellRim = new Color(0.8f, 2.0f, 0.8f);
                    t.particle = new Color(0.8f, 2.0f, 0.6f); t.heart = new Color(0.6f, 2.0f, 0.8f); t.particles = "spores"; t.germs = true; t.particleRate = 40; t.cellSpeed = 1.4f;
                    t.ca = 0.14f;
                    break;
                case "night":
                    t.ringA = new Color(0.9f, 0.7f, 2.4f); t.veinB = new Color(0.5f, 0.45f, 1.6f); t.fog = new Color(0.04f, 0.03f, 0.1f);
                    t.tint = new Color(0.95f, 0.95f, 1.12f); t.cell = new Color(0.55f, 0.1f, 0.2f); t.cellRim = new Color(0.9f, 0.7f, 2.0f);
                    t.particle = new Color(1.2f, 1.1f, 2.0f); t.heart = new Color(1.0f, 0.75f, 2.4f); t.particles = "stars"; t.particleRate = 25; t.cellSpeed = 0.5f; t.cells = 28;
                    t.exposure = 0.95f; t.bloom = 1.1f; t.saturation = 6;
                    break;
                case "finale":
                    t.ringA = new Color(2.4f, 0.6f, 2.0f); t.veinB = new Color(2.2f, 1.6f, 0.5f); t.fog = new Color(0.1f, 0.03f, 0.1f);
                    t.tint = new Color(1.05f, 1.0f, 1.05f); t.cell = new Color(0.95f, 0.12f, 0.2f); t.cellRim = new Color(2.2f, 1.4f, 1.8f);
                    t.particle = new Color(2.2f, 1.6f, 2.2f); t.heart = new Color(2.6f, 0.8f, 1.8f); t.particles = "prism"; t.particleRate = 60; t.cellSpeed = 1.4f; t.cells = 60;
                    t.bloom = 1.25f; t.saturation = 20; t.ca = 0.1f;
                    break;
                default:
                    t.ringA = new Color(2.4f, 0.45f, 0.65f); t.veinB = new Color(1.8f, 0.5f, 0.6f); t.fog = new Color(0.12f, 0.02f, 0.04f);
                    t.tint = new Color(1.05f, 0.98f, 1f); t.cell = new Color(0.9f, 0.1f, 0.14f); t.cellRim = new Color(2.0f, 0.55f, 0.6f);
                    t.particle = new Color(2.0f, 0.8f, 0.9f); t.heart = new Color(2.6f, 0.45f, 0.7f); t.particles = "motes";
                    break;
            }
            return t;
        }
    }

    /// <summary>
    /// The 3D world behind the highway: painted backdrop, beat tunnel, holographic heart, drifting blood cells,
    /// themed particles, URP post-processing and the camera rig. Publishes shader globals every frame.
    /// </summary>
    public class StageWorld : MonoBehaviour
    {
        public static StageWorld I;
        public Camera Cam { get; private set; }
        public Volume Volume { get; private set; }
        Bloom bloom; Vignette vignette; ChromaticAberration chroma; ColorAdjustments color; FilmGrain grain; LensDistortion lens;

        Transform world, backdrop, tunnel, heartRoot;
        Material backdropMat, tunnelMat, cellMat, germMat;
        readonly List<Material> heartMats = new List<Material>();
        ParticleSystem ambient;
        Material ambientMat;
        readonly List<(Transform t, Vector3 spin, float speed)> cells = new List<(Transform, Vector3, float)>();
        readonly List<(Transform t, Vector3 spin, float speed)> germs = new List<(Transform, Vector3, float)>();
        StageTheme theme = StageTheme.For("warm");
        float beat, hit, scroll, energy, fovKick, shake, heartPulse, missFlash, surge;
        float bpm = 120;
        bool playing;
        Vector3 camBase = new Vector3(0, 5.1f, -6.9f);
        Vector3 camLook = new Vector3(0, 0, 17f);
        Texture2D currentBackdrop;

        static readonly int IdTime = Shader.PropertyToID("_GH_Time"), IdBeat = Shader.PropertyToID("_GH_Beat"), IdHit = Shader.PropertyToID("_GH_Hit"),
            IdEnergy = Shader.PropertyToID("_GH_Energy"), IdScroll = Shader.PropertyToID("_GH_Scroll"), IdA = Shader.PropertyToID("_GH_ColorA"),
            IdB = Shader.PropertyToID("_GH_ColorB"), IdFog = Shader.PropertyToID("_GH_Fog"), IdFogS = Shader.PropertyToID("_GH_FogStart"), IdFogE = Shader.PropertyToID("_GH_FogEnd");

        public void Init()
        {
            I = this;
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Color.black;
            Cam.fieldOfView = 60;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 400;
            Cam.allowHDR = true;
            Cam.allowMSAA = true;
            var data = Cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            camGo.AddComponent<AudioListener>();
            Cam.transform.position = camBase;
            Cam.transform.LookAt(camLook);

            var volGo = new GameObject("Post");
            volGo.transform.SetParent(transform, false);
            Volume = volGo.AddComponent<Volume>();
            Volume.isGlobal = true;
            Volume.priority = 10;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(1f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(false);
            var tm = p.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.None);
            color = p.Add<ColorAdjustments>(true);
            color.postExposure.Override(0f);
            color.saturation.Override(10f);
            color.contrast.Override(10f);
            vignette = p.Add<Vignette>(true);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(Color.black);
            chroma = p.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0.06f);
            grain = p.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);
            lens = p.Add<LensDistortion>(true);
            lens.intensity.Override(0f);
            Volume.profile = p;

            world = new GameObject("World").transform;
            world.SetParent(transform, false);
            BuildBackdrop();
            BuildTunnel();
            BuildHeart();
            BuildCells();
            BuildAmbient();
            world.gameObject.SetActive(false);
            Save.SettingsChanged += ApplyQuality;
            ApplyQuality();
        }

        void OnDestroy() { Save.SettingsChanged -= ApplyQuality; }

        void ApplyQuality()
        {
            int lvl = Save.S.highQuality ? 1 : 0;
            if (QualitySettings.GetQualityLevel() != lvl && lvl < QualitySettings.names.Length) QualitySettings.SetQualityLevel(lvl, true);
            grain.active = Save.S.highQuality;
        }

        MeshRenderer Make(string n, Transform parent, Mesh m, Material mat)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        void BuildBackdrop()
        {
            backdropMat = new Material(Shader.Find("GameHeart/Backdrop")) { name = "Backdrop" };
            backdrop = Make("Backdrop", world, Meshes.Quad(false), backdropMat).transform;
        }

        void FitBackdrop()
        {
            if (backdrop == null) return;
            float d = 190f;
            var camT = Cam.transform;
            backdrop.position = camT.position + camT.forward * d;
            backdrop.rotation = Quaternion.LookRotation(camT.forward, camT.up);
            float h = 2 * d * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.1f;
            float w = h * Cam.aspect;
            float img = currentBackdrop != null ? currentBackdrop.width / (float)currentBackdrop.height : 16f / 9f;
            float qh = Mathf.Max(h, w / img);
            backdrop.localScale = new Vector3(qh * img, qh, 1);
        }

        void BuildTunnel()
        {
            tunnelMat = new Material(Shader.Find("GameHeart/Tunnel")) { name = "Tunnel" };
            var r = Make("Tunnel", world, Meshes.Tube(12f, 180f, 72, 60), tunnelMat);
            tunnel = r.transform;
            tunnel.localPosition = new Vector3(0, 4.2f, -12f);
        }

        void BuildHeart()
        {
            heartRoot = new GameObject("Heart").transform;
            heartRoot.SetParent(world, false);
            var model = Resources.Load<GameObject>("Models/Heart");
            if (model == null) return;
            var inst = Instantiate(model, heartRoot);
            inst.name = "HeartModel";
            var holo = Shader.Find("GameHeart/Hologram");
            foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>())
            {
                var mat = new Material(holo);
                string part = mr.name.ToUpperInvariant();
                var tex = Resources.Load<Texture2D>("Models/Heart_" + (part.Contains("VEIN") ? "VEINS" : part.Contains("PULM") ? "PULMONARY" : "ARTERIES") + "_C");
                if (tex != null) mat.SetTexture("_MainTex", tex);
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                heartMats.Add(mat);
            }
            // normalise size & centre
            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float s = 15f / Mathf.Max(0.001f, b.size.y);
                inst.transform.localScale *= s;
                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                inst.transform.position -= b.center - heartRoot.position;
            }
            heartRoot.localPosition = new Vector3(0, 15f, 115f);
        }

        void BuildCells()
        {
            cellMat = new Material(Shader.Find("GameHeart/Cell")) { name = "Cell", enableInstancing = true };
            germMat = new Material(Shader.Find("GameHeart/Cell")) { name = "Germ" };
            germMat.SetColor("_Color", new Color(0.25f, 0.9f, 0.3f));
            germMat.SetColor("_Rim", new Color(1.2f, 2.4f, 0.8f));
            var rbc = Meshes.BloodCell();
            for (int i = 0; i < 70; i++)
            {
                var r = Make("Cell", world, rbc, cellMat);
                var t = r.transform;
                Respawn(t, Random.Range(-5f, 140f));
                cells.Add((t, Random.onUnitSphere * Random.Range(20f, 70f), Random.Range(0.8f, 1.3f)));
            }
            var gm = Meshes.Germ();
            for (int i = 0; i < 16; i++)
            {
                var r = Make("Germ", world, gm, germMat);
                Respawn(r.transform, Random.Range(-5f, 140f));
                r.transform.localScale = Vector3.one * Random.Range(0.9f, 1.8f);
                germs.Add((r.transform, Random.onUnitSphere * Random.Range(15f, 50f), Random.Range(0.7f, 1.1f)));
            }
        }

        void Respawn(Transform t, float z)
        {
            // stay out of the highway corridor
            Vector2 p;
            do { p = Random.insideUnitCircle * 10.5f; }
            while ((Mathf.Abs(p.x) < 5.6f && p.y + 4.2f < 3.6f) || p.magnitude < 5.5f);
            t.localPosition = new Vector3(p.x, p.y + 4.2f, z);
            t.localRotation = Random.rotation;
            float s = Random.Range(0.55f, 1.1f);
            t.localScale = new Vector3(s, s, s);
        }

        void BuildAmbient()
        {
            var go = new GameObject("Ambient");
            go.transform.SetParent(world, false);
            go.transform.localPosition = new Vector3(0, 4, 120);
            ambient = go.AddComponent<ParticleSystem>();
            ambient.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ambient.main;
            main.loop = true;
            main.startLifetime = 6f;
            main.startSpeed = 0;
            main.maxParticles = 900;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
            var shape = ambient.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30, 18, 8);
            var vel = ambient.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.z = new ParticleSystem.MinMaxCurve(-26f, -18f);
            vel.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.6f, 0.4f);
            var col = ambient.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            ambientMat = new Material(Shader.Find("GameHeart/Additive")) { mainTexture = StageTex.Snow };
            rend.material = ambientMat;
            ambient.Play();
        }

        // ------------------------------------------------------------------ public API
        public void Show(bool on)
        {
            world.gameObject.SetActive(on);
            playing = on;
        }

        public void SetTheme(Chapter ch, Texture2D backdropTex)
        {
            theme = StageTheme.For(ch != null ? ch.theme : "warm");
            bpm = ch != null ? ch.bpmHint : 120;
            currentBackdrop = backdropTex;
            backdropMat.SetTexture("_MainTex", backdropTex);
            backdropMat.SetTexture("_MainTex2", backdropTex);
            backdropMat.SetFloat("_Blend", 0);
            backdropMat.SetColor("_Tint", theme.tint);
            backdropMat.SetFloat("_Exposure", 0.82f * theme.exposure);
            cellMat.SetColor("_Color", theme.cell);
            cellMat.SetColor("_Rim", theme.cellRim);
            foreach (var m in heartMats) { m.SetColor("_Color", theme.heart * 0.8f); m.SetColor("_Rim", theme.heart * 1.4f); }
            foreach (var gm in germs) gm.t.gameObject.SetActive(theme.germs);
            for (int i = 0; i < cells.Count; i++) cells[i].t.gameObject.SetActive(i < theme.cells);
            var main = ambient.main;
            main.startColor = theme.particle;
            var em = ambient.emission;
            em.rateOverTime = theme.particleRate * (Save.S.highQuality ? 1f : 0.5f);
            switch (theme.particles)
            {
                case "snow": main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.4f); ambientMat.mainTexture = StageTex.Snow; break;
                case "embers": main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f); ambientMat.mainTexture = StageTex.Spark; break;
                case "stars": main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.35f); ambientMat.mainTexture = StageTex.Spark; break;
                case "prism": main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f); ambientMat.mainTexture = StageTex.Spark; break;
                case "spores": main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f); ambientMat.mainTexture = StageTex.Glow; break;
                default: main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f); ambientMat.mainTexture = StageTex.Glow; break;
            }
            ambientMat.SetColor("_Color", Color.white * 0.9f);
            bloom.intensity.Override(theme.bloom);
            color.saturation.Override(theme.saturation);
            color.contrast.Override(theme.contrast);
            Shader.SetGlobalColor(IdA, theme.ringA);
            Shader.SetGlobalColor(IdB, theme.veinB);
            Shader.SetGlobalColor(IdFog, theme.fog);
            Shader.SetGlobalFloat(IdFogS, 30f);
            Shader.SetGlobalFloat(IdFogE, 150f);
            ambient.Clear();
            ambient.Simulate(5f, true, true);
            ambient.Play();
        }

        public void OnBeat(bool downbeat) { beat = Mathf.Max(beat, downbeat ? 1f : 0.65f); heartPulse = Mathf.Max(heartPulse, downbeat ? 0.6f : 0.35f); }

        public void OnJudge(Judgment j, NoteType type)
        {
            if (j == Judgment.Miss)
            {
                missFlash = 1; shake = Mathf.Max(shake, 0.35f);
                return;
            }
            hit = Mathf.Max(hit, j == Judgment.Perfect ? 1f : 0.7f);
            heartPulse = 1f;
            if (type == NoteType.Pulse) { fovKick = 1f; shake = Mathf.Max(shake, 0.5f); }
        }

        public void ComboBurst() { fovKick = Mathf.Max(fovKick, 0.7f); hit = 1; }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            var game = RhythmGame.I;
            float t = game != null && game.Playing ? game.SongTime : Time.unscaledTime;
            beat = Mathf.Max(0, beat - dt * 4.5f);
            hit = Mathf.Max(0, hit - dt * 5f);
            heartPulse = Mathf.Max(0, heartPulse - dt * 3.5f);
            fovKick = Mathf.Max(0, fovKick - dt * 3f);
            shake = Mathf.Max(0, shake - dt * 3f);
            missFlash = Mathf.Max(0, missFlash - dt * 3f);
            surge = GH.Damp(surge, game != null && game.Playing && game.Surge ? 1 : 0, 3, dt);
            float targetEnergy = game != null && game.Playing && game.Chart != null ? game.Chart.EnergyAt(game.SongTime) : 0.4f;
            energy = GH.Damp(energy, targetEnergy, 4, dt);
            float speed = (14f + bpm * 0.12f) * theme.cellSpeed * (0.7f + energy * 0.6f) * (1 + surge * 0.4f);
            scroll += dt * speed;

            Shader.SetGlobalFloat(IdTime, t);
            Shader.SetGlobalFloat(IdBeat, Save.S.reduceFlash ? beat * 0.4f : beat);
            Shader.SetGlobalFloat(IdHit, hit);
            Shader.SetGlobalFloat(IdEnergy, Mathf.Clamp01(energy + surge * 0.4f));
            Shader.SetGlobalFloat(IdScroll, scroll);
            tunnelMat.SetFloat("_Intensity", 0.55f + energy * 0.5f + surge * 0.5f);
            tunnelMat.SetFloat("_RingSpacing", Mathf.Max(6f, speed * 60f / Mathf.Max(60, bpm)));

            if (!playing) return;

            // camera rig
            float sway = Mathf.Sin(Time.unscaledTime * 0.35f) * 0.25f;
            var pos = camBase + new Vector3(sway, Mathf.Sin(Time.unscaledTime * 0.5f) * 0.12f - beat * 0.06f, 0);
            pos += Random.insideUnitSphere * shake * 0.08f;
            Cam.transform.position = pos;
            Cam.transform.LookAt(camLook + new Vector3(sway * 0.4f, 0, 0));
            Cam.fieldOfView = 60f + fovKick * 4.5f + surge * 2.5f - beat * 0.4f;

            // post fx
            float flashScale = Save.S.reduceFlash ? 0.35f : 1f;
            chroma.intensity.Override(theme.ca * 0.5f + fovKick * 0.1f * flashScale + missFlash * 0.08f * flashScale);
            bloom.intensity.Override(theme.bloom * (1 + beat * 0.18f * flashScale + hit * 0.12f * flashScale + surge * 0.3f));
            vignette.intensity.Override(0.3f + missFlash * 0.18f);
            vignette.color.Override(Color.Lerp(Color.black, new Color(0.6f, 0, 0.05f), missFlash * 0.8f));
            lens.intensity.Override(-fovKick * 0.12f * flashScale);
            color.postExposure.Override(-0.05f + hit * 0.08f * flashScale);

            FitBackdrop();

            // heart
            if (heartRoot != null)
            {
                float hp = heartPulse;
                heartRoot.localScale = Vector3.one * (1f + hp * 0.07f);
                heartRoot.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.unscaledTime * 0.3f) * 25f + 180f, Mathf.Sin(Time.unscaledTime * 0.21f) * 4f);
                foreach (var m in heartMats) { m.SetFloat("_Pulse", hp); m.SetFloat("_Alpha", 0.75f + surge * 0.25f); }
            }

            // cells
            float cs = speed * 0.9f;
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (!c.t.gameObject.activeSelf) continue;
                c.t.localPosition += new Vector3(0, 0, -cs * c.speed * dt);
                c.t.Rotate(c.spin * dt, Space.Self);
                if (c.t.localPosition.z < -10) Respawn(c.t, Random.Range(120f, 150f));
            }
            for (int i = 0; i < germs.Count; i++)
            {
                var c = germs[i];
                if (!c.t.gameObject.activeSelf) continue;
                c.t.localPosition += new Vector3(Mathf.Sin(Time.time + i) * dt, 0, -cs * 0.7f * c.speed * dt);
                c.t.Rotate(c.spin * dt, Space.Self);
                if (c.t.localPosition.z < -10) Respawn(c.t, Random.Range(120f, 150f));
            }
            var vel = ambient.velocityOverLifetime;
            vel.z = new ParticleSystem.MinMaxCurve(-speed * 1.3f, -speed * 0.9f);
        }

        public Vector2 WorldToCanvas(Vector3 world, RectTransform canvas)
        {
            var sp = Cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, sp, Cam, out var lp);
            return lp;
        }
    }
}
