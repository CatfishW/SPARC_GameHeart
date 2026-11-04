using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameHeart
{
    /// <summary>Scene entry point. Builds every system at runtime and starts the game flow.</summary>
    public class GameRoot : MonoBehaviour
    {
        public static GameRoot I;
        public Canvas Canvas { get; private set; }
        public RectTransform CanvasRect { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform HudLayer { get; private set; }
        public RectTransform ModalLayer { get; private set; }
        public RectTransform OverlayLayer { get; private set; }
        public StageWorld Stage { get; private set; }
        public RhythmGame Game { get; private set; }
        public Overlay Overlay { get; private set; }
        CanvasScaler scaler;

        void Awake()
        {
            I = this;
            Application.runInBackground = true;
            Input.simulateMouseWithTouches = false;
            Save.Load();
            Content.Init();
            UI.Init();

            gameObject.AddComponent<AudioDirector>();
            Stage = new GameObject("Stage").AddComponent<StageWorld>();
            Stage.Init();
            Game = new GameObject("Rhythm").AddComponent<RhythmGame>();
            Game.transform.SetParent(Stage.transform, false);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            var cgo = new GameObject("Canvas");
            cgo.layer = 5;
            Canvas = cgo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Canvas.worldCamera = Stage.Cam;
            Canvas.planeDistance = 1f;
            Canvas.sortingOrder = 10;
            scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cgo.AddComponent<GraphicRaycaster>();
            CanvasRect = (RectTransform)cgo.transform;
            ScreenLayer = UI.Fill(UI.Node("Screens", cgo.transform));
            HudLayer = UI.Fill(UI.Node("HUD", cgo.transform));
            ModalLayer = UI.Fill(UI.Node("Modals", cgo.transform));
            OverlayLayer = UI.Fill(UI.Node("Overlay", cgo.transform));
            Overlay = OverlayLayer.gameObject.AddComponent<Overlay>();
            Overlay.Build(OverlayLayer);
            UpdateScaler();
        }

        void Start()
        {
            StartCoroutine(new GameFlow().Run());
        }

        void Update() { UpdateScaler(); }

        void UpdateScaler()
        {
            float aspect = UnityEngine.Screen.width / (float)Mathf.Max(1, UnityEngine.Screen.height);
            scaler.matchWidthOrHeight = aspect >= 16f / 9f ? 1f : 0f;
            // phones in landscape: render the UI ~12% larger so small labels stay readable
            bool phone = (Application.isMobilePlatform || Input.touchSupported) && UnityEngine.Screen.height <= 900 && aspect >= 2.0f;
            scaler.referenceResolution = phone ? new Vector2(1920f * 960f / 1080f, 960f) : new Vector2(1920, 1080);
        }
    }
}
