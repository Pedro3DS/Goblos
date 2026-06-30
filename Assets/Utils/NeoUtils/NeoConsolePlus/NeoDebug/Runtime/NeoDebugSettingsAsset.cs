using UnityEngine;

namespace Neo.Debugging
{
    /// <summary>
    /// Serialized settings container used by the editor and by development builds.
    ///
    /// Release builds do not need this asset because NeoDebug calls are stripped
    /// by Conditional attributes when neither UNITY_EDITOR nor DEVELOPMENT_BUILD is defined.
    /// </summary>
    internal sealed class NeoDebugSettingsAsset : ScriptableObject
    {
        public const bool DefaultEnabled = true;
        public const bool DefaultEnableInEditor = true;
        public const bool DefaultEnableInDevelopmentBuild = true;
        public const bool DefaultShowScriptPrefix = true;
        public const bool DefaultColorPrefix = true;
        public const bool DefaultPauseOnError = false;

        public static readonly Color DefaultLogPrefixColor = new Color(0.30f, 0.64f, 1f, 1f);
        public static readonly Color DefaultWarningPrefixColor = new Color(1f, 0.85f, 0.30f, 1f);
        public static readonly Color DefaultErrorPrefixColor = new Color(1f, 0.35f, 0.35f, 1f);

        [SerializeField] private bool enabled = DefaultEnabled;
        [SerializeField] private bool enableInEditor = DefaultEnableInEditor;
        [SerializeField] private bool enableInDevelopmentBuild = DefaultEnableInDevelopmentBuild;
        [SerializeField] private bool showScriptPrefix = DefaultShowScriptPrefix;
        [SerializeField] private bool colorPrefix = DefaultColorPrefix;
        [SerializeField] private bool pauseOnError = DefaultPauseOnError;
        [SerializeField] private Color logPrefixColor = DefaultLogPrefixColor;
        [SerializeField] private Color warningPrefixColor = DefaultWarningPrefixColor;
        [SerializeField] private Color errorPrefixColor = DefaultErrorPrefixColor;

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public bool EnableInEditor
        {
            get => enableInEditor;
            set => enableInEditor = value;
        }

        public bool EnableInDevelopmentBuild
        {
            get => enableInDevelopmentBuild;
            set => enableInDevelopmentBuild = value;
        }

        public bool ShowScriptPrefix
        {
            get => showScriptPrefix;
            set => showScriptPrefix = value;
        }

        public bool ColorPrefix
        {
            get => colorPrefix;
            set => colorPrefix = value;
        }

        public bool PauseOnError
        {
            get => pauseOnError;
            set => pauseOnError = value;
        }

        public Color LogPrefixColor
        {
            get => logPrefixColor;
            set => logPrefixColor = value;
        }

        public Color WarningPrefixColor
        {
            get => warningPrefixColor;
            set => warningPrefixColor = value;
        }

        public Color ErrorPrefixColor
        {
            get => errorPrefixColor;
            set => errorPrefixColor = value;
        }

        public void ResetToDefaults()
        {
            enabled = DefaultEnabled;
            enableInEditor = DefaultEnableInEditor;
            enableInDevelopmentBuild = DefaultEnableInDevelopmentBuild;
            showScriptPrefix = DefaultShowScriptPrefix;
            colorPrefix = DefaultColorPrefix;
            pauseOnError = DefaultPauseOnError;
            logPrefixColor = DefaultLogPrefixColor;
            warningPrefixColor = DefaultWarningPrefixColor;
            errorPrefixColor = DefaultErrorPrefixColor;
        }

        public void CopyFrom(NeoDebugSettingsAsset source)
        {
            if (source == null)
            {
                ResetToDefaults();
                return;
            }

            enabled = source.enabled;
            enableInEditor = source.enableInEditor;
            enableInDevelopmentBuild = source.enableInDevelopmentBuild;
            showScriptPrefix = source.showScriptPrefix;
            colorPrefix = source.colorPrefix;
            pauseOnError = source.pauseOnError;
            logPrefixColor = source.logPrefixColor;
            warningPrefixColor = source.warningPrefixColor;
            errorPrefixColor = source.errorPrefixColor;
        }
    }
}
