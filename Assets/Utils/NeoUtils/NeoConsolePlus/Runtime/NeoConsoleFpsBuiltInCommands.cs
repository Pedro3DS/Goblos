#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using UnityEngine;

namespace Neo.ConsolePlus
{
    internal static class NeoConsoleFpsBuiltInCommands
    {
        [NeoCommandRuntimeOnly("neo.fps.show", "Shows a lightweight FPS overlay.")]
        private static string Show()
        {
            NeoConsoleFpsOverlay.Show();
            return "FPS overlay shown.";
        }

        [NeoCommandRuntimeOnly("neo.fps.hide", "Hides the FPS overlay.")]
        private static string Hide()
        {
            NeoConsoleFpsOverlay.Hide();
            return "FPS overlay hidden.";
        }

        [NeoCommandRuntimeOnly("neo.fps.toggle", "Toggles the FPS overlay.")]
        private static string Toggle()
        {
            bool visible = NeoConsoleFpsOverlay.Toggle();
            return visible ? "FPS overlay shown." : "FPS overlay hidden.";
        }

        [NeoCommandRuntimeOnly("neo.fps.info", "Shows current FPS statistics.")]
        private static string Info()
        {
            NeoConsoleFpsOverlay.FpsStats stats = NeoConsoleFpsOverlay.GetStats();
            return "FPS current: " + stats.CurrentFps.ToString("0.0", CultureInfo.InvariantCulture) +
                   " | average: " + stats.AverageFps.ToString("0.0", CultureInfo.InvariantCulture) +
                   " | min: " + stats.MinFps.ToString("0.0", CultureInfo.InvariantCulture) +
                   " | max: " + stats.MaxFps.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class NeoConsoleFpsOverlay : MonoBehaviour
    {
        private const float SampleInterval = 0.25f;
        private const float StatsResetAfterSeconds = 2f;

        private static NeoConsoleFpsOverlay instance;
        private static GUIStyle style;
        private static readonly GUIContent Content = new GUIContent();

        private bool visible;
        private int frames;
        private float elapsed;
        private float currentFps;
        private float averageFps;
        private float minFps;
        private float maxFps;
        private float statsElapsed;
        private int samples;

        internal struct FpsStats
        {
            public float CurrentFps;
            public float AverageFps;
            public float MinFps;
            public float MaxFps;
        }

        internal static void Show()
        {
            EnsureCreated();
            if (instance != null)
                instance.visible = true;
        }

        internal static void Hide()
        {
            if (instance != null)
                instance.visible = false;
        }

        internal static bool Toggle()
        {
            EnsureCreated();
            if (instance == null)
                return false;

            instance.visible = !instance.visible;
            return instance.visible;
        }

        internal static FpsStats GetStats()
        {
            EnsureCreated();
            if (instance == null)
                return default(FpsStats);

            return new FpsStats
            {
                CurrentFps = instance.currentFps,
                AverageFps = instance.averageFps,
                MinFps = instance.minFps <= 0f ? instance.currentFps : instance.minFps,
                MaxFps = instance.maxFps
            };
        }

        private static void EnsureCreated()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif
            if (instance != null)
                return;

            GameObject go = new GameObject("NeoConsoleFpsOverlay");
            go.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<NeoConsoleFpsOverlay>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            ResetStats();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Destroy(gameObject);
                return;
            }
#endif
            float delta = Time.unscaledDeltaTime;
            frames++;
            elapsed += delta;
            statsElapsed += delta;

            if (statsElapsed > StatsResetAfterSeconds && samples == 0)
                ResetStats();

            if (elapsed < SampleInterval)
                return;

            currentFps = frames / Mathf.Max(0.0001f, elapsed);
            frames = 0;
            elapsed = 0f;

            if (samples == 0)
            {
                minFps = currentFps;
                maxFps = currentFps;
                averageFps = currentFps;
            }
            else
            {
                minFps = Mathf.Min(minFps, currentFps);
                maxFps = Mathf.Max(maxFps, currentFps);
                averageFps = ((averageFps * samples) + currentFps) / (samples + 1);
            }

            samples++;
            statsElapsed = 0f;
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyle();
            Content.text = "FPS: " + currentFps.ToString("0.0", CultureInfo.InvariantCulture) +
                           "  AVG: " + averageFps.ToString("0.0", CultureInfo.InvariantCulture) +
                           "  MIN: " + (minFps <= 0f ? currentFps : minFps).ToString("0.0", CultureInfo.InvariantCulture);

            Rect rect = new Rect(12f, 12f, 260f, 26f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, rect.height - 8f), Content, style);
        }

        private static void EnsureStyle()
        {
            if (style != null)
                return;

            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
        }

        private void ResetStats()
        {
            frames = 0;
            elapsed = 0f;
            currentFps = 0f;
            averageFps = 0f;
            minFps = 0f;
            maxFps = 0f;
            statsElapsed = 0f;
            samples = 0;
        }
    }
}
#endif
