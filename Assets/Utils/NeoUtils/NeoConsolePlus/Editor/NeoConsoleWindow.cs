#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Neo.ConsolePlus.Editor
{
    [EditorWindowTitle(title = "NeoConsole+")]
    internal sealed class NeoConsoleWindow : EditorWindow, IHasCustomMenu
    {
        public static readonly GUIContent WindowTitle = new GUIContent("NeoConsole+");

        private const float LogRowHeight = 22f;
        private const int LogMessageMaxVisibleLines = 2;
        private const float DetailsMinHeight = 104f;
        private const float DetailsMaxHeight = 700f;
        private const float DetailsReservedBottomHeight = 132f;
        private const float CommandInputMinHeight = 20f;
        private const float CommandInputMaxHeight = 92f;
        private const float CommandInputRowPadding = 8f;
        private const float CommandRunButtonHeight = 22f;
        private const float CommandRunButtonWidth = 56f;
        private const int MaxVisibleSuggestions = 6;

        private int selectedLogEntryId = -1;
        private int pendingCommandCursorIndex = -1;
        private int selectedSuggestionIndex;
        private int hoveredSuggestionIndex = -1;
        private bool suggestionMouseHoverActive;
        private int commandInputFocusFrames;
        private Vector2 logScroll;
        private Vector2 logDetailsScroll;
        private string searchText = string.Empty;
        private string commandInput = string.Empty;
        private string previousCommandInput = string.Empty;
        private string commandHistoryDraft = string.Empty;
        private int commandHistoryIndex = -1;
        private bool showLog = true;
        private bool showWarning = true;
        private bool showError = true;
        private bool collapseLogs = true;
        private bool autoScroll = true;
        private bool resizingDetails;
        private bool pendingRepaint;
        private bool commandInputFocusedLastFrame;
        private bool commandInputActive;
        private bool requestCommandInputFocus;
        private bool hasLastSuggestionOverlayRect;
        private Rect lastSuggestionOverlayRect;
        private Rect lastCommandInputRect;
        private bool suppressCommandSuggestions;
        private bool pendingCommandInputStateSync;
        private bool pendingCommandInputDelayedRepaint;
        private int pendingCommandCursorApplyFrames;
        private double lastTabCompletionTime = -1d;
        private float logDetailsHeight = 180f;

        private readonly List<NeoConsoleLogEntry> logSnapshotCache = new List<NeoConsoleLogEntry>(512);
        private readonly List<NeoConsoleLogDisplayEntry> cachedDisplayEntries = new List<NeoConsoleLogDisplayEntry>(512);
        private float[] cachedDisplayEntryHeights = new float[0];
        private float[] cachedDisplayEntryY = new float[0];
        private float cachedDisplayContentHeight;
        private int cachedDisplayLogVersion = -1;
        private int cachedDisplayWidth = -1;
        private string cachedDisplaySearchText = string.Empty;
        private bool cachedDisplayShowLog;
        private bool cachedDisplayShowWarning;
        private bool cachedDisplayShowError;
        private bool cachedDisplayCollapseLogs;
        private bool cachedDisplayShowNeoCommandLogs;
        private bool cachedDisplayUseCustomColor;
        private Color cachedDisplayConsoleColor;
        private static readonly Dictionary<int, StackTraceLine[]> ScriptStackLineCache = new Dictionary<int, StackTraceLine[]>();
        private static readonly GUIContent TempContent = new GUIContent();

        [MenuItem("Tools/NeoUtils/NeoConsolePlus/Open NeoConsole")]
        public static void OpenFromToolsMenu()
        {
            OpenWindow();
        }

        // Kept so Unity can list NeoConsole+ in the Add Tab menu on versions that build Add Tab from Window menu entries.
        [MenuItem("Window/General/NeoConsole+")]
        private static void OpenFromWindowMenu()
        {
            OpenWindow();
        }

        internal static NeoConsoleWindow OpenFloating()
        {
            return OpenWindow();
        }

        private static void OpenSettingsWindow()
        {
            NeoConsolePlusSettingsWindow.OpenWindow();
        }

        private static NeoConsoleWindow OpenWindow()
        {
            NeoConsoleWindow window = GetWindow<NeoConsoleWindow>(WindowTitle.text);
            window.SetWindowTitle();
            window.Show();
            window.Focus();
            return window;
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            SetWindowTitle();
            logDetailsHeight = NeoConsolePlusEditorSettings.DetailsHeight;
            NeoConsoleLogBuffer.EnsureListening();
            NeoConsoleLogBuffer.SetNormalLogCaptureOwner(this, true);
            // Command registry initialization is lazy to keep script reloads fast.
            NeoConsoleLogBuffer.EntryAdded += OnEntryAdded;
            NeoConsoleLogBuffer.Cleared += OnLogsCleared;
        }

        private void OnDisable()
        {
            NeoConsoleLogBuffer.EntryAdded -= OnEntryAdded;
            NeoConsoleLogBuffer.Cleared -= OnLogsCleared;
            NeoConsoleLogBuffer.SetNormalLogCaptureOwner(this, false);
            NeoConsolePlusEditorSettings.DetailsHeight = logDetailsHeight;
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Open Settings"), false, OpenSettingsWindow);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Clear"), false, NeoConsoleLogBuffer.Clear);
        }

        private void OnGUI()
        {
            DrawConsoleTab();
        }

        private void SetWindowTitle()
        {
            titleContent = new GUIContent("NeoConsole+", GetConsoleWindowIcon(), "NeoConsole+");
        }

        private void DrawConsoleTab()
        {
            HandleCommandFocusMouseDown();
            HandleCommandInputKeys();

            List<NeoConsoleLogDisplayEntry> displayEntries = GetCachedDisplayEntries();

            DrawLogsToolbar();
            DrawLogFilters(NeoConsoleLogBuffer.Count, displayEntries.Count);

            if (selectedLogEntryId < 0 && displayEntries.Count > 0)
                selectedLogEntryId = displayEntries[displayEntries.Count - 1].Entry.Id;

            DrawVirtualizedLogRows(displayEntries);

            ClampDetailsHeightForCurrentWindow();
            DrawDetailsResizeHandle();
            DrawSelectedLogDetails(displayEntries);
            DrawCommandInputRow();
        }

        private void DrawLogsToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                NeoConsoleLogBuffer.Clear();

            if (GUILayout.Button("▾", EditorStyles.toolbarButton, GUILayout.Width(22f)))
                ShowClearOptionsMenu(GUILayoutUtility.GetLastRect());

            GUILayout.Space(4f);
            collapseLogs = GUILayout.Toggle(collapseLogs, "Collapse", EditorStyles.toolbarButton, GUILayout.Width(76f));
            autoScroll = GUILayout.Toggle(autoScroll, "Auto Scroll", EditorStyles.toolbarButton, GUILayout.Width(88f));
            GUILayout.Space(4f);
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                NeoConsolePlusSettingsWindow.OpenWindow();

            GUILayout.FlexibleSpace();
            GUILayout.Label("Search", GUILayout.Width(44f));
            searchText = GUILayout.TextField(searchText, EditorStyles.toolbarTextField, GUILayout.MinWidth(90f), GUILayout.MaxWidth(360f));

            EditorGUILayout.EndHorizontal();
        }

        private void ShowClearOptionsMenu(Rect buttonRect)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Clear"), false, NeoConsoleLogBuffer.Clear);
            menu.AddItem(new GUIContent("Clear Including Compiler Errors"), false, () => NeoConsoleLogBuffer.Clear(true));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open Settings"), false, OpenSettingsWindow);
            menu.DropDown(buttonRect);
        }

        private void DrawLogFilters(int totalCount, int visibleCount)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            showLog = DrawLogFilterToggle(showLog, "Log", 72f);
            showWarning = DrawLogFilterToggle(showWarning, "Warning", 92f);
            showError = DrawLogFilterToggle(showError, "Error", 78f);
            GUILayout.FlexibleSpace();
            GUILayout.Label(visibleCount + " shown / " + totalCount + " entries", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private bool DrawLogFilterToggle(bool value, string label, float width)
        {
            GUIContent content = new GUIContent(" " + label, GetVisibilityIcon(value), value ? "Visible" : "Hidden");
            return GUILayout.Toggle(value, content, EditorStyles.toolbarButton, GUILayout.Width(width));
        }

        private void DrawLogRow(NeoConsoleLogDisplayEntry displayEntry, int rowIndex)
        {
            float rowHeight = CalculateLogRowHeight(displayEntry);
            Rect rowRect = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true));
            DrawLogRowAt(displayEntry, rowIndex, rowRect);
        }

        private void DrawLogRowAt(NeoConsoleLogDisplayEntry displayEntry, int rowIndex, Rect rowRect)
        {
            NeoConsoleLogEntry entry = displayEntry.Entry;
            if (entry == null)
                return;

            bool selected = selectedLogEntryId == entry.Id;

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rowRect, GetRowBackgroundColor(selected, rowIndex));

            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                selectedLogEntryId = entry.Id;

                if (Event.current.button == 1)
                {
                    ShowLogContextMenu(displayEntry);
                }
                else if (Event.current.clickCount == 2)
                {
                    if (!OpenFirstScriptStackLine(displayEntry.Entry))
                        CopyLogToClipboard(displayEntry);
                }

                Repaint();
                Event.current.Use();
            }

            StackTraceLine sourceLine = GetFirstRelevantStackLineInfo(entry);
            string source = GetStackPathDisplay(sourceLine);

            Rect iconRect = new Rect(rowRect.x + 7f, rowRect.y + Mathf.Max(4f, (rowRect.height - 14f) * 0.5f), 14f, 14f);
            Rect countRect = new Rect(rowRect.xMax - 46f, rowRect.y + Mathf.Max(3f, (rowRect.height - 16f) * 0.5f), 38f, 16f);

            float sourceWidth = string.IsNullOrEmpty(source) ? 0f : Mathf.Clamp(rowRect.width * 0.30f, 150f, 260f);
            float sourceRight = displayEntry.Count > 1 ? countRect.x - 8f : rowRect.xMax - 8f;
            Rect sourceRect = new Rect(sourceRight - sourceWidth, rowRect.y + Mathf.Max(3f, (rowRect.height - 16f) * 0.5f), sourceWidth, 16f);
            Rect messageRect = new Rect(rowRect.x + 28f, rowRect.y + 2f, Mathf.Max(70f, (string.IsNullOrEmpty(source) ? sourceRight : sourceRect.x - 8f) - rowRect.x - 28f), Mathf.Max(18f, rowRect.height - 4f));

            DrawLogIcon(iconRect, entry.Type);
            DrawWrappedLogLabel(messageRect, MakeSingleLine(GetDisplayMessage(entry)));

            if (!string.IsNullOrEmpty(source))
            {
                GUIStyle sourceStyle = CreateLogSourceStyle();
                bool sourceTruncated = IsPlainTextTruncatedToFit(source, sourceStyle, sourceRect.width);
                string sourceLabel = sourceTruncated ? TruncatePlainTextToFit(source, sourceStyle, sourceRect.width) : source;
                string sourceTooltip = sourceTruncated ? GetStackTooltip(sourceLine) : string.Empty;
                TempContent.text = sourceLabel;
                TempContent.tooltip = sourceTooltip;
                GUI.Label(sourceRect, TempContent, sourceStyle);
            }

            if (displayEntry.Count > 1)
                GUI.Label(countRect, displayEntry.Count.ToString(), CreateLogCountStyle());
        }

        private float CalculateLogRowHeight(NeoConsoleLogDisplayEntry displayEntry)
        {
            if (displayEntry.Entry == null)
                return LogRowHeight;

            float rowWidth = Mathf.Max(160f, position.width - 22f);
            StackTraceLine sourceLine = GetFirstRelevantStackLineInfo(displayEntry.Entry);
            string source = GetStackPathDisplay(sourceLine);
            float countWidth = displayEntry.Count > 1 ? 46f : 0f;
            float sourceWidth = string.IsNullOrEmpty(source) ? 0f : Mathf.Clamp(rowWidth * 0.30f, 150f, 260f);
            float messageWidth = Mathf.Max(70f, rowWidth - 28f - sourceWidth - countWidth - 22f);

            GUIStyle style = CreateLogMessageStyle();
            style.wordWrap = true;
            string message = MakeSingleLine(GetDisplayMessage(displayEntry.Entry));
            float contentHeight = style.CalcHeight(new GUIContent(message), messageWidth);
            float maxMessageHeight = Mathf.Max(18f, style.lineHeight * LogMessageMaxVisibleLines + 2f);
            return Mathf.Max(LogRowHeight, Mathf.Min(contentHeight, maxMessageHeight) + 4f);
        }

        private void DrawDetailsResizeHandle()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 5f, GUILayout.ExpandWidth(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2f, rect.width, 1f), EditorGUIUtility.isProSkin ? new Color(0.32f, 0.32f, 0.32f, 1f) : new Color(0.55f, 0.55f, 0.55f, 1f));

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                resizingDetails = true;
                Event.current.Use();
            }

            if (resizingDetails && Event.current.type == EventType.MouseDrag)
            {
                logDetailsHeight = Mathf.Clamp(logDetailsHeight - Event.current.delta.y, DetailsMinHeight, GetDynamicDetailsMaxHeight());
                Repaint();
                Event.current.Use();
            }

            if (Event.current.rawType == EventType.MouseUp)
                resizingDetails = false;
        }

        private void DrawSelectedLogDetails(List<NeoConsoleLogDisplayEntry> displayEntries)
        {
            NeoConsoleLogDisplayEntry selectedEntry;
            if (!TryGetSelectedDisplayEntry(displayEntries, out selectedEntry) || selectedEntry.Entry == null)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(logDetailsHeight));
            DrawDetailsHeader(selectedEntry);

            logDetailsScroll = EditorGUILayout.BeginScrollView(logDetailsScroll, GUILayout.ExpandHeight(true));
            string message = GetDisplayMessage(selectedEntry.Entry);
            GUIStyle detailsMessageStyle = CreateDetailsMessageStyle();
            float detailsMessageWidth = Mathf.Max(180f, EditorGUIUtility.currentViewWidth - 42f);
            float detailsMessageHeight = Mathf.Max(detailsMessageStyle.lineHeight + 8f, detailsMessageStyle.CalcHeight(new GUIContent(message), detailsMessageWidth) + 8f);
            EditorGUILayout.LabelField(new GUIContent(message), detailsMessageStyle, GUILayout.Height(detailsMessageHeight));

            StackTraceLine[] scriptLines = GetScriptStackLines(selectedEntry.Entry);
            if (scriptLines.Length > 0)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Script Trace", EditorStyles.boldLabel);
                DrawScriptStackLines(scriptLines);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetailsHeader(NeoConsoleLogDisplayEntry selectedEntry)
        {
            NeoConsoleLogEntry entry = selectedEntry.Entry;
            Rect headerRect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
                EditorStyles.toolbar.Draw(headerRect, GUIContent.none, false, false, false, false);

            Rect iconRect = new Rect(headerRect.x + 3, headerRect.y + 3, 15f, 15f);
            DrawLogIcon(iconRect, entry.Type);

            GUI.Label(new Rect(headerRect.x + 30f, headerRect.y + 2, 78f, 18f), GetTypeLabel(entry.Type), EditorStyles.boldLabel);
            GUI.Label(new Rect(headerRect.x + 112f, headerRect.y + 3, 116f, 16f), "Time: " + FormatTime(entry.TimeSinceStartup), EditorStyles.miniLabel);

            if (selectedEntry.Count > 1)
                GUI.Label(new Rect(headerRect.x + 234f, headerRect.y + 3, 90f, 16f), "Count: " + selectedEntry.Count, EditorStyles.miniLabel);
            Rect copyRect = new Rect(headerRect.xMax - 58f, headerRect.y, 54f, 18f);

            if (GUI.Button(copyRect, "Copy", EditorStyles.miniButton))
                CopyLogToClipboard(selectedEntry);

        }

        private void DrawCommandSuggestionOverlay(Rect inputRect)
        {
            hasLastSuggestionOverlayRect = false;

            if (suppressCommandSuggestions)
                return;

            if (!IsCommandInputFocused() || !NeoCommandAutoComplete.LooksLikeCommandInput(commandInput))
                return;

            NeoCommandSuggestion[] suggestions = GetVisibleCommandSuggestions();
            bool hasMatches = suggestions.Length > 0;
            int visibleCount = hasMatches ? Mathf.Min(suggestions.Length, MaxVisibleSuggestions) : 1;
            float rowHeight = 22f;
            float hintHeight = 6f;
            float overlayHeight = visibleCount * rowHeight + (hasMatches ? hintHeight : 0f);
            float y = Mathf.Max(48f, inputRect.y - overlayHeight - 2f);
            Rect overlayRect = new Rect(inputRect.x, y, inputRect.width, Mathf.Min(overlayHeight, inputRect.y - y - 2f));

            if (overlayRect.height <= 18f)
                return;

            hasLastSuggestionOverlayRect = true;
            lastSuggestionOverlayRect = overlayRect;

            GUI.Box(overlayRect, GUIContent.none, EditorStyles.helpBox);

            if (!hasMatches)
            {
                string emptyMessage = NeoCommandAutoComplete.HasResolvedCommand(commandInput, NeoCommandExecutionContext.Editor) || NeoCommandAutoComplete.GetMatches(commandInput, NeoCommandExecutionContext.Editor).Length > 0 ? "No suggestion available" : "No command found";
                GUI.Label(new Rect(overlayRect.x + 8f, overlayRect.y + 4f, overlayRect.width - 16f, 18f), emptyMessage, EditorStyles.miniLabel);
                if (Event.current.type == EventType.MouseDown && overlayRect.Contains(Event.current.mousePosition))
                {
                    commandInputActive = true;
                    RequestCommandInputFocus();
                    Event.current.Use();
                }
                return;
            }

            selectedSuggestionIndex = Mathf.Clamp(selectedSuggestionIndex, 0, suggestions.Length - 1);
            int windowStartIndex = GetSuggestionWindowStartIndex(suggestions.Length);
            UpdateSuggestionHoverState(overlayRect, windowStartIndex, visibleCount, rowHeight);
            HandleSuggestionScroll(overlayRect, suggestions.Length);

            bool useMouseHighlight = suggestionMouseHoverActive && hoveredSuggestionIndex >= windowStartIndex && hoveredSuggestionIndex < windowStartIndex + visibleCount;
            for (int i = 0; i < visibleCount; i++)
            {
                int suggestionIndex = windowStartIndex + i;
                if (suggestionIndex >= suggestions.Length)
                    break;

                Rect row = new Rect(overlayRect.x + 3f, overlayRect.y + 3f + i * rowHeight, overlayRect.width - 6f, rowHeight);
                NeoCommandSuggestion suggestion = suggestions[suggestionIndex];
                bool highlighted = !IsInformationalSuggestion(suggestion) && (useMouseHighlight ? suggestionIndex == hoveredSuggestionIndex : suggestionIndex == selectedSuggestionIndex);
                DrawCommandSuggestionOverlayRow(row, suggestion, highlighted, suggestionIndex);
            }

            if (Event.current.type == EventType.MouseDown && overlayRect.Contains(Event.current.mousePosition))
            {
                commandInputActive = true;
                RequestCommandInputFocus();
                Event.current.Use();
            }
        }

        private void UpdateSuggestionHoverState(Rect overlayRect, int windowStartIndex, int visibleCount, float rowHeight)
        {
            Event current = Event.current;
            if (current == null)
                return;

            int currentHoverIndex = GetSuggestionIndexAtMousePosition(overlayRect, windowStartIndex, visibleCount, rowHeight);
            if (currentHoverIndex >= 0 && IsInformationalSuggestion(GetVisibleSuggestionAtIndex(currentHoverIndex)))
                currentHoverIndex = -1;

            if (current.type == EventType.MouseMove)
            {
                hoveredSuggestionIndex = currentHoverIndex;
                suggestionMouseHoverActive = hoveredSuggestionIndex >= 0;
                Repaint();
            }
            else if (current.type == EventType.Repaint && suggestionMouseHoverActive && currentHoverIndex < 0)
            {
                hoveredSuggestionIndex = -1;
                suggestionMouseHoverActive = false;
            }
        }

        private int GetSuggestionIndexAtMousePosition(Rect overlayRect, int windowStartIndex, int visibleCount, float rowHeight)
        {
            Event current = Event.current;
            if (current == null || !overlayRect.Contains(current.mousePosition))
                return -1;

            for (int i = 0; i < visibleCount; i++)
            {
                Rect row = new Rect(overlayRect.x + 3f, overlayRect.y + 3f + i * rowHeight, overlayRect.width - 6f, rowHeight);
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
            suggestionMouseHoverActive = false;
            hoveredSuggestionIndex = -1;
            MoveSuggestion(direction);
            current.Use();
        }

        private void DrawCommandSuggestionOverlayRow(Rect row, NeoCommandSuggestion suggestion, bool highlighted, int index)
        {
            Event current = Event.current;

            if (current != null && current.type == EventType.Repaint)
                EditorGUI.DrawRect(row, highlighted ? GetSelectedSuggestionColor() : GetSuggestionRowColor());

            string displayText = suggestion != null ? suggestion.DisplayText : string.Empty;
            string description = suggestion != null
                ? (!string.IsNullOrEmpty(suggestion.Description) ? suggestion.Description : suggestion.Hint)
                : string.Empty;

            GUIStyle commandStyle = highlighted ? CreateSuggestionSelectedStyle() : CreateSuggestionStyle();
            GUI.Label(new Rect(row.x + 8f, row.y + 2f, row.width * 0.42f, row.height - 4f), displayText, commandStyle);
            GUI.Label(new Rect(row.x + row.width * 0.42f, row.y + 2f, row.width * 0.58f - 10f, row.height - 4f), description, CreateSuggestionDescriptionStyle());

            if (current != null && current.type == EventType.MouseDown && row.Contains(current.mousePosition))
            {
                if (IsInformationalSuggestion(suggestion))
                    return;

                CompleteSuggestionAtIndex(index);
                current.Use();
            }
        }

        private void DrawCommandInputRow()
        {
            float availableWidth = Mathf.Max(120f, position.width - CommandRunButtonWidth - 24f);
            float inputHeight = GetCommandInputHeight(availableWidth);
            float rowHeight = inputHeight + CommandInputRowPadding;
            Rect rowRect = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true));

            float runWidth = Mathf.Min(CommandRunButtonWidth, Mathf.Max(42f, rowRect.width * 0.20f));
            Rect inputRect = new Rect(
                rowRect.x + 4f,
                rowRect.y + CommandInputRowPadding * 0.5f,
                Mathf.Max(60f, rowRect.width - runWidth - 12f),
                inputHeight
            );
            float runHeight = Mathf.Min(CommandRunButtonHeight, inputHeight);
            Rect runRect = new Rect(rowRect.xMax - runWidth - 4f, inputRect.yMax - runHeight, runWidth, runHeight);
            lastCommandInputRect = inputRect;

            if (Event.current.type == EventType.MouseDown && inputRect.Contains(Event.current.mousePosition))
            {
                commandInputActive = true;
                RequestCommandInputFocus();
            }

            PrimePendingCommandCursorBeforeInput();

            GUI.SetNextControlName("NeoCommandInput");
            string newInput = GUI.TextArea(inputRect, commandInput, CreateCommandInputStyle());
            newInput = SanitizeCommandInput(newInput);
            commandInputFocusedLastFrame = GUI.GetNameOfFocusedControl() == "NeoCommandInput";
            if (commandInputFocusedLastFrame)
                commandInputActive = true;

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
                    suppressCommandSuggestions = false;
                    pendingCommandCursorIndex = -1;
                    pendingCommandCursorApplyFrames = 0;
                    pendingCommandInputStateSync = false;
                }
                previousCommandInput = commandInput;
            }

            DrawInlineCommandSuggestion(inputRect);

            if (requestCommandInputFocus || commandInputFocusFrames > 0)
            {
                FocusCommandInputNow();
                requestCommandInputFocus = false;
                if (commandInputFocusFrames > 0)
                    commandInputFocusFrames--;
            }

            ApplyPendingCommandCursor();

            if (GUI.Button(runRect, "Run", CreateCommandRunButtonStyle()))
                ExecuteCommandInput();

            DrawCommandSuggestionOverlay(inputRect);
        }

        private float GetCommandInputHeight(float inputWidth)
        {
            GUIStyle style = CreateCommandInputStyle();
            string text = GetCommandInputHeightMeasurementText();
            float measuredHeight = style.CalcHeight(new GUIContent(text), Mathf.Max(40f, inputWidth));
            return Mathf.Clamp(Mathf.Ceil(measuredHeight), CommandInputMinHeight, CommandInputMaxHeight);
        }

        private string GetCommandInputHeightMeasurementText()
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
            NeoCommandSuggestion suggestion = NeoCommandAutoComplete.GetSuggestion(commandInput, selectedSuggestionIndex, cursorIndex, NeoCommandExecutionContext.Editor);
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

        private void DrawInlineCommandSuggestion(Rect inputRect)
        {
            GUIStyle inputStyle = CreateCommandInputStyle();

            if (string.IsNullOrEmpty(commandInput))
            {
                GUI.Label(inputRect, "Type / to list commands", CreateInlineCommandGhostStyle(inputStyle, false));
                return;
            }

            if (!IsCommandInputFocused() || !NeoCommandAutoComplete.LooksLikeCommandInput(commandInput))
                return;

            int cursorIndex = GetCommandInputCursorIndex();
            NeoCommandSuggestion suggestion = NeoCommandAutoComplete.GetSuggestion(commandInput, selectedSuggestionIndex, cursorIndex, NeoCommandExecutionContext.Editor);
            if (suggestion == null || string.IsNullOrEmpty(suggestion.Completion))
                return;

            string hiddenPrefix;
            string visibleGhost;
            string hiddenSuffix;
            if (!TryGetInlineGhostParts(commandInput, cursorIndex, suggestion.Completion, out hiddenPrefix, out visibleGhost, out hiddenSuffix))
                return;

            string richText = BuildInlineGhostRichText(hiddenPrefix, visibleGhost, hiddenSuffix);
            GUI.Label(inputRect, richText, CreateInlineCommandGhostStyle(inputStyle, true));
        }

        private void HandleCommandFocusMouseDown()
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown)
                return;

            bool insideInput = lastCommandInputRect.Contains(current.mousePosition);
            bool insideSuggestion = hasLastSuggestionOverlayRect && lastSuggestionOverlayRect.Contains(current.mousePosition);
            if (insideInput || insideSuggestion)
            {
                commandInputActive = true;
                RequestCommandInputFocus();
            }
            else
            {
                commandInputActive = false;
                requestCommandInputFocus = false;
                commandInputFocusFrames = 0;
            }
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

            if (current.type == EventType.KeyUp && NeoConsoleShortcutUtility.IsTab(current) && IsCommandInputFocused())
            {
                current.Use();
                return;
            }

            if (current.type != EventType.KeyDown || !IsCommandInputFocused())
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
                NavigateCommandHistory(NeoConsoleShortcutUtility.GetVerticalArrowDirection(current));
                current.Use();
            }
            else if (NeoConsoleShortcutUtility.IsSuggestionNavigation(current))
            {
                HandleEditorSuggestionArrow(NeoConsoleShortcutUtility.GetVerticalArrowDirection(current));
                current.Use();
            }
            else if (NeoConsoleShortcutUtility.IsTab(current))
            {
                // Consume Tab before drawing the TextArea. If IMGUI receives the same Tab
                // event in the text control, Unity can move focus or select the whole field.
                current.Use();

                if (ShouldProcessTabCompletion())
                {
                    CompleteSelectedSuggestion(false);
                    Repaint();
                }

                commandInputActive = true;
                commandInputFocusedLastFrame = true;
            }
            else if (NeoConsoleShortcutUtility.IsSubmit(current))
            {
                ExecuteCommandInput();
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
                PreserveEditorCommandCaret(selectionStart);
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
                PreserveEditorCommandCaret(selectionStart);
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
            suppressCommandSuggestions = false;
            pendingCommandCursorIndex = Mathf.Clamp(newCursorIndex, 0, commandInput.Length);
            pendingCommandCursorApplyFrames = Mathf.Max(pendingCommandCursorApplyFrames, 2);
            pendingCommandInputStateSync = true;
            commandInputActive = true;
            commandInputFocusedLastFrame = true;
            GUI.changed = true;
            Repaint();
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
                return;
            }

            int fallbackIndex = GetCommandInputCursorIndex();
            selectionStart = fallbackIndex;
            selectionEnd = fallbackIndex;
        }

        private void HandleEditorSuggestionArrow(int direction)
        {
            int preservedCursorIndex = GetCommandInputCursorIndex();

            if (HasNavigableSuggestions())
                MoveSuggestion(direction);

            PreserveEditorCommandCaret(preservedCursorIndex);
        }

        private void PreserveEditorCommandCaret(int cursorIndex)
        {
            pendingCommandCursorIndex = Mathf.Clamp(cursorIndex, 0, commandInput.Length);
            pendingCommandCursorApplyFrames = Mathf.Max(pendingCommandCursorApplyFrames, 2);
            pendingCommandInputStateSync = true;
            commandInputActive = true;
            commandInputFocusedLastFrame = true;
        }

        private bool IsCommandInputFocused()
        {
            return commandInputActive || GUI.GetNameOfFocusedControl() == "NeoCommandInput" || commandInputFocusedLastFrame || requestCommandInputFocus;
        }

        private bool ShouldProcessTabCompletion()
        {
            double now = EditorApplication.timeSinceStartup;

            // IMGUI can emit more than one Tab-related key event for a single physical press
            // on some layouts/platforms. Without this guard, one Tab can complete multiple
            // autocomplete steps at once, for example: command -> first object field -> second field.
            if (lastTabCompletionTime >= 0d && now - lastTabCompletionTime < 0.05d)
                return false;

            lastTabCompletionTime = now;
            return true;
        }

        private NeoCommandSuggestion[] GetVisibleCommandSuggestions()
        {
            if (!IsCommandInputFocused())
                return new NeoCommandSuggestion[0];

            if (!NeoCommandAutoComplete.LooksLikeCommandInput(commandInput))
                return new NeoCommandSuggestion[0];

            return NeoCommandAutoComplete.GetVisibleSuggestions(commandInput, GetCommandInputCursorIndex(), NeoCommandExecutionContext.Editor);
        }

        private bool HasNavigableSuggestions()
        {
            NeoCommandSuggestion[] suggestions = GetVisibleCommandSuggestions();
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
                    commandInput = commandHistoryDraft;
                    previousCommandInput = commandInput;
                    pendingCommandCursorIndex = commandInput.Length;
                    commandInputActive = true;
                    RequestCommandInputFocus();
                    Repaint();
                    return;
                }

                commandHistoryIndex++;
            }

            commandInput = NeoCommandHistory.Get(commandHistoryIndex);
            previousCommandInput = commandInput;
            selectedSuggestionIndex = 0;
            pendingCommandCursorIndex = commandInput.Length;
            commandInputActive = true;
            RequestCommandInputFocus();
            Repaint();
        }

        private void MoveSuggestion(int direction)
        {
            NeoCommandSuggestion[] suggestions = GetVisibleCommandSuggestions();
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

            suggestionMouseHoverActive = false;
            hoveredSuggestionIndex = -1;
            selectedSuggestionIndex = nextIndex;
            Repaint();
        }

        private int GetSuggestionWindowStartIndex(int suggestionCount)
        {
            if (suggestionCount <= MaxVisibleSuggestions)
                return 0;

            int maxStartIndex = Mathf.Max(0, suggestionCount - MaxVisibleSuggestions);
            return Mathf.Clamp(selectedSuggestionIndex - MaxVisibleSuggestions + 1, 0, maxStartIndex);
        }


        private NeoCommandSuggestion GetVisibleSuggestionAtIndex(int index)
        {
            NeoCommandSuggestion[] suggestions = GetVisibleCommandSuggestions();
            if (index < 0 || index >= suggestions.Length)
                return null;

            return suggestions[index];
        }

        private static bool IsInformationalSuggestion(NeoCommandSuggestion suggestion)
        {
            return suggestion != null && suggestion.IsInformationalOnly;
        }

        private void CompleteSuggestionAtIndex(int index)
        {
            NeoCommandSuggestion suggestion = GetVisibleSuggestionAtIndex(index);
            if (IsInformationalSuggestion(suggestion))
                return;

            selectedSuggestionIndex = Mathf.Max(0, index);
            CompleteSelectedSuggestion(true);
        }

        private void CompleteSelectedSuggestion()
        {
            CompleteSelectedSuggestion(true);
        }

        private void CompleteSelectedSuggestion(bool closeSuggestions)
        {
            if (!NeoCommandAutoComplete.LooksLikeCommandInput(commandInput))
                return;

            int cursorIndex = GetCommandInputCursorIndex();
            NeoCommandSuggestion suggestion = NeoCommandAutoComplete.GetSuggestion(commandInput, selectedSuggestionIndex, cursorIndex, NeoCommandExecutionContext.Editor);
            if (suggestion == null || string.IsNullOrEmpty(suggestion.Completion))
                return;

            commandInput = suggestion.Completion;
            previousCommandInput = commandInput;
            selectedSuggestionIndex = 0;
            suggestionMouseHoverActive = false;
            hoveredSuggestionIndex = -1;
            suppressCommandSuggestions = false;
            pendingCommandCursorIndex = suggestion.CursorIndex >= 0 ? suggestion.CursorIndex : commandInput.Length;
            pendingCommandCursorApplyFrames = 8;
            pendingCommandInputStateSync = true;
            commandInputActive = true;
            commandInputFocusedLastFrame = true;

            if (closeSuggestions)
                HideCommandSuggestions();

            ForceCommandInputStateRefresh();
        }

        private void HideCommandSuggestions()
        {
            suppressCommandSuggestions = true;
            hasLastSuggestionOverlayRect = false;
            lastSuggestionOverlayRect = Rect.zero;
            suggestionMouseHoverActive = false;
            hoveredSuggestionIndex = -1;
        }

        private void ForceCommandInputStateRefresh()
        {
            pendingCommandInputStateSync = true;
            GUI.changed = true;
            Repaint();

            if (pendingCommandInputDelayedRepaint)
                return;

            pendingCommandInputDelayedRepaint = true;
            EditorApplication.delayCall += DelayedCommandInputRepaint;
        }

        private void DelayedCommandInputRepaint()
        {
            pendingCommandInputDelayedRepaint = false;
            if (this == null)
                return;

            pendingCommandInputStateSync = true;
            Repaint();
        }

        private int GetCommandInputCursorIndex()
        {
            TextEditor textEditor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            if (textEditor != null)
            {
                int cursorIndex = Mathf.Clamp(textEditor.cursorIndex, 0, commandInput.Length);
                int selectIndex = Mathf.Clamp(textEditor.selectIndex, 0, commandInput.Length);

                if (cursorIndex != selectIndex)
                    return Mathf.Max(cursorIndex, selectIndex);

                return cursorIndex;
            }

            if (pendingCommandCursorIndex >= 0)
                return Mathf.Clamp(pendingCommandCursorIndex, 0, commandInput.Length);

            return commandInput.Length;
        }

        private void PrimePendingCommandCursorBeforeInput()
        {
            if (pendingCommandCursorIndex < 0 && !pendingCommandInputStateSync && pendingCommandCursorApplyFrames <= 0)
                return;

            if (GUI.GetNameOfFocusedControl() != "NeoCommandInput")
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

            FocusCommandInputWithoutSelecting();

            if (!TryApplyCommandCursorToActiveTextEditor())
            {
                // The TextArea state can be created one IMGUI pass after focus is requested.
                // Keep the pending cursor alive instead of consuming the request too early.
                pendingCommandInputStateSync = true;
                pendingCommandCursorApplyFrames = Mathf.Max(pendingCommandCursorApplyFrames, 2);
                Repaint();
                return;
            }

            pendingCommandInputStateSync = false;

            // Count visual frames instead of raw IMGUI events. Layout/Used/KeyUp events can
            // otherwise consume the guard before the TextArea has actually repainted, which
            // is what caused Tab completion to leave the whole command selected.
            if (pendingCommandCursorApplyFrames > 0 && Event.current.type == EventType.Repaint)
                pendingCommandCursorApplyFrames--;

            if (pendingCommandCursorApplyFrames > 0)
            {
                Repaint();
                return;
            }

            pendingCommandCursorIndex = -1;
        }

        private bool TryApplyCommandCursorToActiveTextEditor()
        {
            if (GUI.GetNameOfFocusedControl() != "NeoCommandInput")
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

        private void ExecuteCommandInput()
        {
            string inputToExecute = commandInput;
            NeoCommandHistory.Add(inputToExecute);
            commandHistoryIndex = -1;
            commandHistoryDraft = string.Empty;
            NeoCommandResult result = NeoCommandRegistry.Execute(inputToExecute, NeoCommandExecutionContext.Editor);
            
            commandInput = string.Empty;
            previousCommandInput = string.Empty;
            selectedSuggestionIndex = 0;
            suppressCommandSuggestions = false;
            pendingCommandCursorIndex = 0;
            pendingCommandInputStateSync = true;
            pendingCommandCursorApplyFrames = 6;
            RequestCommandInputFocus();
            Repaint();

            if (!NeoConsolePlusEditorSettings.ShowNeoCommandLogs)
                return;

            LogType type = result.Success ? LogType.Log : LogType.Warning;
            NeoConsoleLogBuffer.AddDirect("[NeoCommand] " + result.Message, string.Empty, type);
        }

        private List<NeoConsoleLogDisplayEntry> GetCachedDisplayEntries()
        {
            int logVersion = NeoConsoleLogBuffer.Version;
            int width = Mathf.RoundToInt(Mathf.Max(160f, position.width));
            bool showNeoCommandLogs = NeoConsolePlusEditorSettings.ShowNeoCommandLogs;
            bool useCustomColor = NeoConsolePlusEditorSettings.UseCustomConsoleLogColor;
            Color consoleColor = NeoConsolePlusEditorSettings.ConsoleLogColor;

            if (cachedDisplayLogVersion == logVersion &&
                cachedDisplayWidth == width &&
                cachedDisplayShowLog == showLog &&
                cachedDisplayShowWarning == showWarning &&
                cachedDisplayShowError == showError &&
                cachedDisplayCollapseLogs == collapseLogs &&
                cachedDisplayShowNeoCommandLogs == showNeoCommandLogs &&
                cachedDisplayUseCustomColor == useCustomColor &&
                cachedDisplayConsoleColor == consoleColor &&
                string.Equals(cachedDisplaySearchText, searchText ?? string.Empty, StringComparison.Ordinal))
            {
                return cachedDisplayEntries;
            }

            NeoConsoleLogBuffer.CopySnapshot(logSnapshotCache);
            cachedDisplayEntries.Clear();
            BuildDisplayEntries(logSnapshotCache, cachedDisplayEntries);
            RebuildDisplayEntryHeightCache(cachedDisplayEntries);

            cachedDisplayLogVersion = logVersion;
            cachedDisplayWidth = width;
            cachedDisplayShowLog = showLog;
            cachedDisplayShowWarning = showWarning;
            cachedDisplayShowError = showError;
            cachedDisplayCollapseLogs = collapseLogs;
            cachedDisplayShowNeoCommandLogs = showNeoCommandLogs;
            cachedDisplayUseCustomColor = useCustomColor;
            cachedDisplayConsoleColor = consoleColor;
            cachedDisplaySearchText = searchText ?? string.Empty;

            return cachedDisplayEntries;
        }

        private void RebuildDisplayEntryHeightCache(List<NeoConsoleLogDisplayEntry> displayEntries)
        {
            int count = displayEntries != null ? displayEntries.Count : 0;
            if (cachedDisplayEntryHeights.Length != count)
                cachedDisplayEntryHeights = new float[count];
            if (cachedDisplayEntryY.Length != count)
                cachedDisplayEntryY = new float[count];

            float y = 0f;
            for (int i = 0; i < count; i++)
            {
                cachedDisplayEntryY[i] = y;
                float height = CalculateLogRowHeight(displayEntries[i]);
                cachedDisplayEntryHeights[i] = height;
                y += height;
            }

            cachedDisplayContentHeight = y;
        }

        private void InvalidateDisplayCache()
        {
            cachedDisplayLogVersion = -1;
        }

        private void DrawVirtualizedLogRows(List<NeoConsoleLogDisplayEntry> displayEntries)
        {
            Rect scrollRect = GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            float viewWidth = Mathf.Max(1f, scrollRect.width - 16f);
            float contentHeight = Mathf.Max(scrollRect.height, cachedDisplayContentHeight);
            Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);
            float maxScrollY = Mathf.Max(0f, contentHeight - scrollRect.height);

            if (autoScroll && Event.current.type == EventType.Repaint)
                logScroll.y = maxScrollY;
            else
                logScroll.y = Mathf.Clamp(logScroll.y, 0f, maxScrollY);

            logScroll = GUI.BeginScrollView(scrollRect, logScroll, viewRect);

            int count = displayEntries != null ? displayEntries.Count : 0;
            if (count > 0)
            {
                int first = FindFirstVisibleRow(logScroll.y, count);
                int last = FindFirstVisibleRow(logScroll.y + scrollRect.height, count);
                last = Mathf.Min(count - 1, last + 2);

                for (int i = first; i <= last; i++)
                {
                    float rowHeight = i < cachedDisplayEntryHeights.Length ? cachedDisplayEntryHeights[i] : LogRowHeight;
                    float y = i < cachedDisplayEntryY.Length ? cachedDisplayEntryY[i] : i * LogRowHeight;
                    DrawLogRowAt(displayEntries[i], i, new Rect(0f, y, viewWidth, rowHeight));
                }
            }

            GUI.EndScrollView();
        }

        private int FindFirstVisibleRow(float y, int count)
        {
            if (count <= 0 || cachedDisplayEntryY == null || cachedDisplayEntryY.Length == 0)
                return 0;

            int low = 0;
            int high = count - 1;
            while (low < high)
            {
                int mid = (low + high) / 2;
                float rowY = cachedDisplayEntryY[mid];
                float rowHeight = mid < cachedDisplayEntryHeights.Length ? cachedDisplayEntryHeights[mid] : LogRowHeight;
                if (rowY + rowHeight < y)
                    low = mid + 1;
                else
                    high = mid;
            }

            return Mathf.Clamp(low, 0, count - 1);
        }

        private void BuildDisplayEntries(List<NeoConsoleLogEntry> entries, List<NeoConsoleLogDisplayEntry> result)
        {
            if (result == null)
                return;

            result.Clear();
            if (entries == null)
                return;

            if (!collapseLogs)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (ShouldShow(entries[i]))
                        result.Add(new NeoConsoleLogDisplayEntry(entries[i], 1));
                }
                return;
            }

            Dictionary<LogCollapseKey, NeoConsoleLogDisplayEntry> grouped = new Dictionary<LogCollapseKey, NeoConsoleLogDisplayEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                NeoConsoleLogEntry entry = entries[i];
                if (!ShouldShow(entry))
                    continue;

                LogCollapseKey key = new LogCollapseKey(entry);
                NeoConsoleLogDisplayEntry displayEntry;
                if (grouped.TryGetValue(key, out displayEntry))
                {
                    displayEntry.Count++;
                    grouped[key] = displayEntry;
                }
                else
                {
                    grouped.Add(key, new NeoConsoleLogDisplayEntry(entry, 1));
                }
            }

            foreach (KeyValuePair<LogCollapseKey, NeoConsoleLogDisplayEntry> pair in grouped)
                result.Add(pair.Value);

            result.Sort(CompareDisplayEntriesByEntryId);
        }

        private static int CompareDisplayEntriesByEntryId(NeoConsoleLogDisplayEntry left, NeoConsoleLogDisplayEntry right)
        {
            int leftId = left.Entry != null ? left.Entry.Id : 0;
            int rightId = right.Entry != null ? right.Entry.Id : 0;
            return leftId.CompareTo(rightId);
        }

        private bool ShouldShow(NeoConsoleLogEntry entry)
        {
            if (entry == null)
                return false;

            if (!NeoConsolePlusEditorSettings.ShowNeoCommandLogs && IsNeoCommandLog(entry.Message))
                return false;

            if (entry.Type == LogType.Log && !showLog)
                return false;

            if (entry.Type == LogType.Warning && !showWarning)
                return false;

            if ((entry.Type == LogType.Error || entry.Type == LogType.Exception || entry.Type == LogType.Assert) && !showError)
                return false;

            if (!string.IsNullOrEmpty(searchText))
            {
                string message = entry.Message != null ? StripRichTextTags(entry.Message) : string.Empty;
                string stack = entry.StackTrace ?? string.Empty;
                if (message.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0 &&
                    stack.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            return true;
        }

        private bool TryGetSelectedDisplayEntry(List<NeoConsoleLogDisplayEntry> displayEntries, out NeoConsoleLogDisplayEntry selectedEntry)
        {
            if (displayEntries != null)
            {
                for (int i = 0; i < displayEntries.Count; i++)
                {
                    NeoConsoleLogDisplayEntry entry = displayEntries[i];
                    if (entry.Entry != null && entry.Entry.Id == selectedLogEntryId)
                    {
                        selectedEntry = entry;
                        return true;
                    }
                }
            }

            selectedEntry = default(NeoConsoleLogDisplayEntry);
            return false;
        }

        private void CopyLogToClipboard(NeoConsoleLogDisplayEntry displayEntry)
        {
            if (displayEntry.Entry == null)
                return;

            StringBuilder builder = new StringBuilder();
            builder.Append("[").Append(GetTypeLabel(displayEntry.Entry.Type)).Append("] ");
            builder.Append(StripRichTextTags(displayEntry.Entry.Message));

            StackTraceLine[] scriptLines = GetScriptStackLines(displayEntry.Entry);
            if (scriptLines.Length > 0)
            {
                builder.AppendLine();
                for (int i = 0; i < scriptLines.Length; i++)
                    builder.AppendLine(scriptLines[i].DisplayText);
            }

            EditorGUIUtility.systemCopyBuffer = builder.ToString().TrimEnd();
        }

        private void ShowLogContextMenu(NeoConsoleLogDisplayEntry displayEntry)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy"), false, delegate { CopyLogToClipboard(displayEntry); });
            menu.AddItem(new GUIContent("Copy Message"), false, delegate
            {
                if (displayEntry.Entry != null)
                    EditorGUIUtility.systemCopyBuffer = StripRichTextTags(displayEntry.Entry.Message);
            });

            if (displayEntry.Entry != null && GetScriptStackLines(displayEntry.Entry).Length > 0)
                menu.AddItem(new GUIContent("Open First Script Trace"), false, delegate { OpenFirstScriptStackLine(displayEntry.Entry); });
            else
                menu.AddDisabledItem(new GUIContent("Open First Script Trace"));

            menu.ShowAsContext();
        }

        private void DrawScriptStackLines(StackTraceLine[] scriptLines)
        {
            if (scriptLines == null || scriptLines.Length == 0)
                return;

            for (int i = 0; i < scriptLines.Length; i++)
            {
                StackTraceLine line = scriptLines[i];
                Rect rect = GUILayoutUtility.GetRect(0f, 19f, GUILayout.ExpandWidth(true));
                Rect labelRect = new Rect(rect.x + 4f, rect.y + 1f, rect.width - 8f, rect.height - 2f);

                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

                if (Event.current.type == EventType.Repaint && i % 2 == 0)
                    EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f, 1f) : new Color(0.82f, 0.82f, 0.82f, 1f));

                GUI.Label(labelRect, line.DisplayText, CreateScriptStackLineStyle());

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition) && !ShouldIgnoreUnderlyingMouseForSuggestionOverlay())
                {
                    OpenScriptStackLine(line);
                    Event.current.Use();
                }
            }
        }

        private bool OpenFirstScriptStackLine(NeoConsoleLogEntry entry)
        {
            if (entry == null)
                return false;

            StackTraceLine[] lines = GetScriptStackLines(entry);
            if (lines.Length == 0)
                return false;

            OpenScriptStackLine(lines[0]);
            return true;
        }

        private static void OpenScriptStackLine(StackTraceLine line)
        {
            if (string.IsNullOrEmpty(line.Path))
                return;

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(line.Path);
            if (asset != null)
            {
                if (line.Line > 0)
                    AssetDatabase.OpenAsset(asset, line.Line);
                else
                    AssetDatabase.OpenAsset(asset);
            }
            else
            {
                InternalEditorUtility.OpenFileAtLineExternal(line.Path, Mathf.Max(1, line.Line));
            }
        }

        private void FocusCommandInputNow()
        {
            bool alreadyFocused = GUI.GetNameOfFocusedControl() == "NeoCommandInput" || commandInputFocusedLastFrame || commandInputActive;
            GUI.FocusControl("NeoCommandInput");
            if (!alreadyFocused)
                EditorGUI.FocusTextInControl("NeoCommandInput");

            EditorGUIUtility.editingTextField = true;
            commandInputFocusedLastFrame = true;
            commandInputActive = true;
        }

        private void FocusCommandInputWithoutSelecting()
        {
            // Do not repeatedly refocus an already focused TextArea. In IMGUI, forcing focus
            // during Layout/Used/Repaint passes can reset the TextEditor and visually select
            // the full input. commandInputActive and commandInputFocusedLastFrame are used as
            // guards because GUI.GetNameOfFocusedControl can be empty before the named control
            // is drawn in the current IMGUI pass.
            bool alreadyFocused = GUI.GetNameOfFocusedControl() == "NeoCommandInput" || commandInputFocusedLastFrame || commandInputActive;
            if (!alreadyFocused)
                GUI.FocusControl("NeoCommandInput");

            EditorGUIUtility.editingTextField = true;
            commandInputFocusedLastFrame = true;
            commandInputActive = true;
        }

        private void RequestCommandInputFocus()
        {
            requestCommandInputFocus = true;
            bool alreadyFocused = GUI.GetNameOfFocusedControl() == "NeoCommandInput" || commandInputFocusedLastFrame || commandInputActive;
            commandInputFocusFrames = alreadyFocused ? 1 : 4;
        }

        private void ClampDetailsHeightForCurrentWindow()
        {
            logDetailsHeight = Mathf.Clamp(logDetailsHeight, DetailsMinHeight, GetDynamicDetailsMaxHeight());
        }

        private float GetDynamicDetailsMaxHeight()
        {
            float max = position.height - DetailsReservedBottomHeight;
            return Mathf.Clamp(max, DetailsMinHeight, DetailsMaxHeight);
        }


        private bool ShouldIgnoreUnderlyingMouseForSuggestionOverlay()
        {
            Event current = Event.current;
            return current != null &&
                   current.type == EventType.MouseDown &&
                   hasLastSuggestionOverlayRect &&
                   lastSuggestionOverlayRect.Contains(current.mousePosition);
        }

        private static bool IsInternalNeoConsolePlusTrace(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/NeoConsolePlus/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("NeoConsolePlus/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("NeoConsolePlus/Runtime/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("NeoConsolePlus/NeoDebug/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ToggleNeoCommandLogs()
        {
            NeoConsolePlusEditorSettings.ShowNeoCommandLogs = !NeoConsolePlusEditorSettings.ShowNeoCommandLogs;
            InvalidateDisplayCache();
            Repaint();
        }

        private void ToggleCustomConsoleLogColor()
        {
            NeoConsolePlusEditorSettings.UseCustomConsoleLogColor = !NeoConsolePlusEditorSettings.UseCustomConsoleLogColor;
            InvalidateDisplayCache();
            Repaint();
        }

        private void ToggleClearOnPlay()
        {
            NeoConsolePlusEditorSettings.ClearOnPlay = !NeoConsolePlusEditorSettings.ClearOnPlay;
        }

        private void ToggleClearOnBuild()
        {
            NeoConsolePlusEditorSettings.ClearOnBuild = !NeoConsolePlusEditorSettings.ClearOnBuild;
        }

        private void ToggleClearOnRecompile()
        {
            NeoConsolePlusEditorSettings.ClearOnRecompile = !NeoConsolePlusEditorSettings.ClearOnRecompile;
        }

        private static bool IsNeoCommandLog(string message)
        {
            return NeoConsoleTextUtility.IsNeoCommandLog(message);
        }

        private static string GetDisplayMessage(NeoConsoleLogEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Message))
                return string.Empty;

            string message = entry.Message;
            if (ContainsColorRichText(message))
                return message;

            if (NeoConsolePlusEditorSettings.UseCustomConsoleLogColor && StartsWithBracketPrefix(message))
                return ColorizeFirstBracketPrefix(message, NeoConsolePlusEditorSettings.ConsoleLogColor);

            return message;
        }

        private static bool StartsWithBracketPrefix(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] != '[')
                return false;

            int closeIndex = value.IndexOf(']');
            return closeIndex > 0;
        }

        private static bool ContainsColorRichText(string value)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf("<color=", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ColorizeFirstBracketPrefix(string value, Color color)
        {
            if (string.IsNullOrEmpty(value) || value[0] != '[')
                return value;

            int closeIndex = value.IndexOf(']');
            if (closeIndex <= 0)
                return value;

            string prefix = value.Substring(0, closeIndex + 1);
            string suffix = value.Substring(closeIndex + 1);
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + prefix + "</color>" + suffix;
        }

        private static Color GetRowBackgroundColor(bool selected, int rowIndex)
        {
            if (selected)
                return EditorGUIUtility.isProSkin ? new Color(0.22f, 0.36f, 0.58f, 0.9f) : new Color(0.24f, 0.49f, 0.9f, 0.35f);

            if (rowIndex % 2 == 0)
                return EditorGUIUtility.isProSkin ? new Color(0.20f, 0.20f, 0.20f, 1f) : new Color(0.86f, 0.86f, 0.86f, 1f);

            return EditorGUIUtility.isProSkin ? new Color(0.17f, 0.17f, 0.17f, 1f) : new Color(0.80f, 0.80f, 0.80f, 1f);
        }

        private static Color GetSuggestionRowColor()
        {
            return EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f, 1f) : new Color(0.86f, 0.86f, 0.86f, 1f);
        }

        private static Color GetSelectedSuggestionColor()
        {
            return EditorGUIUtility.isProSkin ? new Color(0.20f, 0.28f, 0.38f, 1f) : new Color(0.55f, 0.72f, 0.95f, 0.75f);
        }

        private static void DrawLogIcon(Rect rect, LogType type)
        {
            Texture texture = GetLogIconTexture(type);
            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
        }

        private static Texture GetLogIconTexture(LogType type)
        {
            string[] names;
            switch (type)
            {
                case LogType.Warning:
                    names = new[] { "console.warnicon", "console.warnicon.sml" };
                    break;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    names = new[] { "console.erroricon", "console.erroricon.sml" };
                    break;
                default:
                    names = new[] { "console.infoicon", "console.infoicon.sml" };
                    break;
            }

            for (int i = 0; i < names.Length; i++)
            {
                Texture texture = EditorGUIUtility.FindTexture(names[i]);
                if (texture != null)
                    return texture;
            }

            GUIContent content = EditorGUIUtility.IconContent(names[0]);
            return content != null ? content.image : null;
        }

        private static Texture GetVisibilityIcon(bool active)
        {
            string[] names = active
                ? new[] { "scenevis_visible_hover", "animationvisibilitytoggleon", "ViewToolOrbit" }
                : new[] { "scenevis_hidden_hover", "animationvisibilitytoggleoff", "ViewToolOrbit" };

            for (int i = 0; i < names.Length; i++)
            {
                Texture texture = EditorGUIUtility.FindTexture(names[i]);
                if (texture != null)
                    return texture;
            }

            return null;
        }

        private static Texture GetConsoleWindowIcon()
        {
            string[] names = { "UnityEditor.ConsoleWindow", "d_UnityEditor.ConsoleWindow", "console.infoicon" };
            for (int i = 0; i < names.Length; i++)
            {
                Texture texture = EditorGUIUtility.FindTexture(names[i]);
                if (texture != null)
                    return texture;
            }

            GUIContent content = EditorGUIUtility.IconContent("console.infoicon");
            return content != null ? content.image : null;
        }

        private static GUIStyle CreateLogMessageStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.richText = true;
            style.clipping = TextClipping.Clip;
            style.wordWrap = false;
            style.alignment = TextAnchor.MiddleLeft;
            return style;
        }

        private static GUIStyle CreateLogSourceStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
            style.clipping = TextClipping.Clip;
            style.alignment = TextAnchor.MiddleRight;
            return style;
        }

        private static GUIStyle CreateLogCountStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel);
            style.alignment = TextAnchor.MiddleRight;
            return style;
        }

        private static GUIStyle CreateDetailsMessageStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.wordWrappedLabel);
            style.richText = true;
            return style;
        }

        private static GUIStyle CreateDetailsStackStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.textArea);
            style.wordWrap = false;
            return style;
        }

        private static GUIStyle CreateScriptStackLineStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
            style.clipping = TextClipping.Clip;
            style.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.64f, 0.78f, 1f, 1f) : new Color(0.05f, 0.24f, 0.55f, 1f);
            return style;
        }

        private static GUIStyle CreateCommandInputStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.textArea);
            style.fixedHeight = 0f;
            style.stretchHeight = true;
            style.wordWrap = true;
            style.clipping = TextClipping.Clip;
            style.alignment = TextAnchor.UpperLeft;
            style.padding = new RectOffset(5, 5, 3, 3);
            style.contentOffset = Vector2.zero;
            return style;
        }

        private static GUIStyle CreateCommandRunButtonStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniButton);
            style.fixedHeight = 0f;
            style.alignment = TextAnchor.MiddleCenter;
            style.padding = new RectOffset(4, 4, 2, 2);
            style.contentOffset = Vector2.zero;
            return style;
        }

        private static GUIStyle CreateInlineCommandGhostStyle(GUIStyle inputStyle, bool richText)
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.font = inputStyle.font;
            style.fontSize = inputStyle.fontSize;
            style.fontStyle = inputStyle.fontStyle;
            style.fixedHeight = 0f;
            style.stretchHeight = true;
            style.wordWrap = true;
            style.richText = richText;
            style.clipping = TextClipping.Clip;
            style.alignment = TextAnchor.UpperLeft;
            style.padding = new RectOffset(inputStyle.padding.left, inputStyle.padding.right, inputStyle.padding.top, inputStyle.padding.bottom);
            style.contentOffset = inputStyle.contentOffset;
            style.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.62f, 0.62f, 0.62f, 0.75f) : new Color(0.32f, 0.32f, 0.32f, 0.65f);
            style.hover.textColor = style.normal.textColor;
            style.active.textColor = style.normal.textColor;
            style.focused.textColor = style.normal.textColor;
            return style;
        }

        private static GUIStyle CreateCommandGhostStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.wordWrap = true;
            style.clipping = TextClipping.Clip;
            style.alignment = TextAnchor.UpperLeft;
            style.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.62f, 0.62f, 0.62f, 0.75f) : new Color(0.32f, 0.32f, 0.32f, 0.65f);
            style.hover.textColor = style.normal.textColor;
            style.active.textColor = style.normal.textColor;
            return style;
        }

        private static GUIStyle CreateSuggestionStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.clipping = TextClipping.Clip;
            style.hover.textColor = style.normal.textColor;
            style.active.textColor = style.normal.textColor;
            style.focused.textColor = style.normal.textColor;
            return style;
        }

        private static GUIStyle CreateSuggestionSelectedStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.clipping = TextClipping.Clip;
            style.normal.textColor = EditorGUIUtility.isProSkin ? Color.yellow : Color.black;
            style.hover.textColor = style.normal.textColor;
            style.active.textColor = style.normal.textColor;
            style.focused.textColor = style.normal.textColor;
            return style;
        }

        private static GUIStyle CreateSuggestionDescriptionStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
            style.clipping = TextClipping.Clip;
            return style;
        }

        private static GUIStyle CreateReadOnlyCodeStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.wordWrap = false;
            style.clipping = TextClipping.Clip;
            style.font = EditorStyles.miniLabel.font;
            return style;
        }


        private static void DrawTruncatedLogLabel(Rect rect, string value)
        {
            GUIStyle style = CreateLogMessageStyle();
            GUI.Label(rect, TruncateRichTextToFit(value, style, rect.width), style);
        }

        private static void DrawWrappedLogLabel(Rect rect, string value)
        {
            GUIStyle style = CreateLogMessageStyle();
            style.wordWrap = true;
            style.clipping = TextClipping.Clip;
            style.alignment = TextAnchor.UpperLeft;

            bool truncated = IsRichTextTruncatedToMaxLines(value, style, rect.width, LogMessageMaxVisibleLines);
            string displayValue = truncated ? TruncateRichTextToMaxLines(value, style, rect.width, LogMessageMaxVisibleLines) : MakeSingleLine(value);
            string tooltip = truncated ? StripRichTextTags(MakeSingleLine(value)) : string.Empty;
            GUI.Label(rect, new GUIContent(displayValue, tooltip), style);
        }

        private static bool IsRichTextTruncatedToMaxLines(string value, GUIStyle style, float width, int maxLines)
        {
            return NeoConsoleTextUtility.IsRichTextTruncatedToMaxLines(value, style, width, maxLines);
        }

        private static bool IsPlainTextTruncatedToFit(string value, GUIStyle style, float width)
        {
            return NeoConsoleTextUtility.IsPlainTextTruncatedToFit(value, style, width);
        }

        private static string TruncateRichTextToMaxLines(string value, GUIStyle style, float width, int maxLines)
        {
            return NeoConsoleTextUtility.TruncateRichTextToMaxLines(value, style, width, maxLines);
        }



        private static string TruncateRichTextToFit(string value, GUIStyle style, float width)
        {
            return NeoConsoleTextUtility.TruncateRichTextToFit(value, style, width);
        }


        private static string TruncatePlainTextToFit(string value, GUIStyle style, float width)
        {
            return NeoConsoleTextUtility.TruncatePlainTextToFit(value, style, width);
        }

        private static string MakeSingleLine(string value)
        {
            return NeoConsoleTextUtility.MakeSingleLine(value);
        }

        private static float CalculateDetailsMessageHeight(string value)
        {
            string safeValue = string.IsNullOrEmpty(value) ? string.Empty : value;
            float width = Mathf.Max(180f, EditorGUIUtility.currentViewWidth - 42f);
            return CreateDetailsMessageStyle().CalcHeight(new GUIContent(safeValue), width) + 8f;
        }

        private static float CalculateStackHeight(string value)
        {
            string safeValue = string.IsNullOrEmpty(value) ? string.Empty : value;
            float width = Mathf.Max(180f, EditorGUIUtility.currentViewWidth - 42f);
            return Mathf.Max(54f, CreateDetailsStackStyle().CalcHeight(new GUIContent(safeValue), width) + 8f);
        }

        private static string GetFirstRelevantStackLine(NeoConsoleLogEntry entry)
        {
            StackTraceLine line = GetFirstRelevantStackLineInfo(entry);
            return GetStackPathDisplay(line);
        }

        private static StackTraceLine GetFirstRelevantStackLineInfo(NeoConsoleLogEntry entry)
        {
            StackTraceLine[] lines = GetScriptStackLines(entry);
            return lines.Length > 0 ? lines[0] : default;
        }

        private static string GetStackPathDisplay(StackTraceLine line)
        {
            if (string.IsNullOrEmpty(line.Path))
                return string.Empty;

            return line.Path + (line.Line > 0 ? ":" + line.Line.ToString() : string.Empty);
        }

        private static string GetStackTooltip(StackTraceLine line)
        {
            string path = GetStackPathDisplay(line);
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            if (string.IsNullOrEmpty(line.Method))
                return path;

            return path + "\n" + line.Method;
        }

        private static StackTraceLine[] GetScriptStackLines(NeoConsoleLogEntry entry)
        {
            if (entry == null)
                return new StackTraceLine[0];

            StackTraceLine[] cached;
            if (ScriptStackLineCache.TryGetValue(entry.Id, out cached))
                return cached;

            List<StackTraceLine> result = new List<StackTraceLine>();
            AddScriptStackLinesFromText(result, entry.Message, true);
            AddScriptStackLinesFromText(result, entry.StackTrace, false);
            cached = DistinctScriptStackLines(result).ToArray();
            ScriptStackLineCache[entry.Id] = cached;
            return cached;
        }

        private static StackTraceLine[] GetScriptStackLines(string stackTrace)
        {
            List<StackTraceLine> result = new List<StackTraceLine>();
            AddScriptStackLinesFromText(result, stackTrace, false);
            return DistinctScriptStackLines(result).ToArray();
        }

        private static void AddScriptStackLinesFromText(List<StackTraceLine> result, string text, bool allowCompileErrorFormat)
        {
            if (result == null || string.IsNullOrEmpty(text))
                return;

            string plainText = StripRichTextTags(text);
            string[] lines = plainText.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                StackTraceLine parsed;
                if (allowCompileErrorFormat && TryParseCompileErrorLine(lines[i], out parsed))
                {
                    result.Add(parsed);
                    continue;
                }

                if (TryParseScriptStackLine(lines[i], out parsed))
                    result.Add(parsed);
            }
        }

        private static List<StackTraceLine> DistinctScriptStackLines(List<StackTraceLine> lines)
        {
            List<StackTraceLine> result = new List<StackTraceLine>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (lines == null)
                return result;

            for (int i = 0; i < lines.Count; i++)
            {
                StackTraceLine line = lines[i];
                string key = (line.Path ?? string.Empty).Replace('\\', '/') + ":" + line.Line;
                if (seen.Add(key))
                    result.Add(line);
            }

            return result;
        }

        private static bool TryParseCompileErrorLine(string rawLine, out StackTraceLine parsed)
        {
            parsed = default;
            if (string.IsNullOrEmpty(rawLine))
                return false;

            string line = rawLine.Trim();
            int extensionIndex = line.IndexOf(".cs(", StringComparison.OrdinalIgnoreCase);
            if (extensionIndex < 0)
                return false;

            int locationStart = FindScriptPathStart(line, extensionIndex);
            if (locationStart < 0)
                return false;

            int parenStart = extensionIndex + 3;
            int parenEnd = line.IndexOf(')', parenStart);
            if (parenEnd <= parenStart)
                return false;

            string path = line.Substring(locationStart, extensionIndex + 3 - locationStart).Replace('\\', '/');
            if (!IsOpenableScriptPath(path, true))
                return false;

            string lineAndColumn = line.Substring(parenStart + 1, parenEnd - parenStart - 1);
            string[] parts = lineAndColumn.Split(',');
            int lineNumber = 0;
            if (parts.Length == 0 || !int.TryParse(parts[0], out lineNumber))
                lineNumber = 1;

            string message = line.Substring(parenEnd + 1).TrimStart(':', ' ');
            string display = path + ":" + Mathf.Max(1, lineNumber);
            if (!string.IsNullOrEmpty(message))
                display += "  —  " + message;

            parsed = new StackTraceLine(path, Mathf.Max(1, lineNumber), display, message);
            return true;
        }

        private static int FindScriptPathStart(string line, int extensionIndex)
        {
            string[] markers = { "Assets/", "Assets\\", "Packages/", "Packages\\", "Library/PackageCache/", "Library\\PackageCache\\" };
            int best = -1;
            for (int i = 0; i < markers.Length; i++)
            {
                int index = line.LastIndexOf(markers[i], extensionIndex, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && (best < 0 || index < best))
                    best = index;
            }

            return best;
        }

        private static bool IsOpenableScriptPath(string path)
        {
            return IsOpenableScriptPath(path, false);
        }

        private static bool IsOpenableScriptPath(string path, bool allowInternalNeoConsolePlusPath)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return false;

            string normalized = path.Replace('\\', '/');
            if (!allowInternalNeoConsolePlusPath && IsInternalNeoConsolePlusTrace(normalized))
                return false;

            return normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Library/PackageCache/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseScriptStackLine(string rawLine, out StackTraceLine parsed)
        {
            parsed = default;
            if (string.IsNullOrEmpty(rawLine))
                return false;

            string line = rawLine.Trim();
            const string marker = "(at ";
            int markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return false;

            int start = markerIndex + marker.Length;
            int end = line.LastIndexOf(')');
            if (end <= start)
                end = line.Length;

            string location = line.Substring(start, end - start).Trim();
            if (string.IsNullOrEmpty(location))
                return false;

            int colonIndex = location.LastIndexOf(':');
            string path = colonIndex >= 0 ? location.Substring(0, colonIndex) : location;
            int lineNumber = 0;
            if (colonIndex >= 0)
                int.TryParse(location.Substring(colonIndex + 1), out lineNumber);

            path = path.Replace('\\', '/');
            if (!IsOpenableScriptPath(path))
                return false;

            string method = line.Substring(0, markerIndex).Trim();
            string simplifiedMethod = SimplifyMethodName(method);
            string display = path + (lineNumber > 0 ? ":" + lineNumber : string.Empty);
            if (!string.IsNullOrEmpty(simplifiedMethod))
                display = display + "  —  " + simplifiedMethod;

            parsed = new StackTraceLine(path, lineNumber, display, simplifiedMethod);
            return true;
        }

        private static string SimplifyMethodName(string method)
        {
            if (string.IsNullOrEmpty(method))
                return string.Empty;

            int paren = method.IndexOf('(');
            string beforeParams = paren >= 0 ? method.Substring(0, paren) : method;
            int lastDot = beforeParams.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < beforeParams.Length - 1)
            {
                int previousDot = beforeParams.LastIndexOf('.', Mathf.Max(0, lastDot - 1));
                if (previousDot >= 0 && previousDot < lastDot)
                    return beforeParams.Substring(previousDot + 1) + (paren >= 0 ? "()" : string.Empty);
            }

            return beforeParams + (paren >= 0 ? "()" : string.Empty);
        }

        private static string SimplifyStackLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            const string marker = "(at ";
            int markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0)
                return line.Substring(markerIndex + marker.Length).TrimEnd(')');

            return line;
        }

        private static string FormatTime(double timeSinceStartup)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0d, timeSinceStartup));
            return time.ToString(@"mm\:ss\.fff");
        }

        private string GetTypeLabel(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return "Warning";
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return "Error";
                default:
                    return "Log";
            }
        }

        private static string StripRichTextTags(string value)
        {
            return NeoConsoleTextUtility.StripRichTextTags(value);
        }

        private void OnEntryAdded(NeoConsoleLogEntry entry)
        {
            selectedLogEntryId = entry != null ? entry.Id : selectedLogEntryId;
            if (pendingRepaint)
                return;

            pendingRepaint = true;
            EditorApplication.delayCall += DelayedRepaint;
        }

        private void OnLogsCleared()
        {
            selectedLogEntryId = -1;
            ScriptStackLineCache.Clear();
            InvalidateDisplayCache();
            Repaint();
        }

        private void DelayedRepaint()
        {
            pendingRepaint = false;
            if (this != null)
                Repaint();
        }

        private struct StackTraceLine
        {
            public StackTraceLine(string path, int line, string displayText, string method)
            {
                Path = path ?? string.Empty;
                Line = line;
                DisplayText = displayText ?? string.Empty;
                Method = method ?? string.Empty;
            }

            public readonly string Path;
            public readonly int Line;
            public readonly string DisplayText;
            public readonly string Method;
        }

        private struct LogCollapseKey : IEquatable<LogCollapseKey>
        {
            public LogCollapseKey(NeoConsoleLogEntry entry)
            {
                Type = entry != null ? entry.Type : LogType.Log;
                Message = entry != null ? entry.Message ?? string.Empty : string.Empty;
                StackTrace = entry != null ? entry.StackTrace ?? string.Empty : string.Empty;
            }

            public readonly LogType Type;
            public readonly string Message;
            public readonly string StackTrace;

            public bool Equals(LogCollapseKey other)
            {
                return Type == other.Type &&
                       string.Equals(Message, other.Message, StringComparison.Ordinal) &&
                       string.Equals(StackTrace, other.StackTrace, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is LogCollapseKey && Equals((LogCollapseKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)Type;
                    hash = (hash * 397) ^ (Message != null ? Message.GetHashCode() : 0);
                    hash = (hash * 397) ^ (StackTrace != null ? StackTrace.GetHashCode() : 0);
                    return hash;
                }
            }
        }

        private struct NeoConsoleLogDisplayEntry
        {
            public NeoConsoleLogDisplayEntry(NeoConsoleLogEntry entry, int count)
            {
                Entry = entry;
                Count = count;
            }

            public NeoConsoleLogEntry Entry;
            public int Count;
        }
    }
}
#endif
