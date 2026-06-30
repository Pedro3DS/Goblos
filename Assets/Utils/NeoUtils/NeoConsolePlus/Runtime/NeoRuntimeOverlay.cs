#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace Neo.ConsolePlus
{
    internal sealed class NeoRuntimeOverlay : MonoBehaviour
    {
        private const int MaxVisibleLogs = 120;
        private const int MaxVisibleSuggestions = 6;
        private const float Margin = 12f;
        private const float InputMinHeight = 32f;
        private const float InputMaxHeight = 96f;
        private const float ClearButtonWidth = 54f;
        private const float ClearButtonHeight = 24f;
        private const float SuggestionRowHeight = 22f;
        private const float SuggestionHintHeight = 6f;
        private const float SuggestionOverlayGap = 2f;
        private const float SuggestionRowInset = 3f;
        private const float RuntimeGhostTypedOffset = -10f;
        private const float PanelMinWidth = 280f;
        private const float PanelMinHeight = 170f;

        private static NeoRuntimeOverlay instance;

        internal static bool OpenOnError { get; set; } = true;

        private bool visible;
        private bool commandInputFocused;
        private bool requestCommandInputFocus;
        private int commandInputFocusFrames;
        private int pendingCommandCursorIndex = -1;
        private int pendingCommandCursorApplyFrames;
        private bool pendingCommandInputStateSync;
        private int selectedSuggestionIndex;
        private int hoveredSuggestionIndex = -1;
        private bool suggestionMouseHoverActive;
        private bool suggestionMouseHoverSuppressedUntilMove;
        private bool hasLastSuggestionMousePosition;
        private Vector2 lastSuggestionMousePosition;
        private int commandHistoryIndex = -1;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private Rect panelRect;
        private Rect inputRectLocal;
        private Vector2 logScroll;
        private bool runtimeLogAutoScroll = true;
        private int lastRuntimeLogCount = -1;
        private float lastRuntimeMaxScrollY;
        private float lastTabCompletionTime = -1f;
        private string commandInput = string.Empty;
        private string previousCommandInput = string.Empty;
        private string lastCommandResult = string.Empty;
        private string commandHistoryDraft = string.Empty;
        private int lastKnownCommandCursorIndex;
        private bool hasLastKnownCommandCursorIndex;
        private readonly System.Collections.Generic.List<NeoConsoleLogEntry> runtimeLogSnapshot = new System.Collections.Generic.List<NeoConsoleLogEntry>(128);
        private readonly System.Collections.Generic.List<float> runtimeLogRowHeights = new System.Collections.Generic.List<float>(128);
        private static readonly GUIContent RuntimeTempContent = new GUIContent();
        private static GUISkin runtimeCachedSkin;
        private static int runtimeCachedScreenHeight;
        private static GUIStyle runtimePanelStyle;
        private static GUIStyle runtimeLogBoxStyle;
        private static GUIStyle runtimeLogStyle;
        private static GUIStyle runtimeCommandLogStyle;
        private static GUIStyle runtimeWarningLogStyle;
        private static GUIStyle runtimeErrorLogStyle;
        private static GUIStyle runtimeButtonStyle;
        private static GUIStyle runtimeTextFieldStyle;
        private static GUIStyle runtimeInlineGhostPlainStyle;
        private static GUIStyle runtimeInlineGhostRichStyle;
        private static GUIStyle runtimeGhostStyle;
        private static GUIStyle runtimeSuggestionBoxStyle;
        private static GUIStyle runtimeSuggestionStyle;
        private static GUIStyle runtimeSuggestionSelectedStyle;
        private static GUIStyle runtimeSuggestionDescriptionStyle;
        private static GUIStyle runtimeHintStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
#if NEO_CONSOLEPLUS_DISABLE_RUNTIME_OVERLAY
            return;
#else
            EnsureCreated();
#endif
        }

        internal static void EnsureCreated()
        {
            if (instance != null)
                return;

            GameObject overlayObject = new GameObject("NeoRuntimeOverlay");
            overlayObject.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(overlayObject);
            instance = overlayObject.AddComponent<NeoRuntimeOverlay>();
        }

        internal static void Show()
        {
            EnsureCreated();
            if (instance != null)
                instance.SetVisible(true);
        }

        internal static void Hide()
        {
            if (instance != null)
                instance.SetVisible(false);
        }

        internal static void Toggle()
        {
            EnsureCreated();
            if (instance != null)
                instance.SetVisible(!instance.visible);
        }

        private void SetVisible(bool value)
        {
            visible = value;
            NeoConsoleLogBuffer.SetNormalLogCaptureOwner(this, visible);
            if (!visible)
                return;

            ResetPanelRectIfScreenChanged();
            ClampPanelToScreen();
            RequestCommandInputFocus();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            ResetPanelRect();
            NeoConsoleLogBuffer.EnsureListening();
            NeoConsoleLogBuffer.SetNormalLogCaptureOwner(this, visible);
            NeoConsoleLogBuffer.EntryAdded += OnRuntimeLogEntryAdded;
            // Command registry initialization is lazy and will happen when autocomplete or execution needs it.
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;

            NeoConsoleLogBuffer.EntryAdded -= OnRuntimeLogEntryAdded;
            NeoConsoleLogBuffer.SetNormalLogCaptureOwner(this, false);
        }

        private void OnRuntimeLogEntryAdded(NeoConsoleLogEntry entry)
        {
            if (!OpenOnError || entry == null || visible || !IsError(entry.Type))
                return;

            SetVisible(true);
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ForceDestroyEditorInstances();
                return;
            }
#endif

            if (!NeoRuntimeOverlayShortcut.IsTogglePressed())
                return;

            if (visible && IsCommandFieldActive())
                return;

            SetVisible(!visible);
        }

        private void OnGUI()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif

            if (!visible)
                return;

            ResetPanelRectIfScreenChanged();
            ClampPanelToScreen();
            HandleCommandInputKeys();

            DrawRuntimeRect(panelRect, new Color(0f, 0f, 0f, 0.72f));
            GUI.Box(panelRect, GUIContent.none, RuntimePanelStyle());
            GUI.BeginGroup(panelRect);

            float width = panelRect.width;
            float height = panelRect.height;
            Rect clearRect = new Rect(Mathf.Max(8f, width - ClearButtonWidth - 8f), 8f, ClearButtonWidth, ClearButtonHeight);
            float inputWidth = Mathf.Max(80f, width - 16f);
            float inputHeight = GetRuntimeCommandInputHeight(inputWidth);
            inputRectLocal = new Rect(8f, height - inputHeight - 8f, inputWidth, inputHeight);
            Rect logsRect = new Rect(8f, clearRect.yMax + 6f, Mathf.Max(80f, width - 16f), Mathf.Max(46f, inputRectLocal.y - clearRect.yMax - 10f));

            DrawRuntimeClearButton(clearRect);
            DrawLogsArea(logsRect);
            DrawCommandInputArea(inputRectLocal);
            DrawSuggestionOverlay(inputRectLocal);
            ConsumeCurrentEventWhileTyping();

            GUI.EndGroup();
        }

        private void DrawRuntimeClearButton(Rect rect)
        {
            if (GUI.Button(rect, "Clear", RuntimeButtonStyle()))
            {
                runtimeLogAutoScroll = true;
                logScroll = Vector2.zero;
                NeoConsoleLogBuffer.Clear();
            }
        }

        private void DrawLogsArea(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, RuntimeLogBoxStyle());

            Rect inner = new Rect(rect.x + 6f, rect.y + 6f, Mathf.Max(40f, rect.width - 12f), Mathf.Max(40f, rect.height - 12f));
            NeoConsoleLogBuffer.CopySnapshot(runtimeLogSnapshot);
            int start = Mathf.Max(0, runtimeLogSnapshot.Count - MaxVisibleLogs);
            float viewWidth = Mathf.Max(40f, inner.width - 16f);
            CalculateRuntimeLogRowHeights(runtimeLogSnapshot, start, viewWidth, runtimeLogRowHeights);
            float contentHeight = Mathf.Max(inner.height, SumRuntimeLogRowHeights(runtimeLogRowHeights) + 4f);
            Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);
            float maxScrollY = Mathf.Max(0f, contentHeight - inner.height);

            HandleRuntimeLogScrollIntent(inner, maxScrollY);

            if (runtimeLogAutoScroll && Event.current.type == EventType.Repaint)
                logScroll.y = maxScrollY;
            else
                logScroll.y = Mathf.Clamp(logScroll.y, 0f, maxScrollY);

            logScroll = GUI.BeginScrollView(inner, logScroll, viewRect);
            float y = 0f;
            int heightIndex = 0;
            for (int i = start; i < runtimeLogSnapshot.Count; i++)
            {
                NeoConsoleLogEntry entry = runtimeLogSnapshot[i];
                if (entry == null)
                    continue;

                float rowHeight = heightIndex < runtimeLogRowHeights.Count ? runtimeLogRowHeights[heightIndex] : GetRuntimeLogMinRowHeight();
                Rect row = new Rect(0f, y, viewRect.width, rowHeight);
                GUIStyle rowStyle = RuntimeLogStyle(entry);
                string formattedMessage = FormatRuntimeLog(entry);
                string rowMessage = TruncateRuntimeLogToMaxLines(formattedMessage, rowStyle, row.width, 2);
                RuntimeTempContent.text = rowMessage;
                RuntimeTempContent.tooltip = formattedMessage;
                GUI.Label(row, RuntimeTempContent, rowStyle);
                y += rowHeight;
                heightIndex++;
            }
            GUI.EndScrollView();

            UpdateRuntimeLogAutoScrollState(inner, runtimeLogSnapshot.Count, maxScrollY);
        }

        private static void CalculateRuntimeLogRowHeights(System.Collections.Generic.List<NeoConsoleLogEntry> entries, int start, float width, System.Collections.Generic.List<float> heights)
        {
            if (heights == null)
                return;

            heights.Clear();
            if (entries == null || entries.Count <= start)
                return;

            for (int i = start; i < entries.Count; i++)
                heights.Add(CalculateRuntimeLogRowHeight(entries[i], width));
        }

        private static float SumRuntimeLogRowHeights(System.Collections.Generic.List<float> heights)
        {
            float total = 0f;
            if (heights == null)
                return total;

            for (int i = 0; i < heights.Count; i++)
                total += heights[i];

            return total;
        }

        private static float CalculateRuntimeLogRowHeight(NeoConsoleLogEntry entry, float width)
        {
            GUIStyle style = RuntimeLogStyle(entry);
            string message = TruncateRuntimeLogToMaxLines(FormatRuntimeLog(entry), style, Mathf.Max(40f, width), 2);
            float contentHeight = style.CalcHeight(new GUIContent(message), Mathf.Max(40f, width));
            float maxHeight = GetRuntimeLogMaxRowHeight();
            return Mathf.Clamp(contentHeight + 2f, GetRuntimeLogMinRowHeight(), maxHeight);
        }

        private static float GetRuntimeLogMinRowHeight()
        {
            return Mathf.Clamp(Screen.height / 42f, 17f, 24f);
        }

        private static float GetRuntimeLogMaxRowHeight()
        {
            GUIStyle style = RuntimeLogStyle(null);
            return Mathf.Max(GetRuntimeLogMinRowHeight(), style.lineHeight * 2f + 4f);
        }

        private void HandleRuntimeLogScrollIntent(Rect inner, float maxScrollY)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.ScrollWheel || !inner.Contains(current.mousePosition))
                return;

            if (maxScrollY <= 0f)
            {
                runtimeLogAutoScroll = true;
                return;
            }

            float projectedY = Mathf.Clamp(logScroll.y + current.delta.y * 16f, 0f, maxScrollY);
            runtimeLogAutoScroll = IsRuntimeLogScrollAtBottom(projectedY, maxScrollY);
        }

        private void UpdateRuntimeLogAutoScrollState(Rect inner, int entryCount, float maxScrollY)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (maxScrollY <= 0f)
            {
                runtimeLogAutoScroll = true;
                logScroll.y = 0f;
            }
            else if (IsRuntimeLogScrollAtBottom(logScroll.y, maxScrollY))
            {
                runtimeLogAutoScroll = true;
                logScroll.y = maxScrollY;
            }
            else if (entryCount != lastRuntimeLogCount)
            {
                runtimeLogAutoScroll = false;
                logScroll.y = Mathf.Clamp(logScroll.y, 0f, maxScrollY);
            }

            lastRuntimeLogCount = entryCount;
            lastRuntimeMaxScrollY = maxScrollY;
        }

        private static bool IsRuntimeLogScrollAtBottom(float scrollY, float maxScrollY)
        {
            return maxScrollY <= 0f || scrollY >= maxScrollY - 3f;
        }

        private void DrawCommandInputArea(Rect inputRect)
        {
            if (Event.current.type == EventType.MouseDown && inputRect.Contains(Event.current.mousePosition))
                RequestCommandInputFocus();

            PrimePendingCommandCursorBeforeInput();

            GUI.SetNextControlName("NeoRuntimeOverlayCommand");
            string newInput = GUI.TextArea(inputRect, commandInput, RuntimeTextFieldStyle());
            newInput = SanitizeCommandInput(newInput);
            commandInputFocused = GUI.GetNameOfFocusedControl() == "NeoRuntimeOverlayCommand";

            bool isApplyingProgrammaticCompletion = pendingCommandInputStateSync || pendingCommandCursorApplyFrames > 0;
            if (!isApplyingProgrammaticCompletion && newInput != commandInput)
            {
                commandInput = newInput;
                if (commandInput != previousCommandInput)
                {
                    selectedSuggestionIndex = 0;
                    suggestionMouseHoverActive = false;
                    hoveredSuggestionIndex = -1;
                    commandHistoryIndex = -1;
                    commandHistoryDraft = string.Empty;
                    pendingCommandCursorIndex = -1;
                    pendingCommandCursorApplyFrames = 0;
                    pendingCommandInputStateSync = false;
                    hasLastKnownCommandCursorIndex = false;
                }

                previousCommandInput = commandInput;
            }

            DrawInlineCommandSuggestion(inputRect);

            if (requestCommandInputFocus || commandInputFocusFrames > 0)
            {
                FocusRuntimeInputNow();
                requestCommandInputFocus = false;
                if (commandInputFocusFrames > 0)
                    commandInputFocusFrames--;
            }

            ApplyPendingCommandCursor();
            UpdateLastKnownCommandCursorIndex();
        }

        private float GetRuntimeCommandInputHeight(float inputWidth)
        {
            GUIStyle style = RuntimeTextFieldStyle();
            string text = GetRuntimeCommandInputHeightMeasurementText();
            float measuredHeight = style.CalcHeight(new GUIContent(text), Mathf.Max(40f, inputWidth));
            return Mathf.Clamp(Mathf.Ceil(measuredHeight), InputMinHeight, InputMaxHeight);
        }

        private string GetRuntimeCommandInputHeightMeasurementText()
        {
            string text = string.IsNullOrEmpty(commandInput) ? " " : commandInput;
            string completion = GetInlineGhostCompletionForMeasurement();
            return !string.IsNullOrEmpty(completion) && completion.Length > text.Length ? completion : text;
        }

        private string GetInlineGhostCompletionForMeasurement()
        {
            if (string.IsNullOrEmpty(commandInput) || !NeoCommandAutoComplete.LooksLikeCommandInput(commandInput))
                return string.Empty;

            int cursorIndex = GetCommandInputCursorIndex();
            NeoCommandSuggestion suggestion = NeoCommandAutoComplete.GetSuggestion(commandInput, selectedSuggestionIndex, cursorIndex, NeoCommandExecutionContext.Runtime);
            if (suggestion == null || string.IsNullOrEmpty(suggestion.Completion))
                return string.Empty;

            string hiddenPrefix;
            string visibleGhost;
            string hiddenSuffix;
            return TryGetInlineGhostParts(commandInput, cursorIndex, suggestion.Completion, out hiddenPrefix, out visibleGhost, out hiddenSuffix)
                ? suggestion.Completion
                : string.Empty;
        }

        private static string SanitizeCommandInput(string value)
        {
            return NeoConsoleTextUtility.SanitizeCommandInput(value);
        }

        private void DrawSuggestionOverlay(Rect inputRect)
        {
            if (!IsCommandInputInteractionActive() || !NeoCommandAutoComplete.LooksLikeCommandInput(commandInput))
                return;

            NeoCommandSuggestion[] suggestions = NeoCommandAutoComplete.GetVisibleSuggestions(commandInput, GetCommandInputCursorIndex(), NeoCommandExecutionContext.Runtime);
            bool hasMatches = suggestions.Length > 0;
            int visibleCount = hasMatches ? Mathf.Min(suggestions.Length, MaxVisibleSuggestions) : 1;

            // Matches the Editor console spacing: compact hint reservation, 2px gap from input,
            // and 3px inset for rows inside the suggestion box.
            float overlayHeight = visibleCount * SuggestionRowHeight + (hasMatches ? SuggestionHintHeight : 0f);
            float overlayY = Mathf.Max(8f, inputRect.y - overlayHeight - SuggestionOverlayGap);
            overlayHeight = Mathf.Min(overlayHeight, inputRect.y - overlayY - SuggestionOverlayGap);

            if (overlayHeight <= 18f)
                return;

            Rect overlayRect = new Rect(inputRect.x, overlayY, inputRect.width, overlayHeight);
            GUI.Box(overlayRect, GUIContent.none, RuntimeSuggestionBoxStyle());

            if (!hasMatches)
            {
                string emptyMessage = NeoCommandAutoComplete.HasResolvedCommand(commandInput, NeoCommandExecutionContext.Runtime) || NeoCommandAutoComplete.GetMatches(commandInput, NeoCommandExecutionContext.Runtime).Length > 0 ? "No suggestion available" : "No command found";
                GUI.Label(new Rect(overlayRect.x + 8f, overlayRect.y + 4f, overlayRect.width - 16f, 18f), emptyMessage, RuntimeHintStyle());
                if (Event.current.type == EventType.MouseDown && overlayRect.Contains(Event.current.mousePosition))
                {
                    RequestCommandInputFocus();
                    Event.current.Use();
                }
                return;
            }

            selectedSuggestionIndex = Mathf.Clamp(selectedSuggestionIndex, 0, suggestions.Length - 1);
            int windowStartIndex = GetSuggestionWindowStartIndex(suggestions.Length);
            UpdateSuggestionHoverState(overlayRect, windowStartIndex, visibleCount);
            HandleSuggestionScroll(overlayRect, suggestions.Length);

            bool useMouseHighlight = suggestionMouseHoverActive && hoveredSuggestionIndex >= windowStartIndex && hoveredSuggestionIndex < windowStartIndex + visibleCount;
            for (int i = 0; i < visibleCount; i++)
            {
                int suggestionIndex = windowStartIndex + i;
                if (suggestionIndex >= suggestions.Length)
                    break;

                NeoCommandSuggestion suggestion = suggestions[suggestionIndex];
                Rect row = new Rect(
                    overlayRect.x + SuggestionRowInset,
                    overlayRect.y + SuggestionRowInset + i * SuggestionRowHeight,
                    overlayRect.width - SuggestionRowInset * 2f,
                    SuggestionRowHeight
                );

                bool highlighted = !IsInformationalSuggestion(suggestion) && (useMouseHighlight ? suggestionIndex == hoveredSuggestionIndex : suggestionIndex == selectedSuggestionIndex);
                DrawRuntimeSuggestionRow(row, suggestion, highlighted, suggestionIndex);
            }

            if (Event.current.type == EventType.MouseDown && overlayRect.Contains(Event.current.mousePosition))
            {
                RequestCommandInputFocus();
                Event.current.Use();
            }
        }

        private void UpdateSuggestionHoverState(Rect overlayRect, int windowStartIndex, int visibleCount)
        {
            Event current = Event.current;
            if (current == null)
                return;

            bool directMouseEvent = current.type == EventType.MouseMove ||
                                    current.type == EventType.MouseDrag ||
                                    current.type == EventType.MouseDown;
            bool repaintEvent = current.type == EventType.Repaint;

            if (!directMouseEvent && !repaintEvent)
                return;

            bool mouseMoved = UpdateSuggestionMousePosition(current.mousePosition);

            // Keyboard arrows and mouse-wheel scrolling intentionally own the selection until
            // the pointer actually moves again. Without this guard, a stationary mouse sitting
            // above an old row reactivates hover during Repaint and makes the visual highlight
            // disagree with selectedSuggestionIndex.
            if (suggestionMouseHoverSuppressedUntilMove)
            {
                if (!directMouseEvent && !mouseMoved)
                    return;

                suggestionMouseHoverSuppressedUntilMove = false;
            }

            // Runtime IMGUI does not consistently emit MouseMove events on every platform/player,
            // so Repaint can still update hover, but only when the pointer position changed.
            if (repaintEvent && !mouseMoved && !suggestionMouseHoverActive)
                return;

            int currentHoverIndex = GetSuggestionIndexAtMousePosition(overlayRect, windowStartIndex, visibleCount);
            if (currentHoverIndex >= 0 && IsInformationalSuggestion(GetVisibleSuggestionAtIndex(currentHoverIndex)))
                currentHoverIndex = -1;

            hoveredSuggestionIndex = currentHoverIndex;
            suggestionMouseHoverActive = hoveredSuggestionIndex >= 0;
        }

        private bool UpdateSuggestionMousePosition(Vector2 mousePosition)
        {
            if (!hasLastSuggestionMousePosition)
            {
                lastSuggestionMousePosition = mousePosition;
                hasLastSuggestionMousePosition = true;
                return false;
            }

            bool moved = (lastSuggestionMousePosition - mousePosition).sqrMagnitude > 0.01f;
            lastSuggestionMousePosition = mousePosition;
            return moved;
        }

        private void SuppressSuggestionMouseHoverUntilMove()
        {
            suggestionMouseHoverActive = false;
            hoveredSuggestionIndex = -1;
            suggestionMouseHoverSuppressedUntilMove = true;

            Event current = Event.current;
            if (current != null && IsReliableMousePositionEvent(current.type))
            {
                lastSuggestionMousePosition = current.mousePosition;
                hasLastSuggestionMousePosition = true;
            }
        }

        private static bool IsReliableMousePositionEvent(EventType eventType)
        {
            return eventType == EventType.MouseMove ||
                   eventType == EventType.MouseDrag ||
                   eventType == EventType.MouseDown ||
                   eventType == EventType.MouseUp ||
                   eventType == EventType.ScrollWheel ||
                   eventType == EventType.Repaint;
        }

        private int GetSuggestionIndexAtMousePosition(Rect overlayRect, int windowStartIndex, int visibleCount)
        {
            Event current = Event.current;
            if (current == null || !overlayRect.Contains(current.mousePosition))
                return -1;

            for (int i = 0; i < visibleCount; i++)
            {
                Rect row = new Rect(
                    overlayRect.x + SuggestionRowInset,
                    overlayRect.y + SuggestionRowInset + i * SuggestionRowHeight,
                    overlayRect.width - SuggestionRowInset * 2f,
                    SuggestionRowHeight
                );

                if (row.Contains(current.mousePosition))
                    return windowStartIndex + i;
            }

            return -1;
        }

        private void HandleSuggestionScroll(Rect overlayRect, int suggestionCount)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.ScrollWheel || suggestionCount <= 0 || !overlayRect.Contains(current.mousePosition))
                return;

            int direction = current.delta.y > 0f ? 1 : -1;
            MoveSuggestion(direction);
            RequestCommandInputFocus();
            current.Use();
        }

        private void DrawRuntimeSuggestionRow(Rect row, NeoCommandSuggestion suggestion, bool highlighted, int index)
        {
            Event current = Event.current;

            if (current != null && current.type == EventType.Repaint)
                DrawRuntimeRect(row, highlighted ? RuntimeSelectedSuggestionColor() : RuntimeSuggestionRowColor());

            string displayText = suggestion != null ? suggestion.DisplayText : string.Empty;
            string description = suggestion != null
                ? (!string.IsNullOrEmpty(suggestion.Description) ? suggestion.Description : suggestion.Hint)
                : string.Empty;

            GUIStyle commandStyle = highlighted ? RuntimeSuggestionSelectedStyle() : RuntimeSuggestionStyle();
            GUI.Label(new Rect(row.x + 8f, row.y + 2f, row.width * 0.42f, row.height - 4f), displayText, commandStyle);
            GUI.Label(new Rect(row.x + row.width * 0.42f, row.y + 2f, row.width * 0.58f - 10f, row.height - 4f), description, RuntimeSuggestionDescriptionStyle());

            if (current != null && current.type == EventType.MouseDown && row.Contains(current.mousePosition))
            {
                if (IsInformationalSuggestion(suggestion))
                    return;

                selectedSuggestionIndex = index;
                suggestionMouseHoverActive = false;
                hoveredSuggestionIndex = -1;
                CompleteSelectedSuggestion();
                current.Use();
            }
        }

        private void DrawInlineCommandSuggestion(Rect inputRect)
        {
            GUIStyle inputStyle = RuntimeTextFieldStyle();

            if (string.IsNullOrEmpty(commandInput))
            {
                GUI.Label(inputRect, "Type / to list commands", RuntimeInlineGhostStyle(inputStyle, false));
                return;
            }

            if (!IsCommandFieldActive() || !NeoCommandAutoComplete.LooksLikeCommandInput(commandInput))
                return;

            int cursorIndex = GetCommandInputCursorIndex();
            NeoCommandSuggestion suggestion = NeoCommandAutoComplete.GetSuggestion(commandInput, selectedSuggestionIndex, cursorIndex, NeoCommandExecutionContext.Runtime);
            if (suggestion == null || string.IsNullOrEmpty(suggestion.Completion))
                return;

            string hiddenPrefix;
            string visibleGhost;
            string hiddenSuffix;
            if (!TryGetInlineGhostParts(commandInput, cursorIndex, suggestion.Completion, out hiddenPrefix, out visibleGhost, out hiddenSuffix))
                return;

            string richText = BuildInlineGhostRichText(hiddenPrefix, visibleGhost, hiddenSuffix);
            GUI.Label(inputRect, richText, RuntimeInlineGhostStyle(inputStyle, true));
        }



        private static bool TryGetInlineGhostParts(string input, int cursorIndex, string completion, out string hiddenPrefix, out string visibleGhost, out string hiddenSuffix)
        {
            return NeoConsoleTextUtility.TryGetInlineGhostParts(input, cursorIndex, completion, out hiddenPrefix, out visibleGhost, out hiddenSuffix);
        }

        private static string BuildInlineGhostRichText(string hiddenPrefix, string visibleGhost, string hiddenSuffix)
        {
            return NeoConsoleTextUtility.BuildInlineGhostRichText(hiddenPrefix, visibleGhost, hiddenSuffix);
        }


        private void HandleCommandInputKeys()
        {
            Event current = Event.current;
            if (current == null)
                return;

            if (current.type == EventType.KeyUp && NeoConsoleShortcutUtility.IsTab(current) && IsCommandInputInteractionActive())
            {
                current.Use();
                return;
            }

            if (current.type != EventType.KeyDown)
                return;

            if (!IsCommandInputInteractionActive())
                return;

            if (NeoConsoleShortcutUtility.IsDeletePreviousWord(current))
            {
                DeletePreviousCommandWord();
                current.Use();
            }
            else if (NeoConsoleShortcutUtility.IsDeleteNextWord(current))
            {
                DeleteNextCommandWord();
                current.Use();
            }
            else if (NeoConsoleShortcutUtility.IsHistoryNavigation(current))
            {
                HandleRuntimeHistoryArrow(NeoConsoleShortcutUtility.GetVerticalArrowDirection(current));
                current.Use();
            }
            else if (NeoConsoleShortcutUtility.IsSuggestionNavigation(current))
            {
                HandleRuntimeSuggestionArrow(NeoConsoleShortcutUtility.GetVerticalArrowDirection(current));
                current.Use();
            }
            else if (NeoConsoleShortcutUtility.IsTab(current))
            {
                // Consume Tab before drawing the TextArea. If IMGUI receives the same Tab
                // event in the text control, Unity can move focus or select the whole field.
                current.Use();

                if (ShouldProcessTabCompletion())
                {
                    CompleteSelectedSuggestion();
                    GUI.changed = true;
                }

                commandInputFocused = true;
            }
            else if (NeoConsoleShortcutUtility.IsSubmit(current))
            {
                ExecuteCommand();
                RequestCommandInputFocus();
                current.Use();
            }
        }


        private void DeletePreviousCommandWord()
        {
            int selectionStart;
            int selectionEnd;
            GetCommandInputSelectionRange(out selectionStart, out selectionEnd);

            string newInput;
            int newCursorIndex;
            if (!NeoConsoleTextUtility.TryDeletePreviousWord(commandInput, selectionStart, selectionEnd, out newInput, out newCursorIndex))
            {
                PreserveRuntimeCommandCaret(selectionStart);
                return;
            }

            ApplyManualCommandInputEdit(newInput, newCursorIndex);
        }

        private void DeleteNextCommandWord()
        {
            int selectionStart;
            int selectionEnd;
            GetCommandInputSelectionRange(out selectionStart, out selectionEnd);

            string newInput;
            int newCursorIndex;
            if (!NeoConsoleTextUtility.TryDeleteNextWord(commandInput, selectionStart, selectionEnd, out newInput, out newCursorIndex))
            {
                PreserveRuntimeCommandCaret(selectionStart);
                return;
            }

            ApplyManualCommandInputEdit(newInput, newCursorIndex);
        }

        private void ApplyManualCommandInputEdit(string newInput, int newCursorIndex)
        {
            commandInput = SanitizeCommandInput(newInput);
            previousCommandInput = commandInput;
            selectedSuggestionIndex = 0;
            suggestionMouseHoverActive = false;
            hoveredSuggestionIndex = -1;
            commandHistoryIndex = -1;
            commandHistoryDraft = string.Empty;
            pendingCommandCursorIndex = Mathf.Clamp(newCursorIndex, 0, commandInput.Length);
            StoreLastKnownCommandCursorIndex(pendingCommandCursorIndex);
            pendingCommandCursorApplyFrames = Mathf.Max(pendingCommandCursorApplyFrames, 2);
            pendingCommandInputStateSync = true;
            requestCommandInputFocus = false;
            commandInputFocusFrames = 0;
            commandInputFocused = true;
            GUI.changed = true;
        }

        private void GetCommandInputSelectionRange(out int selectionStart, out int selectionEnd)
        {
            TextEditor textEditor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            if (textEditor != null)
            {
                int cursorIndex = Mathf.Clamp(textEditor.cursorIndex, 0, commandInput.Length);
                int selectIndex = Mathf.Clamp(textEditor.selectIndex, 0, commandInput.Length);
                selectionStart = Mathf.Min(cursorIndex, selectIndex);
                selectionEnd = Mathf.Max(cursorIndex, selectIndex);
                StoreLastKnownCommandCursorIndex(selectionEnd);
                return;
            }

            int fallbackIndex = GetCommandInputCursorIndex();
            selectionStart = fallbackIndex;
            selectionEnd = fallbackIndex;
        }

        private bool IsCommandFieldActive()
        {
            return commandInputFocused || requestCommandInputFocus || commandInputFocusFrames > 0 || GUI.GetNameOfFocusedControl() == "NeoRuntimeOverlayCommand";
        }

        private bool IsCommandInputInteractionActive()
        {
            if (!visible)
                return false;

            if (IsCommandFieldActive())
                return true;

            // Handle runtime navigation keys before the TextArea consumes them. Some IMGUI
            // event passes report no focused control until the named TextArea is drawn, which
            // caused Up/Down/Tab to move the caret, drop suggestions, or select the whole input.
            return !string.IsNullOrEmpty(commandInput);
        }

        private bool ShouldProcessTabCompletion()
        {
            float now = Time.unscaledTime;

            // IMGUI can emit more than one Tab-related key event for a single physical press
            // on some layouts/platforms. Without this guard, one Tab can complete multiple
            // autocomplete steps at once, for example: command -> first object field -> second field.
            if (lastTabCompletionTime >= 0f && now - lastTabCompletionTime < 0.05f)
                return false;

            lastTabCompletionTime = now;
            return true;
        }

        private NeoCommandSuggestion[] GetVisibleSuggestions()
        {
            if (!IsCommandInputInteractionActive())
                return new NeoCommandSuggestion[0];

            if (!NeoCommandAutoComplete.LooksLikeCommandInput(commandInput))
                return new NeoCommandSuggestion[0];

            return NeoCommandAutoComplete.GetVisibleSuggestions(commandInput, GetCommandInputCursorIndex(), NeoCommandExecutionContext.Runtime);
        }

        private void HandleRuntimeSuggestionArrow(int direction)
        {
            // Runtime Up/Down are reserved for suggestion navigation only.
            // When there is no navigable suggestion, consume the key and keep the
            // command input/caret untouched instead of letting TextArea select text
            // or falling back to command history.
            int preservedCursorIndex = GetCommandInputCursorIndex();

            if (HasNavigableSuggestions())
                MoveSuggestion(direction);

            PreserveRuntimeCommandCaret(preservedCursorIndex);
        }

        private void HandleRuntimeHistoryArrow(int direction)
        {
            NavigateCommandHistory(direction);
        }

        private void PreserveRuntimeCommandCaret(int cursorIndex)
        {
            int safeIndex = Mathf.Clamp(cursorIndex, 0, commandInput.Length);
            pendingCommandCursorIndex = safeIndex;
            StoreLastKnownCommandCursorIndex(safeIndex);
            pendingCommandCursorApplyFrames = Mathf.Max(pendingCommandCursorApplyFrames, 2);
            pendingCommandInputStateSync = true;
            requestCommandInputFocus = false;
            commandInputFocusFrames = 0;
            commandInputFocused = true;
        }

        private void MoveSuggestion(int direction)
        {
            NeoCommandSuggestion[] suggestions = GetVisibleSuggestions();
            if (suggestions.Length == 0)
                return;

            int attempts = 0;
            int nextIndex = selectedSuggestionIndex;
            do
            {
                nextIndex = (nextIndex + direction + suggestions.Length) % suggestions.Length;
                attempts++;
            }
            while (attempts <= suggestions.Length && IsInformationalSuggestion(suggestions[nextIndex]));

            if (attempts > suggestions.Length)
                return;

            selectedSuggestionIndex = nextIndex;
            SuppressSuggestionMouseHoverUntilMove();
        }

        private int GetSuggestionWindowStartIndex(int suggestionCount)
        {
            if (suggestionCount <= MaxVisibleSuggestions)
                return 0;

            int maxStartIndex = Mathf.Max(0, suggestionCount - MaxVisibleSuggestions);
            return Mathf.Clamp(selectedSuggestionIndex - MaxVisibleSuggestions + 1, 0, maxStartIndex);
        }


        private bool HasNavigableSuggestions()
        {
            NeoCommandSuggestion[] suggestions = GetVisibleSuggestions();
            for (int i = 0; i < suggestions.Length; i++)
            {
                if (!IsInformationalSuggestion(suggestions[i]))
                    return true;
            }

            return false;
        }

        private void NavigateCommandHistory(int direction)
        {
            if (NeoCommandHistory.Count == 0)
                return;

            if (direction < 0)
            {
                if (commandHistoryIndex < 0)
                {
                    commandHistoryDraft = commandInput;
                    commandHistoryIndex = NeoCommandHistory.Count - 1;
                }
                else
                {
                    commandHistoryIndex = Mathf.Max(0, commandHistoryIndex - 1);
                }
            }
            else
            {
                if (commandHistoryIndex < 0)
                    return;

                if (commandHistoryIndex >= NeoCommandHistory.Count - 1)
                {
                    commandHistoryIndex = -1;
                    ApplyRuntimeHistoryInput(commandHistoryDraft);
                    return;
                }

                commandHistoryIndex++;
            }

            ApplyRuntimeHistoryInput(NeoCommandHistory.Get(commandHistoryIndex));
        }

        private void ApplyRuntimeHistoryInput(string value)
        {
            commandInput = value ?? string.Empty;
            previousCommandInput = commandInput;
            selectedSuggestionIndex = 0;
            suggestionMouseHoverActive = false;
            hoveredSuggestionIndex = -1;
            pendingCommandCursorIndex = commandInput.Length;
            StoreLastKnownCommandCursorIndex(pendingCommandCursorIndex);
            pendingCommandCursorApplyFrames = 6;
            pendingCommandInputStateSync = true;
            requestCommandInputFocus = false;
            commandInputFocusFrames = 0;
            commandInputFocused = true;
        }

        private NeoCommandSuggestion GetVisibleSuggestionAtIndex(int index)
        {
            NeoCommandSuggestion[] suggestions = GetVisibleSuggestions();
            if (index < 0 || index >= suggestions.Length)
                return null;

            return suggestions[index];
        }

        private static bool IsInformationalSuggestion(NeoCommandSuggestion suggestion)
        {
            return suggestion != null && suggestion.IsInformationalOnly;
        }

        private void CompleteSelectedSuggestion()
        {
            if (!NeoCommandAutoComplete.LooksLikeCommandInput(commandInput))
                return;

            int cursorIndex = GetCommandInputCursorIndex();
            NeoCommandSuggestion suggestion = NeoCommandAutoComplete.GetSuggestion(commandInput, selectedSuggestionIndex, cursorIndex, NeoCommandExecutionContext.Runtime);
            if (suggestion == null || string.IsNullOrEmpty(suggestion.Completion))
                return;

            commandInput = suggestion.Completion;
            previousCommandInput = commandInput;
            selectedSuggestionIndex = 0;
            suggestionMouseHoverActive = false;
            hoveredSuggestionIndex = -1;
            pendingCommandCursorIndex = suggestion.CursorIndex >= 0 ? suggestion.CursorIndex : commandInput.Length;
            StoreLastKnownCommandCursorIndex(pendingCommandCursorIndex);
            pendingCommandCursorApplyFrames = 8;
            pendingCommandInputStateSync = true;
            requestCommandInputFocus = false;
            commandInputFocusFrames = 0;
            commandInputFocused = true;
        }

        private int GetCommandInputCursorIndex()
        {
            TextEditor textEditor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            if (textEditor != null)
            {
                int cursorIndex = Mathf.Clamp(textEditor.cursorIndex, 0, commandInput.Length);
                int selectIndex = Mathf.Clamp(textEditor.selectIndex, 0, commandInput.Length);
                int resolvedIndex = cursorIndex != selectIndex ? Mathf.Max(cursorIndex, selectIndex) : cursorIndex;
                StoreLastKnownCommandCursorIndex(resolvedIndex);
                return resolvedIndex;
            }

            if (pendingCommandCursorIndex >= 0)
            {
                int pendingIndex = Mathf.Clamp(pendingCommandCursorIndex, 0, commandInput.Length);
                StoreLastKnownCommandCursorIndex(pendingIndex);
                return pendingIndex;
            }

            if (hasLastKnownCommandCursorIndex)
                return Mathf.Clamp(lastKnownCommandCursorIndex, 0, commandInput.Length);

            return commandInput.Length;
        }

        private void StoreLastKnownCommandCursorIndex(int index)
        {
            lastKnownCommandCursorIndex = Mathf.Clamp(index, 0, commandInput.Length);
            hasLastKnownCommandCursorIndex = true;
        }

        private void UpdateLastKnownCommandCursorIndex()
        {
            if (GUI.GetNameOfFocusedControl() != "NeoRuntimeOverlayCommand")
                return;

            TextEditor textEditor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            if (textEditor == null)
                return;

            int cursorIndex = Mathf.Clamp(textEditor.cursorIndex, 0, commandInput.Length);
            int selectIndex = Mathf.Clamp(textEditor.selectIndex, 0, commandInput.Length);
            StoreLastKnownCommandCursorIndex(cursorIndex != selectIndex ? Mathf.Max(cursorIndex, selectIndex) : cursorIndex);
        }

        private void PrimePendingCommandCursorBeforeInput()
        {
            if (pendingCommandCursorIndex < 0 && !pendingCommandInputStateSync && pendingCommandCursorApplyFrames <= 0)
                return;

            if (GUI.GetNameOfFocusedControl() != "NeoRuntimeOverlayCommand")
                return;

            TextEditor textEditor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            if (textEditor == null)
                return;

            ApplyCommandCursorToTextEditor(textEditor);
        }

        private void ApplyPendingCommandCursor()
        {
            if (pendingCommandCursorIndex < 0 && !pendingCommandInputStateSync && pendingCommandCursorApplyFrames <= 0)
                return;

            FocusRuntimeInputWithoutSelecting();

            if (!TryApplyCommandCursorToActiveTextEditor())
            {
                // The TextArea state can be created one IMGUI pass after focus is requested.
                // Keep the pending cursor alive instead of consuming the request too early.
                pendingCommandInputStateSync = true;
                pendingCommandCursorApplyFrames = Mathf.Max(pendingCommandCursorApplyFrames, 2);
                return;
            }

            pendingCommandInputStateSync = false;

            // Count visual frames instead of raw IMGUI events. Layout/Used/KeyUp events can
            // otherwise consume the guard before the TextArea has actually repainted, which
            // is what caused Tab completion to leave the whole command selected.
            if (pendingCommandCursorApplyFrames > 0 && Event.current.type == EventType.Repaint)
                pendingCommandCursorApplyFrames--;

            if (pendingCommandCursorApplyFrames > 0)
                return;

            pendingCommandCursorIndex = -1;
        }

        private bool TryApplyCommandCursorToActiveTextEditor()
        {
            if (GUI.GetNameOfFocusedControl() != "NeoRuntimeOverlayCommand")
                return false;

            TextEditor textEditor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            if (textEditor == null)
                return false;

            ApplyCommandCursorToTextEditor(textEditor);
            return true;
        }

        private void ApplyCommandCursorToTextEditor(TextEditor textEditor)
        {
            if (textEditor == null)
                return;

            int index = pendingCommandCursorIndex >= 0
                ? Mathf.Clamp(pendingCommandCursorIndex, 0, commandInput.Length)
                : Mathf.Clamp(textEditor.cursorIndex, 0, commandInput.Length);

            textEditor.cursorIndex = index;
            textEditor.selectIndex = index;
            textEditor.scrollOffset = Vector2.zero;
        }

        private void ExecuteCommand()
        {
            string inputToExecute = commandInput;
            NeoCommandHistory.Add(inputToExecute);
            commandHistoryIndex = -1;
            commandHistoryDraft = string.Empty;
            NeoCommandResult result = NeoCommandRegistry.Execute(inputToExecute, NeoCommandExecutionContext.Runtime);
            lastCommandResult = result.Message;
            runtimeLogAutoScroll = true;

            commandInput = string.Empty;
            previousCommandInput = string.Empty;
            selectedSuggestionIndex = 0;
            pendingCommandCursorIndex = 0;
            StoreLastKnownCommandCursorIndex(pendingCommandCursorIndex);
            pendingCommandCursorApplyFrames = 6;
            pendingCommandInputStateSync = true;
            RequestCommandInputFocus();

            LogType type = result.Success ? LogType.Log : LogType.Warning;
            NeoConsoleLogBuffer.AddDirect("[NeoCommand] " + result.Message, string.Empty, type);
        }

        private void FocusRuntimeInputNow()
        {
            GUI.FocusControl("NeoRuntimeOverlayCommand");
            commandInputFocused = true;
        }

        private void FocusRuntimeInputWithoutSelecting()
        {
            // Do not repeatedly refocus an already focused TextArea. In IMGUI, forcing focus
            // during Layout/Used/Repaint passes can reset the TextEditor and visually select
            // the full input. commandInputFocused is used as a guard because
            // GUI.GetNameOfFocusedControl can be empty before the named control is drawn in the
            // current IMGUI pass.
            bool alreadyFocused = GUI.GetNameOfFocusedControl() == "NeoRuntimeOverlayCommand" || commandInputFocused;
            if (!alreadyFocused)
                GUI.FocusControl("NeoRuntimeOverlayCommand");

            commandInputFocused = true;
        }

        private void RequestCommandInputFocus()
        {
            requestCommandInputFocus = true;
            commandInputFocusFrames = commandInputFocused || GUI.GetNameOfFocusedControl() == "NeoRuntimeOverlayCommand" ? 1 : 4;
        }

        private void ConsumeCurrentEventWhileTyping()
        {
            if (!visible || !IsCommandFieldActive())
                return;

            Event current = Event.current;
            if (current == null)
                return;

            if (current.type == EventType.KeyDown ||
                current.type == EventType.KeyUp ||
                current.type == EventType.ScrollWheel)
            {
                current.Use();
            }
        }

        private void LateUpdate()
        {
            if (!visible || !IsCommandFieldActive())
                return;

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            Input.ResetInputAxes();
#endif
        }

        private void ResetPanelRectIfScreenChanged()
        {
            if (lastScreenWidth == Screen.width && lastScreenHeight == Screen.height && panelRect.width > 1f && panelRect.height > 1f)
                return;

            ResetPanelRect();
        }

        private void ResetPanelRect()
        {
            lastScreenWidth = Mathf.Max(1, Screen.width);
            lastScreenHeight = Mathf.Max(1, Screen.height);
            float width = Mathf.Max(PanelMinWidth, lastScreenWidth - Margin * 2f);
            float height = Mathf.Clamp(lastScreenHeight * 0.48f, PanelMinHeight, Mathf.Max(PanelMinHeight, lastScreenHeight - Margin * 2f));
            panelRect = new Rect(Margin, Mathf.Max(Margin, lastScreenHeight - height - Margin), width, height);
        }

        private void ClampPanelToScreen()
        {
            if (panelRect.width <= 1f || panelRect.height <= 1f)
                ResetPanelRect();

            float maxWidth = Mathf.Max(PanelMinWidth, Screen.width - Margin * 2f);
            float maxHeight = Mathf.Max(PanelMinHeight, Screen.height - Margin * 2f);
            panelRect.width = Mathf.Clamp(panelRect.width, PanelMinWidth, maxWidth);
            panelRect.height = Mathf.Clamp(panelRect.height, PanelMinHeight, maxHeight);
            panelRect.x = Mathf.Clamp(panelRect.x, Margin, Mathf.Max(Margin, Screen.width - panelRect.width - Margin));
            panelRect.y = Mathf.Clamp(panelRect.y, Margin, Mathf.Max(Margin, Screen.height - panelRect.height - Margin));
        }

        private static void DrawRuntimeRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static string FormatRuntimeLog(NeoConsoleLogEntry entry)
        {
            string label = entry.Type == LogType.Warning ? "Warning" : IsError(entry.Type) ? "Error" : "Log";
            return "[" + label + "] " + StripRichText(entry.Message);
        }

        private static string TruncateRuntimeLogToMaxLines(string value, GUIStyle style, float width, int maxLines)
        {
            return NeoConsoleTextUtility.TruncateRichTextToMaxLines(value, style, width, maxLines);
        }


        private static bool IsError(LogType type)
        {
            return type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        }

        private static bool IsNeoCommandLog(string message)
        {
            return NeoConsoleTextUtility.IsNeoCommandLog(message);
        }

        private static string StripRichText(string value)
        {
            return NeoConsoleTextUtility.StripRichTextTags(value);
        }

        private static void EnsureRuntimeStyles()
        {
            int screenHeight = Mathf.Max(1, Screen.height);
            if (runtimePanelStyle != null && runtimeCachedSkin == GUI.skin && runtimeCachedScreenHeight == screenHeight)
                return;

            runtimeCachedSkin = GUI.skin;
            runtimeCachedScreenHeight = screenHeight;

            int logFontSize = Mathf.Clamp(screenHeight / 48, 12, 18);
            int buttonFontSize = Mathf.Clamp(screenHeight / 54, 11, 15);
            int inputFontSize = Mathf.Clamp(screenHeight / 44, 13, 20);

            runtimePanelStyle = new GUIStyle(GUI.skin.box);
            runtimePanelStyle.normal.textColor = Color.white;

            runtimeLogBoxStyle = new GUIStyle(GUI.skin.box);
            runtimeLogBoxStyle.padding = new RectOffset(0, 0, 0, 0);

            runtimeLogStyle = CreateRuntimeLogStyle(Color.white, logFontSize);
            runtimeCommandLogStyle = CreateRuntimeLogStyle(new Color(0.325f, 0.643f, 0.369f, 1f), logFontSize);
            runtimeWarningLogStyle = CreateRuntimeLogStyle(new Color(1f, 0.85f, 0.25f, 1f), logFontSize);
            runtimeErrorLogStyle = CreateRuntimeLogStyle(new Color(1f, 0.35f, 0.35f, 1f), logFontSize);

            runtimeButtonStyle = new GUIStyle(GUI.skin.button);
            runtimeButtonStyle.alignment = TextAnchor.MiddleCenter;
            runtimeButtonStyle.fontSize = buttonFontSize;

            runtimeTextFieldStyle = new GUIStyle(GUI.skin.textArea);
            runtimeTextFieldStyle.fontSize = inputFontSize;
            runtimeTextFieldStyle.wordWrap = true;
            runtimeTextFieldStyle.clipping = TextClipping.Clip;
            runtimeTextFieldStyle.alignment = TextAnchor.UpperLeft;
            runtimeTextFieldStyle.padding = new RectOffset(5, 5, 4, 4);
            runtimeTextFieldStyle.contentOffset = Vector2.zero;

            runtimeInlineGhostPlainStyle = CreateRuntimeInlineGhostStyle(runtimeTextFieldStyle, false);
            runtimeInlineGhostRichStyle = CreateRuntimeInlineGhostStyle(runtimeTextFieldStyle, true);

            runtimeGhostStyle = new GUIStyle(GUI.skin.label);
            runtimeGhostStyle.fontSize = inputFontSize;
            runtimeGhostStyle.wordWrap = true;
            runtimeGhostStyle.clipping = TextClipping.Clip;
            runtimeGhostStyle.alignment = TextAnchor.UpperLeft;
            runtimeGhostStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            runtimeGhostStyle.hover.textColor = runtimeGhostStyle.normal.textColor;
            runtimeGhostStyle.active.textColor = runtimeGhostStyle.normal.textColor;

            runtimeSuggestionBoxStyle = new GUIStyle(GUI.skin.box);
            runtimeSuggestionBoxStyle.padding = new RectOffset(2, 2, 2, 2);

            runtimeSuggestionStyle = new GUIStyle(GUI.skin.label);
            runtimeSuggestionStyle.clipping = TextClipping.Clip;
            runtimeSuggestionStyle.alignment = TextAnchor.MiddleLeft;
            runtimeSuggestionStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            runtimeSuggestionStyle.hover.textColor = runtimeSuggestionStyle.normal.textColor;
            runtimeSuggestionStyle.active.textColor = runtimeSuggestionStyle.normal.textColor;
            runtimeSuggestionStyle.focused.textColor = runtimeSuggestionStyle.normal.textColor;

            runtimeSuggestionSelectedStyle = new GUIStyle(runtimeSuggestionStyle);
            runtimeSuggestionSelectedStyle.fontStyle = FontStyle.Bold;
            runtimeSuggestionSelectedStyle.normal.textColor = Color.yellow;
            runtimeSuggestionSelectedStyle.hover.textColor = runtimeSuggestionSelectedStyle.normal.textColor;
            runtimeSuggestionSelectedStyle.active.textColor = runtimeSuggestionSelectedStyle.normal.textColor;
            runtimeSuggestionSelectedStyle.focused.textColor = runtimeSuggestionSelectedStyle.normal.textColor;

            runtimeSuggestionDescriptionStyle = new GUIStyle(GUI.skin.label);
            runtimeSuggestionDescriptionStyle.clipping = TextClipping.Clip;
            runtimeSuggestionDescriptionStyle.alignment = TextAnchor.MiddleLeft;
            runtimeSuggestionDescriptionStyle.normal.textColor = new Color(0.78f, 0.78f, 0.78f, 1f);

            runtimeHintStyle = new GUIStyle(GUI.skin.label);
            runtimeHintStyle.clipping = TextClipping.Clip;
            runtimeHintStyle.alignment = TextAnchor.MiddleLeft;
            runtimeHintStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        }

        private static GUIStyle CreateRuntimeLogStyle(Color color, int fontSize)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.clipping = TextClipping.Clip;
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = fontSize;
            style.normal.textColor = color;
            return style;
        }

        private static GUIStyle CreateRuntimeInlineGhostStyle(GUIStyle inputStyle, bool richText)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.font = inputStyle.font;
            style.fontSize = inputStyle.fontSize;
            style.fontStyle = inputStyle.fontStyle;
            style.wordWrap = true;
            style.richText = richText;
            style.clipping = TextClipping.Clip;
            style.alignment = TextAnchor.UpperLeft;
            style.padding = new RectOffset(inputStyle.padding.left, inputStyle.padding.right, inputStyle.padding.top, inputStyle.padding.bottom);
            style.contentOffset = inputStyle.contentOffset;
            style.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            style.hover.textColor = style.normal.textColor;
            style.active.textColor = style.normal.textColor;
            style.focused.textColor = style.normal.textColor;
            return style;
        }

        private static GUIStyle RuntimePanelStyle()
        {
            EnsureRuntimeStyles();
            return runtimePanelStyle;
        }

        private static GUIStyle RuntimeLogBoxStyle()
        {
            EnsureRuntimeStyles();
            return runtimeLogBoxStyle;
        }

        private static GUIStyle RuntimeLogStyle(NeoConsoleLogEntry entry)
        {
            EnsureRuntimeStyles();

            if (entry != null && IsNeoCommandLog(entry.Message))
                return runtimeCommandLogStyle;
            if (entry != null && entry.Type == LogType.Warning)
                return runtimeWarningLogStyle;
            if (entry != null && IsError(entry.Type))
                return runtimeErrorLogStyle;

            return runtimeLogStyle;
        }

        private static GUIStyle RuntimeButtonStyle()
        {
            EnsureRuntimeStyles();
            return runtimeButtonStyle;
        }

        private static GUIStyle RuntimeTextFieldStyle()
        {
            EnsureRuntimeStyles();
            return runtimeTextFieldStyle;
        }

        private static GUIStyle RuntimeInlineGhostStyle(GUIStyle inputStyle, bool richText)
        {
            EnsureRuntimeStyles();
            return richText ? runtimeInlineGhostRichStyle : runtimeInlineGhostPlainStyle;
        }

        private static GUIStyle RuntimeGhostStyle()
        {
            EnsureRuntimeStyles();
            return runtimeGhostStyle;
        }

        private static GUIStyle RuntimeSuggestionBoxStyle()
        {
            EnsureRuntimeStyles();
            return runtimeSuggestionBoxStyle;
        }

        private static GUIStyle RuntimeSuggestionStyle()
        {
            EnsureRuntimeStyles();
            return runtimeSuggestionStyle;
        }

        private static GUIStyle RuntimeSuggestionSelectedStyle()
        {
            EnsureRuntimeStyles();
            return runtimeSuggestionSelectedStyle;
        }

        private static GUIStyle RuntimeSuggestionDescriptionStyle()
        {
            EnsureRuntimeStyles();
            return runtimeSuggestionDescriptionStyle;
        }

        private static GUIStyle RuntimeHintStyle()
        {
            EnsureRuntimeStyles();
            return runtimeHintStyle;
        }

        private static Color RuntimeSuggestionRowColor()
        {
            return new Color(0.16f, 0.16f, 0.16f, 0.88f);
        }

        private static Color RuntimeSelectedSuggestionColor()
        {
            return new Color(0.20f, 0.28f, 0.38f, 0.95f);
        }

#if UNITY_EDITOR
        internal static void ForceDestroyEditorInstances()
        {
            NeoRuntimeOverlay[] overlays = NeoUnityObjectUtility.FindObjectsByType<NeoRuntimeOverlay>();
            for (int i = 0; i < overlays.Length; i++)
            {
                NeoRuntimeOverlay overlay = overlays[i];
                if (overlay == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(overlay.gameObject);
                else
                    DestroyImmediate(overlay.gameObject);
            }

            instance = null;
        }
#endif
    }
}
#endif
