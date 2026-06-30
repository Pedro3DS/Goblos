#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using UnityEngine;

namespace Neo.ConsolePlus
{
    internal static class NeoConsoleTimeBuiltInCommands
    {
        [NeoCommandRuntimeOnly("neo.time.info", "Shows Unity time settings and runtime timing values.")]
        private static string Info()
        {
            return "timeScale: " + Time.timeScale.ToString("0.###", CultureInfo.InvariantCulture) +
                   " | fixedDeltaTime: " + Time.fixedDeltaTime.ToString("0.####", CultureInfo.InvariantCulture) +
                   " | deltaTime: " + Time.deltaTime.ToString("0.####", CultureInfo.InvariantCulture) +
                   " | unscaledDeltaTime: " + Time.unscaledDeltaTime.ToString("0.####", CultureInfo.InvariantCulture);
        }

        [NeoCommandRuntimeOnly("neo.time.scale", "Sets Unity Time.timeScale. Use 1 for normal speed and 0 to pause.")]
        private static string Scale(float value)
        {
            float safeValue = Mathf.Max(0f, value);
            Time.timeScale = safeValue;
            return "Time.timeScale set to " + safeValue.ToString("0.###", CultureInfo.InvariantCulture) + ".";
        }

        [NeoCommandRuntimeOnly("neo.time.pause", "Pauses scaled Unity time by setting Time.timeScale to 0.")]
        private static string Pause()
        {
            Time.timeScale = 0f;
            return "Time paused. Time.timeScale = 0.";
        }

        [NeoCommandRuntimeOnly("neo.time.resume", "Resumes scaled Unity time by setting Time.timeScale to 1.")]
        private static string Resume()
        {
            Time.timeScale = 1f;
            return "Time resumed. Time.timeScale = 1.";
        }

        [NeoCommandRuntimeOnly("neo.time.fixed_delta", "Sets Unity Time.fixedDeltaTime.")]
        private static string FixedDelta(float value)
        {
            if (value <= 0f)
                return "Time.fixedDeltaTime must be greater than zero.";

            Time.fixedDeltaTime = value;
            return "Time.fixedDeltaTime set to " + value.ToString("0.####", CultureInfo.InvariantCulture) + ".";
        }
    }
}
#endif
