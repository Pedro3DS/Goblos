#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Neo.ConsolePlus
{
    internal sealed class NeoCommandSuggestion
    {
        public string Completion;
        public string DisplayText;
        public string Hint;
        public bool HasSuggestion;
        public int CursorIndex;
        public NeoCommandInfo Command;
        public bool IsTargetSuggestion;
        public bool IsInformationalOnly;
        public string Description;
    }

    internal static class NeoCommandAutoComplete
    {
        public const char CommandPrefix = '/';

        private static readonly NeoCommandSuggestion[] EmptySuggestions = new NeoCommandSuggestion[0];
        private static readonly Dictionary<Type, string> SchemaCache = new Dictionary<Type, string>();

        private static string cachedVisibleInput = string.Empty;
        private static int cachedVisibleCursorIndex = -1;
        private static NeoCommandExecutionContext cachedVisibleContext = NeoCommandExecutionContext.Runtime;
        private static int cachedVisibleRegistryVersion = -1;
        private static int cachedVisibleTargetVersion = -1;
        private static NeoCommandSuggestion[] cachedVisibleSuggestions = EmptySuggestions;

        public static NeoCommandInfo[] GetMatches(string input)
        {
            return GetMatches(input, NeoCommandRegistry.GetCommands());
        }

        public static NeoCommandInfo[] GetMatches(string input, NeoCommandExecutionContext context)
        {
            return GetMatches(input, NeoCommandRegistry.GetCommands(context));
        }

        public static bool HasResolvedCommand(string input)
        {
            NeoCommandInfo command;
            string leading;
            string normalized;
            return TryResolveCommand(input ?? string.Empty, NeoCommandRegistry.GetCommands(), out command, out leading, out normalized);
        }

        public static bool HasResolvedCommand(string input, NeoCommandExecutionContext context)
        {
            NeoCommandInfo command;
            string leading;
            string normalized;
            return TryResolveCommand(input ?? string.Empty, NeoCommandRegistry.GetCommands(context), out command, out leading, out normalized);
        }

        public static NeoCommandInfo[] GetMatches(string input, NeoCommandInfo[] commands)
        {
            if (commands == null || commands.Length == 0)
                return new NeoCommandInfo[0];

            string token = GetCommandTokenWithoutPrefix(input);
            bool commandNameFinished = CommandNameIsFinishedAfterPrefix(input);
            List<NeoCommandInfo> matches = new List<NeoCommandInfo>();

            for (int i = 0; i < commands.Length; i++)
            {
                NeoCommandInfo command = commands[i];
                if (command == null)
                    continue;

                if (!string.IsNullOrEmpty(token))
                {
                    bool include = commandNameFinished
                        ? string.Equals(command.Name, token, StringComparison.OrdinalIgnoreCase)
                        : command.Name.StartsWith(token, StringComparison.OrdinalIgnoreCase);

                    if (!include)
                        continue;
                }

                matches.Add(command);
            }

            matches.Sort(CompareMatchesByLengthThenName);
            return matches.ToArray();
        }

        private static int CompareMatchesByLengthThenName(NeoCommandInfo left, NeoCommandInfo right)
        {
            string leftName = left != null ? left.Name : string.Empty;
            string rightName = right != null ? right.Name : string.Empty;
            int lengthComparison = leftName.Length.CompareTo(rightName.Length);
            return lengthComparison != 0 ? lengthComparison : string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }

        public static NeoCommandSuggestion[] GetVisibleSuggestions(string input)
        {
            string safeInput = input ?? string.Empty;
            return GetVisibleSuggestions(safeInput, safeInput.Length, NeoCommandRegistry.GetCommands());
        }

        public static NeoCommandSuggestion[] GetVisibleSuggestions(string input, NeoCommandExecutionContext context)
        {
            string safeInput = input ?? string.Empty;
            return GetVisibleSuggestionsCached(safeInput, safeInput.Length, context);
        }

        public static NeoCommandSuggestion[] GetVisibleSuggestions(string input, int cursorIndex)
        {
            return GetVisibleSuggestions(input, cursorIndex, NeoCommandRegistry.GetCommands());
        }

        public static NeoCommandSuggestion[] GetVisibleSuggestions(string input, int cursorIndex, NeoCommandExecutionContext context)
        {
            return GetVisibleSuggestionsCached(input, cursorIndex, context);
        }

        private static NeoCommandSuggestion[] GetVisibleSuggestionsCached(string input, int cursorIndex, NeoCommandExecutionContext context)
        {
            string safeInput = input ?? string.Empty;
            int safeCursorIndex = Mathf.Clamp(cursorIndex, 0, safeInput.Length);
            int registryVersion = NeoCommandRegistry.Version;
            int targetVersion = NeoCommandTargetCache.Version;

            if (cachedVisibleSuggestions != null &&
                cachedVisibleCursorIndex == safeCursorIndex &&
                cachedVisibleContext == context &&
                cachedVisibleRegistryVersion == registryVersion &&
                cachedVisibleTargetVersion == targetVersion &&
                string.Equals(cachedVisibleInput, safeInput, StringComparison.Ordinal))
            {
                return cachedVisibleSuggestions;
            }

            NeoCommandSuggestion[] suggestions = GetVisibleSuggestions(safeInput, safeCursorIndex, NeoCommandRegistry.GetCommands(context), context);
            cachedVisibleInput = safeInput;
            cachedVisibleCursorIndex = safeCursorIndex;
            cachedVisibleContext = context;
            cachedVisibleRegistryVersion = registryVersion;
            cachedVisibleTargetVersion = targetVersion;
            cachedVisibleSuggestions = suggestions ?? EmptySuggestions;
            return cachedVisibleSuggestions;
        }

        private static NeoCommandSuggestion[] GetVisibleSuggestions(string input, int cursorIndex, NeoCommandInfo[] commands)
        {
            return GetVisibleSuggestions(input, cursorIndex, commands, NeoCommandExecutionContext.Runtime);
        }

        private static NeoCommandSuggestion[] GetVisibleSuggestions(string input, int cursorIndex, NeoCommandInfo[] commands, NeoCommandExecutionContext context)
        {
            string safeInput = input ?? string.Empty;
            int safeCursorIndex = Mathf.Clamp(cursorIndex, 0, safeInput.Length);

            if (safeCursorIndex >= safeInput.Length)
                return GetVisibleSuggestionsCore(safeInput, commands, context);

            string prefix = safeInput.Substring(0, safeCursorIndex);
            string suffix = safeInput.Substring(safeCursorIndex);
            NeoCommandSuggestion[] prefixSuggestions = GetVisibleSuggestionsCore(prefix, commands, context);
            if (prefixSuggestions == null || prefixSuggestions.Length == 0)
                return GetVisibleSuggestionsCore(safeInput, commands, context);

            NeoCommandSuggestion[] merged = new NeoCommandSuggestion[prefixSuggestions.Length];
            for (int i = 0; i < prefixSuggestions.Length; i++)
            {
                NeoCommandSuggestion mergedSuggestion;
                if (!TryMergeSuggestionSuffix(prefix, suffix, prefixSuggestions[i], out mergedSuggestion))
                    return GetVisibleSuggestionsCore(safeInput, commands, context);

                merged[i] = mergedSuggestion;
            }

            return merged;
        }

        private static NeoCommandSuggestion[] GetVisibleSuggestionsCore(string input, NeoCommandInfo[] commands, NeoCommandExecutionContext context)
        {
            string safeInput = input ?? string.Empty;
            if (!LooksLikeCommandInput(safeInput))
                return EmptySuggestions;

            if (!CommandNameIsFinishedAfterPrefix(safeInput))
            {
                NeoCommandInfo[] matches = GetMatches(safeInput, commands);
                NeoCommandSuggestion[] commandSuggestions = new NeoCommandSuggestion[matches.Length];
                for (int i = 0; i < matches.Length; i++)
                    commandSuggestions[i] = GetSuggestionForCommand(safeInput, matches[i]);

                return commandSuggestions;
            }

            NeoCommandInfo command;
            string leading;
            string normalized;
            if (!TryResolveCommand(safeInput, commands, out command, out leading, out normalized))
                return EmptySuggestions;

            string targetPrefix;
            if (TryGetTargetSuggestionContext(safeInput, command, out targetPrefix))
            {
                NeoCommandSuggestion[] targetSuggestions = GetTargetArgumentSuggestions(safeInput, command, targetPrefix);
                if (targetSuggestions.Length > 0)
                    return targetSuggestions;
            }

            NeoCommandSuggestion[] builtInValueSuggestions;
            if (TryGetBuiltInValueSuggestions(safeInput, command, leading, normalized, context, out builtInValueSuggestions) &&
                builtInValueSuggestions != null && builtInValueSuggestions.Length > 0)
            {
                return builtInValueSuggestions;
            }

            SelectableValueContext selectableContext;
            if (TryResolveSelectableValueContext(safeInput, command, leading, normalized, out selectableContext))
            {
                NeoCommandSuggestion[] selectableSuggestions = BuildSelectableValueSuggestions(selectableContext);
                if (selectableSuggestions != null && selectableSuggestions.Length > 0)
                    return selectableSuggestions;
            }

            NeoCommandSuggestion hintSuggestion;

            // Main typed-value completion path. This is intentionally placed before the
            // informational parameter hint, because the hint should never hide a real ghost.
            // It handles single-parameter custom objects and all nested types inside them:
            // objects, lists/arrays, dictionaries, enum/bool fields, and primitive fields.
            if (TryGetSingleParameterTypedCompletionSuggestion(safeInput, command, leading, normalized, out hintSuggestion))
                return new[] { hintSuggestion };

            // Strong object-continuation fallback. This is intentionally executed before the
            // generic argument hint because custom object values may contain balanced nested
            // objects while the parent object is still open. Example:
            // /test.setitem { ..., MainStat: { ..., IsPercent: false }|
            // must suggest ", BonusStats: [" without requiring the user to type the comma first.
            if (TryGetSingleParameterFieldContinuationSuggestion(safeInput, command, leading, normalized, out hintSuggestion))
                return new[] { hintSuggestion };

            if (TryGetArgumentContextSuggestion(safeInput, commands, out hintSuggestion))
                return new[] { hintSuggestion };

            // Fallback for deep object editing. Some incomplete top-level object values can be
            // hard for the generic argument splitter to classify while the user is still inside
            // nested braces. When the command has a single method argument, treat everything after
            // the command name as that argument and let the typed value completion engine decide
            // the next step. This keeps ghosts working for cases like:
            // /test.setitem { ..., Rarity: Rare, |
            // /test.setitem { ..., MainStat: { ..., IsPercent: false }|
            if (TryGetSingleParameterFallbackSuggestion(safeInput, command, leading, normalized, out hintSuggestion))
                return new[] { hintSuggestion };

            return EmptySuggestions;
        }

        public static NeoCommandSuggestion GetSuggestion(string input)
        {
            return GetSuggestion(input, 0, NeoCommandRegistry.GetCommands());
        }

        public static NeoCommandSuggestion GetSuggestion(string input, NeoCommandExecutionContext context)
        {
            string safeInput = input ?? string.Empty;
            NeoCommandSuggestion[] suggestions = GetVisibleSuggestionsCached(safeInput, safeInput.Length, context);
            return suggestions.Length > 0 ? suggestions[0] : Empty(safeInput);
        }

        public static NeoCommandSuggestion GetSuggestion(string input, int selectedMatchIndex)
        {
            return GetSuggestion(input, selectedMatchIndex, NeoCommandRegistry.GetCommands());
        }

        public static NeoCommandSuggestion GetSuggestion(string input, int selectedMatchIndex, NeoCommandExecutionContext context)
        {
            string safeInput = input ?? string.Empty;
            NeoCommandSuggestion[] suggestions = GetVisibleSuggestionsCached(safeInput, safeInput.Length, context);
            if (suggestions.Length == 0)
                return Empty(safeInput);

            int index = Mathf.Clamp(selectedMatchIndex, 0, suggestions.Length - 1);
            return suggestions[index];
        }

        private static NeoCommandSuggestion GetSuggestion(string input, int selectedMatchIndex, NeoCommandInfo[] commands)
        {
            string safeInput = input ?? string.Empty;
            NeoCommandSuggestion[] suggestions = GetVisibleSuggestions(safeInput, safeInput.Length, commands);
            if (suggestions.Length == 0)
                return Empty(safeInput);

            int index = Mathf.Clamp(selectedMatchIndex, 0, suggestions.Length - 1);
            return suggestions[index];
        }

        public static NeoCommandSuggestion GetSuggestion(string input, int selectedMatchIndex, int cursorIndex)
        {
            return GetSuggestion(input, selectedMatchIndex, cursorIndex, NeoCommandRegistry.GetCommands());
        }

        public static NeoCommandSuggestion GetSuggestion(string input, int selectedMatchIndex, int cursorIndex, NeoCommandExecutionContext context)
        {
            string safeInput = input ?? string.Empty;
            NeoCommandSuggestion[] suggestions = GetVisibleSuggestionsCached(safeInput, cursorIndex, context);
            if (suggestions.Length == 0)
                return Empty(safeInput);

            int index = Mathf.Clamp(selectedMatchIndex, 0, suggestions.Length - 1);
            return suggestions[index];
        }

        private static NeoCommandSuggestion GetSuggestion(string input, int selectedMatchIndex, int cursorIndex, NeoCommandInfo[] commands)
        {
            string safeInput = input ?? string.Empty;
            int safeCursorIndex = Mathf.Clamp(cursorIndex, 0, safeInput.Length);

            if (safeCursorIndex >= safeInput.Length)
                return GetSuggestion(safeInput, selectedMatchIndex, commands);

            string prefix = safeInput.Substring(0, safeCursorIndex);
            string suffix = safeInput.Substring(safeCursorIndex);
            NeoCommandSuggestion prefixSuggestion = GetSuggestion(prefix, selectedMatchIndex, commands);
            if (prefixSuggestion == null || !prefixSuggestion.HasSuggestion || string.IsNullOrEmpty(prefixSuggestion.Completion))
                return GetSuggestion(safeInput, selectedMatchIndex, commands);

            if (string.Equals(prefixSuggestion.Completion, prefix, StringComparison.Ordinal))
                return GetSuggestion(safeInput, selectedMatchIndex, commands);

            NeoCommandSuggestion mergedSuggestion;
            if (TryMergeSuggestionSuffix(prefix, suffix, prefixSuggestion, out mergedSuggestion))
                return mergedSuggestion;

            return GetSuggestion(safeInput, selectedMatchIndex, commands);
        }

        public static NeoCommandSuggestion GetSuggestionForCommand(string input, NeoCommandInfo command)
        {
            string safeInput = input ?? string.Empty;
            if (command == null)
                return Empty(safeInput);

            string trimmedStart = safeInput.TrimStart();
            int leadingSpaces = safeInput.Length - trimmedStart.Length;
            string leading = leadingSpaces > 0 ? safeInput.Substring(0, leadingSpaces) : string.Empty;

            bool hasPrefix = trimmedStart.StartsWith(CommandPrefix.ToString(), StringComparison.Ordinal);
            string normalized = hasPrefix ? trimmedStart.Substring(1) : trimmedStart;
            bool commandFinished = CommandNameIsFinished(normalized);

            CompletionBuild completion;
            string hint;

            if (!commandFinished)
            {
                completion = BuildCommandCompletion(command);
                hint = BuildCommandParameterListHint(command);
            }
            else
            {
                completion = BuildArgumentCompletion(normalized, command);
                hint = BuildNextArgumentHint(normalized, command);
                if (string.IsNullOrEmpty(hint))
                    hint = command.Signature;
            }

            string fullCompletion = leading + CommandPrefix + completion.Text;
            return new NeoCommandSuggestion
            {
                Completion = fullCompletion,
                DisplayText = CommandPrefix + command.Name,
                Hint = hint,
                HasSuggestion = true,
                CursorIndex = leading.Length + 1 + completion.CursorIndex,
                Command = command,
                IsTargetSuggestion = false,
                IsInformationalOnly = false,
                Description = hint
            };
        }

        private static bool TryMergeSuggestionSuffix(string prefix, string suffix, NeoCommandSuggestion suggestion, out NeoCommandSuggestion mergedSuggestion)
        {
            mergedSuggestion = suggestion;

            if (suggestion == null || string.IsNullOrEmpty(suggestion.Completion) ||
                string.Equals(suggestion.Completion, prefix, StringComparison.Ordinal))
            {
                return true;
            }

            string preservedSuffix;
            if (!TryGetSuffixAfterCursorCompletion(prefix, suffix, suggestion.Completion, out preservedSuffix))
                return false;

            string mergedCompletionText = suggestion.Completion + preservedSuffix;

            // If the text after the cursor already contains the whole inserted suffix,
            // the merge result can be exactly the current input. This is not a merge
            // failure: falling back to the full-input suggestion path can create a
            // duplicate ghost over the already typed text, especially in malformed
            // objects such as:
            //   { ItemId: "" Di|splayName: ""
            // In that case keep the current input as the completion so the UI draws
            // no inline ghost.

            mergedSuggestion = new NeoCommandSuggestion
            {
                Completion = mergedCompletionText,
                DisplayText = suggestion.DisplayText,
                Hint = suggestion.Hint,
                HasSuggestion = suggestion.HasSuggestion,
                CursorIndex = suggestion.CursorIndex,
                Command = suggestion.Command,
                IsTargetSuggestion = suggestion.IsTargetSuggestion,
                IsInformationalOnly = suggestion.IsInformationalOnly,
                Description = suggestion.Description
            };

            return true;
        }


        private static bool TryGetSingleParameterTypedCompletionSuggestion(string input, NeoCommandInfo command, string leading, string normalized, out NeoCommandSuggestion suggestion)
        {
            suggestion = null;

            if (command == null)
                return false;

            CompletionParameter[] parameters = GetCompletionParameters(command);
            if (parameters.Length != 1)
                return false;

            CompletionParameter parameter = parameters[0];
            if (parameter.IsTarget)
                return false;

            string safeNormalized = normalized ?? string.Empty;
            string commandName = GetCommandNameFromNormalizedInput(safeNormalized);
            if (string.IsNullOrEmpty(commandName) || commandName.Length >= safeNormalized.Length)
                return false;

            int firstWhitespaceAfterCommand = commandName.Length;
            if (!char.IsWhiteSpace(safeNormalized[firstWhitespaceAfterCommand]))
                return false;

            int valueStart = firstWhitespaceAfterCommand + 1;
            while (valueStart < safeNormalized.Length && char.IsWhiteSpace(safeNormalized[valueStart]))
                valueStart++;

            string prefixBeforeValue = safeNormalized.Substring(0, valueStart);
            string currentValue = valueStart < safeNormalized.Length ? safeNormalized.Substring(valueStart) : string.Empty;

            CompletionBuild valueCompletion = BuildValueCompletion(currentValue, parameter.Type, parameter.Name);
            if (string.IsNullOrEmpty(valueCompletion.Text) || string.Equals(valueCompletion.Text, currentValue, StringComparison.Ordinal))
                return false;

            string completionNormalized = prefixBeforeValue + valueCompletion.Text;
            string fullCompletion = leading + CommandPrefix + completionNormalized;

            HintContext hintContext = ResolveHintContext(valueCompletion.Text, parameter.Type, parameter.Name);
            string label = BuildValueLabel(hintContext.Type, hintContext.Name, parameter.IsTarget);
            string hint = BuildValueWritingHint(hintContext.Type, hintContext.Name);

            suggestion = new NeoCommandSuggestion
            {
                Completion = fullCompletion,
                DisplayText = label,
                Hint = hint,
                HasSuggestion = true,
                CursorIndex = leading.Length + 1 + prefixBeforeValue.Length + valueCompletion.CursorIndex,
                Command = command,
                IsTargetSuggestion = parameter.IsTarget,
                IsInformationalOnly = true,
                Description = hint
            };

            return true;
        }

        private static bool TryGetSingleParameterFieldContinuationSuggestion(string input, NeoCommandInfo command, string leading, string normalized, out NeoCommandSuggestion suggestion)
        {
            suggestion = null;

            if (command == null)
                return false;

            CompletionParameter[] parameters = GetCompletionParameters(command);
            if (parameters.Length != 1)
                return false;

            CompletionParameter parameter = parameters[0];
            if (!NeoCommandRegistry.IsCustomJsonParameterType(parameter.Type))
                return false;

            string safeNormalized = normalized ?? string.Empty;
            string commandName = GetCommandNameFromNormalizedInput(safeNormalized);
            if (string.IsNullOrEmpty(commandName) || commandName.Length >= safeNormalized.Length)
                return false;

            int firstWhitespaceAfterCommand = commandName.Length;
            if (!char.IsWhiteSpace(safeNormalized[firstWhitespaceAfterCommand]))
                return false;

            int valueStart = firstWhitespaceAfterCommand + 1;
            while (valueStart < safeNormalized.Length && char.IsWhiteSpace(safeNormalized[valueStart]))
                valueStart++;

            string prefixBeforeValue = safeNormalized.Substring(0, valueStart);
            string currentValue = valueStart < safeNormalized.Length ? safeNormalized.Substring(valueStart) : string.Empty;

            CompletionBuild valueCompletion;
            if (!TryBuildOpenObjectFieldContinuation(currentValue, parameter.Type, out valueCompletion))
                return false;

            if (string.IsNullOrEmpty(valueCompletion.Text) || string.Equals(valueCompletion.Text, currentValue, StringComparison.Ordinal))
                return false;

            string completionNormalized = prefixBeforeValue + valueCompletion.Text;
            string fullCompletion = leading + CommandPrefix + completionNormalized;
            HintContext hintContext = ResolveHintContext(valueCompletion.Text, parameter.Type, parameter.Name);
            string label = BuildValueLabel(hintContext.Type, hintContext.Name, parameter.IsTarget);
            string hint = BuildValueWritingHint(hintContext.Type, hintContext.Name);

            suggestion = new NeoCommandSuggestion
            {
                Completion = fullCompletion,
                DisplayText = label,
                Hint = hint,
                HasSuggestion = true,
                CursorIndex = leading.Length + 1 + prefixBeforeValue.Length + valueCompletion.CursorIndex,
                Command = command,
                IsTargetSuggestion = parameter.IsTarget,
                IsInformationalOnly = true,
                Description = hint
            };

            return true;
        }

        private static bool TryBuildOpenObjectFieldContinuation(string value, Type objectType, out CompletionBuild completion)
        {
            completion = default(CompletionBuild);

            if (!NeoCommandRegistry.IsCustomJsonParameterType(objectType))
                return false;

            string safeValue = value ?? string.Empty;
            FieldInfo[] fields = GetSerializableFields(objectType);
            if (fields.Length == 0)
                return false;

            int openIndex = safeValue.IndexOf('{');
            if (openIndex < 0)
                return false;

            // If this object is already fully closed, it is not an open-object continuation.
            // The caller can decide what to do with the completed argument.
            if (IsBalancedAndClosed(safeValue, '{', '}'))
                return false;

            string content = safeValue.Substring(openIndex + 1);
            if (ContainsTopLevelMissingCommaPattern(content))
                return false;

            if (string.IsNullOrWhiteSpace(content) || EndsWithTopLevelSeparator(content, ','))
            {
                FieldInfo nextField = FindNextUnusedField(fields, content);
                if (nextField == null)
                {
                    completion = AppendToOpenValue(safeValue, " }");
                    return true;
                }

                CompletionBuild fieldStart = BuildFieldStart(nextField);
                string baseText = safeValue.TrimEnd();
                string text = baseText + " " + fieldStart.Text;
                completion = new CompletionBuild(text, baseText.Length + 1 + fieldStart.CursorIndex);
                return true;
            }

            List<TextSegment> segments = SplitTopLevelSegments(content, ',');
            if (segments.Count == 0)
                return false;

            TextSegment last = segments[segments.Count - 1];
            string lastText = last.Text ?? string.Empty;
            int separatorIndex = FindTopLevelNameValueSeparator(lastText);
            if (separatorIndex < 0)
                return false;

            string fieldName = Unquote(lastText.Substring(0, separatorIndex).Trim());
            FieldInfo currentField = FindSerializableField(fields, fieldName);
            if (currentField == null)
                return false;

            int valueStartInLast = separatorIndex + 1;
            while (valueStartInLast < lastText.Length && char.IsWhiteSpace(lastText[valueStartInLast]))
                valueStartInLast++;

            string currentRawValue = lastText.Substring(valueStartInLast);
            int rawValueStartInContent = last.Start + valueStartInLast;
            int rawValueStartInObject = openIndex + 1 + rawValueStartInContent;

            // If the current field is not complete, continue inside that field first. This is what
            // makes MainStat: { ... IsPercent: false| suggest the closing brace for MainStat before
            // returning to ItemData.
            if (!IsValueComplete(currentRawValue, currentField.FieldType))
            {
                CompletionBuild nested = BuildValueCompletion(currentRawValue, currentField.FieldType, currentField.Name);
                if (string.IsNullOrEmpty(nested.Text) || string.Equals(nested.Text, currentRawValue, StringComparison.Ordinal))
                    return false;

                string text = safeValue.Substring(0, rawValueStartInObject) + nested.Text;
                completion = new CompletionBuild(text, rawValueStartInObject + nested.CursorIndex);
                return true;
            }

            // The current field is complete. If there are remaining fields in the parent object,
            // suggest the comma and the next field even when the user has not typed the comma yet.
            FieldInfo next = FindNextUnusedFieldAfter(fields, content, currentField.Name);
            if (next == null)
            {
                completion = AppendToOpenValue(safeValue, " }");
                return true;
            }

            CompletionBuild nextFieldStart = BuildFieldStart(next);
            string completedBase = safeValue.TrimEnd();
            string nextText = completedBase + ", " + nextFieldStart.Text;
            completion = new CompletionBuild(nextText, completedBase.Length + 2 + nextFieldStart.CursorIndex);
            return true;
        }

        private static bool TryGetSingleParameterFallbackSuggestion(string input, NeoCommandInfo command, string leading, string normalized, out NeoCommandSuggestion suggestion)
        {
            suggestion = null;

            if (command == null)
                return false;

            CompletionParameter[] parameters = GetCompletionParameters(command);
            if (parameters.Length != 1)
                return false;

            string safeNormalized = normalized ?? string.Empty;
            string commandName = GetCommandNameFromNormalizedInput(safeNormalized);
            if (string.IsNullOrEmpty(commandName) || commandName.Length >= safeNormalized.Length)
                return false;

            int firstWhitespaceAfterCommand = commandName.Length;
            if (!char.IsWhiteSpace(safeNormalized[firstWhitespaceAfterCommand]))
                return false;

            int valueStart = firstWhitespaceAfterCommand + 1;
            while (valueStart < safeNormalized.Length && char.IsWhiteSpace(safeNormalized[valueStart]))
                valueStart++;

            string prefixBeforeValue = safeNormalized.Substring(0, valueStart);
            string currentValue = valueStart < safeNormalized.Length ? safeNormalized.Substring(valueStart) : string.Empty;

            CompletionParameter parameter = parameters[0];
            CompletionBuild valueCompletion = BuildValueCompletion(currentValue, parameter.Type, parameter.Name);
            if (string.IsNullOrEmpty(valueCompletion.Text) || string.Equals(valueCompletion.Text, currentValue, StringComparison.Ordinal))
                return false;

            string completionNormalized = prefixBeforeValue + valueCompletion.Text;
            string fullCompletion = leading + CommandPrefix + completionNormalized;
            HintContext hintContext = ResolveHintContext(currentValue, parameter.Type, parameter.Name);
            string label = BuildValueLabel(hintContext.Type, hintContext.Name, parameter.IsTarget);
            string hint = BuildValueWritingHint(hintContext.Type, hintContext.Name);

            suggestion = new NeoCommandSuggestion
            {
                Completion = fullCompletion,
                DisplayText = label,
                Hint = hint,
                HasSuggestion = true,
                CursorIndex = leading.Length + 1 + prefixBeforeValue.Length + valueCompletion.CursorIndex,
                Command = command,
                IsTargetSuggestion = parameter.IsTarget,
                IsInformationalOnly = true,
                Description = hint
            };

            return true;
        }

        private static bool TryGetArgumentContextSuggestion(string input, NeoCommandInfo[] commands, out NeoCommandSuggestion suggestion)
        {
            suggestion = null;

            string safeInput = input ?? string.Empty;
            NeoCommandInfo command;
            string leading;
            string normalized;
            if (!TryResolveCommand(safeInput, commands, out command, out leading, out normalized))
                return false;

            ParameterContext parameterContext;
            if (!TryResolveParameterContext(normalized, command, out parameterContext))
                return false;

            CompletionBuild completion = BuildArgumentCompletion(normalized, command);
            HintContext hintContext = ResolveHintContext(parameterContext.CurrentValue, parameterContext.Parameter.Type, parameterContext.Parameter.Name);
            string label = BuildValueLabel(hintContext.Type, hintContext.Name, parameterContext.Parameter.IsTarget);
            string hint = parameterContext.Parameter.IsTarget
                ? "Write an active GameObject name or instance identifier. Example: \"Player\"."
                : BuildValueWritingHint(hintContext.Type, hintContext.Name);

            string fullCompletion = leading + CommandPrefix + completion.Text;
            suggestion = new NeoCommandSuggestion
            {
                Completion = fullCompletion,
                DisplayText = label,
                Hint = hint,
                HasSuggestion = true,
                CursorIndex = leading.Length + 1 + completion.CursorIndex,
                Command = command,
                IsTargetSuggestion = parameterContext.Parameter.IsTarget,
                IsInformationalOnly = true,
                Description = hint
            };

            return true;
        }

        private static bool TryGetBuiltInValueSuggestions(string fullInput, NeoCommandInfo command, string leading, string normalized, NeoCommandExecutionContext context, out NeoCommandSuggestion[] suggestions)
        {
            suggestions = EmptySuggestions;

            if (command == null)
                return false;

            if (!string.Equals(command.Name, "neo.scene.load", StringComparison.OrdinalIgnoreCase))
                return false;

            ParameterContext parameterContext;
            if (!TryResolveParameterContext(normalized, command, out parameterContext))
                return false;

            if (parameterContext.ParameterIndex != 0 || parameterContext.Parameter.Type != typeof(string))
                return false;

            NeoConsoleBuildSceneInfo[] scenes = NeoConsoleSceneUtility.GetBuildScenes();
            if (scenes == null || scenes.Length == 0)
                return false;

            string filter = NormalizeTypedValueFilter(parameterContext.CurrentValue);
            bool requireEnabled = SceneSuggestionsRequireEnabled(context);
            List<NeoCommandSuggestion> result = new List<NeoCommandSuggestion>();
            AddSceneSuggestions(result, command, leading, parameterContext, scenes, filter, true, requireEnabled);
            AddSceneSuggestions(result, command, leading, parameterContext, scenes, filter, false, requireEnabled);

            suggestions = result.Count > 0 ? result.ToArray() : EmptySuggestions;
            return suggestions.Length > 0;
        }

        private static bool SceneSuggestionsRequireEnabled(NeoCommandExecutionContext context)
        {
            if (context == NeoCommandExecutionContext.Runtime)
                return true;

#if UNITY_EDITOR
            return Application.isPlaying;
#else
            return true;
#endif
        }

        private static void AddSceneSuggestions(List<NeoCommandSuggestion> result, NeoCommandInfo command, string leading, ParameterContext parameterContext, NeoConsoleBuildSceneInfo[] scenes, string filter, bool startsWithOnly, bool requireEnabled)
        {
            if (result == null || scenes == null)
                return;

            for (int i = 0; i < scenes.Length; i++)
            {
                NeoConsoleBuildSceneInfo scene = scenes[i];
                if (string.IsNullOrEmpty(scene.Name))
                    continue;

                if (requireEnabled && !scene.Enabled)
                    continue;

                if (!SceneSuggestionMatches(scene, filter, startsWithOnly))
                    continue;

                if (ContainsSceneSuggestion(result, scene.Name))
                    continue;

                string value = NeoConsoleSceneUtility.QuoteSceneNameForCommand(scene.Name);
                string completion = leading + CommandPrefix + parameterContext.PrefixBeforeValueInNormalized + value;
                result.Add(new NeoCommandSuggestion
                {
                    Completion = completion,
                    DisplayText = scene.Name,
                    Hint = "Build Settings scene",
                    HasSuggestion = true,
                    CursorIndex = completion.Length,
                    Command = command,
                    IsTargetSuggestion = false,
                    IsInformationalOnly = false,
                    Description = BuildSceneSuggestionDescription(scene)
                });
            }
        }

        private static bool SceneSuggestionMatches(NeoConsoleBuildSceneInfo scene, string filter, bool startsWithOnly)
        {
            if (string.IsNullOrEmpty(filter))
                return startsWithOnly;

            if (scene.Name != null)
            {
                if (startsWithOnly && scene.Name.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!startsWithOnly && scene.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            if (scene.Path != null)
            {
                if (startsWithOnly && scene.Path.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!startsWithOnly && scene.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool ContainsSceneSuggestion(List<NeoCommandSuggestion> result, string sceneName)
        {
            for (int i = 0; i < result.Count; i++)
            {
                NeoCommandSuggestion suggestion = result[i];
                if (suggestion != null && string.Equals(suggestion.DisplayText, sceneName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string BuildSceneSuggestionDescription(NeoConsoleBuildSceneInfo scene)
        {
            string index = scene.BuildIndex >= 0 ? "#" + scene.BuildIndex.ToString(CultureInfo.InvariantCulture) : "disabled";
            if (string.IsNullOrEmpty(scene.Path))
                return index;

            return index + " | " + scene.Path;
        }

        private static NeoCommandSuggestion[] BuildSelectableValueSuggestions(SelectableValueContext context)
        {
            if (context.Type == typeof(bool))
            {
                return BuildBooleanSuggestions(context);
            }

            if (context.Type != null && context.Type.IsEnum)
            {
                string[] names = Enum.GetNames(context.Type);
                List<NeoCommandSuggestion> result = new List<NeoCommandSuggestion>();
                string filter = NormalizeTypedValueFilter(context.TypedValue);
                for (int i = 0; i < names.Length; i++)
                {
                    string enumName = names[i];
                    if (!string.IsNullOrEmpty(filter) && !enumName.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    result.Add(BuildSelectableSuggestion(context, enumName, context.Type.Name));
                }

                if (result.Count == 0 && !string.IsNullOrEmpty(filter))
                {
                    for (int i = 0; i < names.Length; i++)
                    {
                        string enumName = names[i];
                        if (enumName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        result.Add(BuildSelectableSuggestion(context, enumName, context.Type.Name));
                    }
                }

                return result.ToArray();
            }

            return EmptySuggestions;
        }

        private static NeoCommandSuggestion[] BuildBooleanSuggestions(SelectableValueContext context)
        {
            string filter = NormalizeTypedValueFilter(context.TypedValue);
            List<NeoCommandSuggestion> result = new List<NeoCommandSuggestion>();
            if (string.IsNullOrEmpty(filter) || "true".StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                result.Add(BuildSelectableSuggestion(context, "true", "bool"));
            if (string.IsNullOrEmpty(filter) || "false".StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                result.Add(BuildSelectableSuggestion(context, "false", "bool"));
            return result.ToArray();
        }

        private static NeoCommandSuggestion BuildSelectableSuggestion(SelectableValueContext context, string value, string description)
        {
            string completion = context.PrefixBeforeValue + value + context.SuffixAfterValue;
            return new NeoCommandSuggestion
            {
                Completion = completion,
                DisplayText = value,
                Hint = "Value for " + context.DisplayName,
                HasSuggestion = true,
                CursorIndex = completion.Length,
                Command = context.Command,
                IsTargetSuggestion = false,
                IsInformationalOnly = false,
                Description = description
            };
        }

        private static bool TryResolveSelectableValueContext(string input, NeoCommandInfo command, string leading, string normalized, out SelectableValueContext context)
        {
            context = default(SelectableValueContext);

            ParameterContext parameterContext;
            if (!TryResolveParameterContext(normalized, command, out parameterContext))
                return false;

            if (parameterContext.Parameter.IsTarget)
                return false;

            int valueStartInFullInput = leading.Length + 1 + parameterContext.ValueStartInNormalized;
            string fullInput = input ?? string.Empty;

            if (IsSelectableType(parameterContext.Parameter.Type))
            {
                string typedValue = parameterContext.CurrentValue ?? string.Empty;
                if (SelectableValueIsExact(parameterContext.Parameter.Type, typedValue))
                    return false;

                context = new SelectableValueContext(command, parameterContext.Parameter.Type, fullInput.Substring(0, valueStartInFullInput), typedValue, string.Empty, parameterContext.Parameter.Name);
                return true;
            }

            if (NeoCommandRegistry.IsCustomJsonParameterType(parameterContext.Parameter.Type))
            {
                return TryResolveSelectableInCustomObject(
                    fullInput,
                    command,
                    parameterContext.CurrentValue,
                    parameterContext.Parameter.Type,
                    valueStartInFullInput,
                    out context);
            }

            return false;
        }

        private static bool TryResolveSelectableInCustomObject(string fullInput, NeoCommandInfo command, string value, Type objectType, int valueStartInFullInput, out SelectableValueContext context)
        {
            context = default(SelectableValueContext);

            string safeValue = value ?? string.Empty;
            int openIndex = safeValue.IndexOf('{');
            if (openIndex < 0)
                return false;

            if (IsBalancedAndClosed(safeValue, '{', '}'))
                return false;

            string content = safeValue.Substring(openIndex + 1);
            if (ContainsTopLevelMissingCommaPattern(content))
                return false;

            List<TextSegment> segments = SplitTopLevelSegments(content, ',');
            if (segments.Count == 0 || EndsWithTopLevelSeparator(content, ','))
                return false;

            TextSegment last = segments[segments.Count - 1];
            string segmentText = last.Text;
            if (string.IsNullOrWhiteSpace(segmentText))
                return false;

            int separator = FindTopLevelNameValueSeparator(segmentText);
            if (separator < 0)
                return false;

            string fieldName = Unquote(segmentText.Substring(0, separator).Trim());
            FieldInfo field = FindSerializableField(GetSerializableFields(objectType), fieldName);
            if (field == null)
                return false;

            int rawValueStartInSegment = separator + 1;
            while (rawValueStartInSegment < segmentText.Length && char.IsWhiteSpace(segmentText[rawValueStartInSegment]))
                rawValueStartInSegment++;

            string rawValue = segmentText.Substring(rawValueStartInSegment);
            int rawValueStartInObjectValue = openIndex + 1 + last.Start + rawValueStartInSegment;
            Type fieldType = field.FieldType;

            if (IsSelectableType(fieldType))
            {
                if (SelectableValueIsExact(fieldType, rawValue))
                    return false;

                bool hasNextField = HasUnusedFieldAfter(objectType, field.Name, safeValue);
                context = new SelectableValueContext(
                    command,
                    fieldType,
                    fullInput.Substring(0, valueStartInFullInput + rawValueStartInObjectValue),
                    rawValue,
                    hasNextField ? ", " : string.Empty,
                    field.Name);
                return true;
            }

            if (NeoCommandRegistry.IsCustomJsonParameterType(fieldType))
            {
                if (IsValueComplete(rawValue, fieldType))
                    return false;

                return TryResolveSelectableInCustomObject(fullInput, command, rawValue, fieldType, valueStartInFullInput + rawValueStartInObjectValue, out context);
            }

            if (IsCollectionLike(fieldType))
            {
                if (IsValueComplete(rawValue, fieldType))
                    return false;

                Type elementType = GetListElementType(fieldType);
                return TryResolveSelectableInCollection(fullInput, command, rawValue, elementType, valueStartInFullInput + rawValueStartInObjectValue, out context);
            }

            return false;
        }

        private static bool TryResolveSelectableInCollection(string fullInput, NeoCommandInfo command, string value, Type elementType, int valueStartInFullInput, out SelectableValueContext context)
        {
            context = default(SelectableValueContext);
            string safeValue = value ?? string.Empty;
            int openIndex = safeValue.IndexOf('[');
            if (openIndex < 0)
                return false;

            if (IsBalancedAndClosed(safeValue, '[', ']'))
                return false;

            string content = safeValue.Substring(openIndex + 1);
            if (ContainsTopLevelMissingCommaPattern(content))
                return false;

            List<TextSegment> segments = SplitTopLevelSegments(content, ',');
            if (segments.Count == 0 || EndsWithTopLevelSeparator(content, ','))
                return false;

            TextSegment last = segments[segments.Count - 1];
            string itemValue = last.Text;
            int itemStartInCollection = openIndex + 1 + last.Start;

            if (IsSelectableType(elementType))
            {
                if (SelectableValueIsExact(elementType, itemValue))
                    return false;

                context = new SelectableValueContext(command, elementType, fullInput.Substring(0, valueStartInFullInput + itemStartInCollection), itemValue, string.Empty, "item");
                return true;
            }

            if (NeoCommandRegistry.IsCustomJsonParameterType(elementType))
            {
                if (IsValueComplete(itemValue, elementType))
                    return false;

                return TryResolveSelectableInCustomObject(fullInput, command, itemValue, elementType, valueStartInFullInput + itemStartInCollection, out context);
            }

            return false;
        }

        private static CompletionBuild BuildCommandCompletion(NeoCommandInfo command)
        {
            if (command == null)
                return new CompletionBuild(string.Empty, 0);

            CompletionParameter[] completionParameters = GetCompletionParameters(command);
            if (completionParameters.Length == 0)
                return new CompletionBuild(command.Name, command.Name.Length);

            string text = command.Name + " ";
            return new CompletionBuild(text, text.Length);
        }

        private static CompletionBuild BuildArgumentCompletion(string normalizedInput, NeoCommandInfo command)
        {
            string safeInput = normalizedInput ?? string.Empty;
            if (command == null)
                return new CompletionBuild(safeInput, safeInput.Length);

            ParameterContext parameterContext;
            if (!TryResolveParameterContext(safeInput, command, out parameterContext))
                return new CompletionBuild(safeInput, safeInput.Length);

            CompletionBuild valueCompletion = BuildValueCompletion(parameterContext.CurrentValue, parameterContext.Parameter.Type, parameterContext.Parameter.Name);
            if (string.IsNullOrEmpty(valueCompletion.Text) || string.Equals(valueCompletion.Text, parameterContext.CurrentValue, StringComparison.Ordinal))
                return new CompletionBuild(safeInput, safeInput.Length);

            string text = parameterContext.PrefixBeforeValueInNormalized + valueCompletion.Text;
            return new CompletionBuild(text, parameterContext.PrefixBeforeValueInNormalized.Length + valueCompletion.CursorIndex);
        }

        private static CompletionBuild BuildValueCompletion(string value, Type type, string name)
        {
            string safeValue = value ?? string.Empty;
            string trimmed = safeValue.Trim();

            if (NeoCommandRegistry.IsCustomJsonParameterType(type))
                return BuildCustomObjectCompletion(safeValue, type);

            if (IsDictionaryLike(type))
                return BuildDictionaryCompletion(safeValue, type);

            if (IsCollectionLike(type))
                return BuildCollectionCompletion(safeValue, GetListElementType(type));

            if (type == typeof(string) || type == typeof(char))
            {
                if (string.IsNullOrWhiteSpace(safeValue))
                    return new CompletionBuild("\"\"", 1);

                if (HasUnclosedQuote(safeValue, '"'))
                    return new CompletionBuild(safeValue + "\"", safeValue.Length + 1);

                return new CompletionBuild(safeValue, safeValue.Length);
            }

            if (type == typeof(float) || type == typeof(double))
            {
                if (string.IsNullOrWhiteSpace(safeValue))
                    return new CompletionBuild("1.0", 3);
                return new CompletionBuild(safeValue, safeValue.Length);
            }

            if (type == typeof(int) || type == typeof(long))
            {
                if (string.IsNullOrWhiteSpace(safeValue))
                    return new CompletionBuild("1", 1);
                return new CompletionBuild(safeValue, safeValue.Length);
            }

            if (type == typeof(bool) || (type != null && type.IsEnum))
            {
                return new CompletionBuild(safeValue, safeValue.Length);
            }

            return new CompletionBuild(safeValue, safeValue.Length);
        }

        private static CompletionBuild BuildCustomObjectCompletion(string value, Type objectType)
        {
            string safeValue = value ?? string.Empty;
            FieldInfo[] fields = GetSerializableFields(objectType);
            if (fields.Length == 0)
                return new CompletionBuild(safeValue, safeValue.Length);

            int openIndex = safeValue.IndexOf('{');
            if (openIndex < 0)
            {
                CompletionBuild first = BuildFieldStart(fields[0]);
                string text = "{ " + first.Text;
                return new CompletionBuild(text, 2 + first.CursorIndex);
            }

            CompletionBuild contextualContinuation;
            if (TryBuildOpenObjectFieldContinuation(safeValue, objectType, out contextualContinuation))
                return contextualContinuation;

            if (IsBalancedAndClosed(safeValue, '{', '}'))
                return new CompletionBuild(safeValue, safeValue.Length);

            string content = safeValue.Substring(openIndex + 1);
            if (ContainsTopLevelMissingCommaPattern(content))
                return new CompletionBuild(safeValue, safeValue.Length);

            List<TextSegment> segments = SplitTopLevelSegments(content, ',');
            bool emptyContent = string.IsNullOrWhiteSpace(content);
            bool endsWithComma = EndsWithTopLevelSeparator(content, ',');

            if (emptyContent || endsWithComma)
            {
                FieldInfo nextField = FindNextUnusedField(fields, content);
                if (nextField == null)
                    return AppendToOpenValue(safeValue, " }");

                CompletionBuild fieldStart = BuildFieldStart(nextField);
                string separator = emptyContent ? " " : " ";
                string text = safeValue.TrimEnd() + separator + fieldStart.Text;
                return new CompletionBuild(text, safeValue.TrimEnd().Length + separator.Length + fieldStart.CursorIndex);
            }

            if (segments.Count == 0)
                return new CompletionBuild(safeValue, safeValue.Length);

            TextSegment last = segments[segments.Count - 1];
            string lastText = last.Text;
            int separatorIndex = FindTopLevelNameValueSeparator(lastText);
            if (separatorIndex < 0)
                return new CompletionBuild(safeValue, safeValue.Length);

            string fieldName = Unquote(lastText.Substring(0, separatorIndex).Trim());
            FieldInfo currentField = FindSerializableField(fields, fieldName);
            if (currentField == null)
                return new CompletionBuild(safeValue, safeValue.Length);

            int valueStartInLast = separatorIndex + 1;
            while (valueStartInLast < lastText.Length && char.IsWhiteSpace(lastText[valueStartInLast]))
                valueStartInLast++;

            string currentRawValue = lastText.Substring(valueStartInLast);
            int rawValueStartInContent = last.Start + valueStartInLast;
            int rawValueStartInObject = openIndex + 1 + rawValueStartInContent;

            if (!IsValueComplete(currentRawValue, currentField.FieldType))
            {
                CompletionBuild nested = BuildValueCompletion(currentRawValue, currentField.FieldType, currentField.Name);
                if (!string.Equals(nested.Text, currentRawValue, StringComparison.Ordinal))
                {
                    string text = safeValue.Substring(0, rawValueStartInObject) + nested.Text;
                    return new CompletionBuild(text, rawValueStartInObject + nested.CursorIndex);
                }

                return new CompletionBuild(safeValue, safeValue.Length);
            }

            FieldInfo next = FindNextUnusedFieldAfter(fields, content, currentField.Name);
            if (next == null)
                return AppendToOpenValue(safeValue, " }");

            CompletionBuild nextFieldStart = BuildFieldStart(next);
            string baseText = safeValue.TrimEnd();
            string nextText = baseText + ", " + nextFieldStart.Text;
            return new CompletionBuild(nextText, baseText.Length + 2 + nextFieldStart.CursorIndex);
        }

        private static CompletionBuild BuildCollectionCompletion(string value, Type elementType)
        {
            string safeValue = value ?? string.Empty;
            int openIndex = safeValue.IndexOf('[');
            if (openIndex < 0)
                return new CompletionBuild("[", 1);

            if (IsBalancedAndClosed(safeValue, '[', ']'))
                return new CompletionBuild(safeValue, safeValue.Length);

            string content = safeValue.Substring(openIndex + 1);
            bool emptyContent = string.IsNullOrWhiteSpace(content);
            bool endsWithComma = EndsWithTopLevelSeparator(content, ',');
            if (emptyContent || endsWithComma)
            {
                CompletionBuild elementStart = BuildElementStart(elementType);
                string separator = emptyContent ? string.Empty : " ";
                string text = safeValue.TrimEnd() + separator + elementStart.Text;
                return new CompletionBuild(text, safeValue.TrimEnd().Length + separator.Length + elementStart.CursorIndex);
            }

            List<TextSegment> segments = SplitTopLevelSegments(content, ',');
            if (segments.Count == 0)
                return new CompletionBuild(safeValue, safeValue.Length);

            TextSegment last = segments[segments.Count - 1];
            string item = last.Text;
            if (!IsValueComplete(item, elementType))
            {
                CompletionBuild itemStep = BuildValueCompletion(item, elementType, "item");
                if (!string.Equals(itemStep.Text, item, StringComparison.Ordinal))
                {
                    int itemStart = openIndex + 1 + last.Start;
                    string text = safeValue.Substring(0, itemStart) + itemStep.Text;
                    return new CompletionBuild(text, itemStart + itemStep.CursorIndex);
                }

                return new CompletionBuild(safeValue, safeValue.Length);
            }

            return AppendToOpenValue(safeValue, "]");
        }

        private static CompletionBuild BuildDictionaryCompletion(string value, Type dictionaryType)
        {
            string safeValue = value ?? string.Empty;
            Type keyType;
            Type valueType;
            TryGetDictionaryTypes(dictionaryType, out keyType, out valueType);

            int openIndex = safeValue.IndexOf('{');
            if (openIndex < 0)
                return new CompletionBuild("{", 1);

            if (IsBalancedAndClosed(safeValue, '{', '}'))
                return new CompletionBuild(safeValue, safeValue.Length);

            string content = safeValue.Substring(openIndex + 1);
            if (ContainsTopLevelMissingCommaPattern(content))
                return new CompletionBuild(safeValue, safeValue.Length);

            if (string.IsNullOrWhiteSpace(content) || EndsWithTopLevelSeparator(content, ','))
            {
                string pair = GetDictionaryKeyPlaceholder(keyType) + ": " + GetJsonPlaceholder(valueType);
                int cursor = GetDictionaryPairCursor(pair, keyType);
                string separator = string.IsNullOrWhiteSpace(content) ? " " : " ";
                string text = safeValue.TrimEnd() + separator + pair;
                return new CompletionBuild(text, safeValue.TrimEnd().Length + separator.Length + cursor);
            }

            List<TextSegment> segments = SplitTopLevelSegments(content, ',');
            if (segments.Count == 0)
                return new CompletionBuild(safeValue, safeValue.Length);

            TextSegment last = segments[segments.Count - 1];
            string lastText = last.Text ?? string.Empty;
            int separatorIndex = FindTopLevelNameValueSeparator(lastText);
            if (separatorIndex < 0)
                return new CompletionBuild(safeValue, safeValue.Length);

            int dictionaryValueStart = separatorIndex + 1;
            while (dictionaryValueStart < lastText.Length && char.IsWhiteSpace(lastText[dictionaryValueStart]))
                dictionaryValueStart++;

            string rawDictionaryValue = lastText.Substring(dictionaryValueStart);
            if (!IsValueComplete(rawDictionaryValue, valueType))
            {
                CompletionBuild valueStep = BuildValueCompletion(rawDictionaryValue, valueType, "value");
                if (!string.Equals(valueStep.Text, rawDictionaryValue, StringComparison.Ordinal))
                {
                    int valueStart = openIndex + 1 + last.Start + dictionaryValueStart;
                    string text = safeValue.Substring(0, valueStart) + valueStep.Text;
                    return new CompletionBuild(text, valueStart + valueStep.CursorIndex);
                }

                return new CompletionBuild(safeValue, safeValue.Length);
            }

            return AppendToOpenValue(safeValue, " }");
        }

        private static CompletionBuild BuildFieldStart(FieldInfo field)
        {
            if (field == null)
                return new CompletionBuild(string.Empty, 0);

            Type fieldType = field.FieldType;
            string prefix = field.Name + ": ";

            if (IsSelectableType(fieldType))
                return new CompletionBuild(prefix, prefix.Length);

            CompletionBuild value = BuildFieldInitialValue(fieldType);
            return new CompletionBuild(prefix + value.Text, prefix.Length + value.CursorIndex);
        }

        private static CompletionBuild BuildFieldInitialValue(Type type)
        {
            if (type == typeof(string) || type == typeof(char))
                return new CompletionBuild("\"\"", 1);

            if (type == typeof(float) || type == typeof(double))
                return new CompletionBuild("1.0", 3);

            if (type == typeof(int) || type == typeof(long))
                return new CompletionBuild("1", 1);

            if (NeoCommandRegistry.IsCustomJsonParameterType(type))
                return new CompletionBuild("{", 1);

            if (IsCollectionLike(type))
                return new CompletionBuild("[", 1);

            if (IsDictionaryLike(type))
                return new CompletionBuild("{", 1);

            if (type == typeof(bool) || (type != null && type.IsEnum))
                return new CompletionBuild(string.Empty, 0);

            return new CompletionBuild("null", 4);
        }

        private static CompletionBuild BuildElementStart(Type elementType)
        {
            if (NeoCommandRegistry.IsCustomJsonParameterType(elementType))
                return new CompletionBuild("{", 1);

            return BuildFieldInitialValue(elementType);
        }

        private static CompletionBuild AppendToOpenValue(string value, string suffix)
        {
            string baseText = (value ?? string.Empty).TrimEnd();
            string text = baseText + suffix;
            return new CompletionBuild(text, text.Length);
        }

        private static bool TryResolveCommand(string input, NeoCommandInfo[] commands, out NeoCommandInfo command, out string leading, out string normalized)
        {
            command = null;
            leading = string.Empty;
            normalized = string.Empty;

            string safeInput = input ?? string.Empty;
            string trimmedStart = safeInput.TrimStart();
            int leadingSpaces = safeInput.Length - trimmedStart.Length;
            leading = leadingSpaces > 0 ? safeInput.Substring(0, leadingSpaces) : string.Empty;

            if (!trimmedStart.StartsWith(CommandPrefix.ToString(), StringComparison.Ordinal))
                return false;

            normalized = trimmedStart.Substring(1);
            string commandName = GetCommandNameFromNormalizedInput(normalized);
            if (string.IsNullOrEmpty(commandName))
                return false;

            if (commands == null)
                return false;

            for (int i = 0; i < commands.Length; i++)
            {
                NeoCommandInfo current = commands[i];
                if (current != null && string.Equals(current.Name, commandName, StringComparison.OrdinalIgnoreCase))
                {
                    command = current;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveParameterContext(string normalizedInput, NeoCommandInfo command, out ParameterContext context)
        {
            context = default(ParameterContext);
            if (command == null)
                return false;

            CompletionParameter[] parameters = GetCompletionParameters(command);
            if (parameters.Length == 0)
                return false;

            string safeInput = normalizedInput ?? string.Empty;
            string commandName = GetCommandNameFromNormalizedInput(safeInput);
            if (string.IsNullOrEmpty(commandName))
                return false;

            int commandEnd = commandName.Length;
            string rest = commandEnd < safeInput.Length ? safeInput.Substring(commandEnd) : string.Empty;
            bool hasWhitespaceAfterCommand = rest.Length > 0 && char.IsWhiteSpace(rest[0]);
            if (!hasWhitespaceAfterCommand)
                return false;

            List<ArgumentToken> arguments = SplitArgumentsWithBounds(rest);
            bool endsWithTopLevelWhitespace = EndsWithTopLevelWhitespace(rest);
            int parameterIndex = endsWithTopLevelWhitespace ? arguments.Count : Mathf.Max(0, arguments.Count - 1);

            if (parameterIndex < 0 || parameterIndex >= parameters.Length)
                return false;

            CompletionParameter parameter = parameters[parameterIndex];
            bool hasCurrentValue = !endsWithTopLevelWhitespace && parameterIndex < arguments.Count;
            string currentValue = hasCurrentValue ? arguments[parameterIndex].Value : string.Empty;
            int valueStartInNormalized;
            string prefixBeforeValue;

            if (hasCurrentValue)
            {
                valueStartInNormalized = commandEnd + arguments[parameterIndex].Start;
                prefixBeforeValue = safeInput.Substring(0, valueStartInNormalized);
            }
            else
            {
                valueStartInNormalized = safeInput.Length;
                prefixBeforeValue = safeInput;
                if (prefixBeforeValue.Length == 0 || !char.IsWhiteSpace(prefixBeforeValue[prefixBeforeValue.Length - 1]))
                {
                    prefixBeforeValue += " ";
                    valueStartInNormalized++;
                }
            }

            context = new ParameterContext(parameter, parameterIndex, currentValue, prefixBeforeValue, valueStartInNormalized);
            return true;
        }

        private static List<ArgumentToken> SplitArgumentsWithBounds(string input)
        {
            List<ArgumentToken> result = new List<ArgumentToken>();
            string safe = input ?? string.Empty;
            int index = 0;

            while (index < safe.Length)
            {
                while (index < safe.Length && char.IsWhiteSpace(safe[index]))
                    index++;

                if (index >= safe.Length)
                    break;

                int start = index;
                bool insideQuotes = false;
                char quote = '\0';
                bool escaped = false;
                int objectDepth = 0;
                int arrayDepth = 0;

                while (index < safe.Length)
                {
                    char c = safe[index];

                    if (escaped)
                    {
                        escaped = false;
                        index++;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        index++;
                        continue;
                    }

                    if (c == '"' || c == '\'')
                    {
                        if (!insideQuotes)
                        {
                            insideQuotes = true;
                            quote = c;
                        }
                        else if (quote == c)
                        {
                            insideQuotes = false;
                            quote = '\0';
                        }

                        index++;
                        continue;
                    }

                    if (!insideQuotes)
                    {
                        if (c == '{') objectDepth++;
                        else if (c == '}' && objectDepth > 0) objectDepth--;
                        else if (c == '[') arrayDepth++;
                        else if (c == ']' && arrayDepth > 0) arrayDepth--;
                        else if (char.IsWhiteSpace(c) && objectDepth == 0 && arrayDepth == 0) break;
                    }

                    index++;
                }

                result.Add(new ArgumentToken(safe.Substring(start, index - start), start, index));
            }

            return result;
        }

        private static bool EndsWithTopLevelWhitespace(string input)
        {
            if (string.IsNullOrEmpty(input) || !char.IsWhiteSpace(input[input.Length - 1]))
                return false;

            bool insideQuotes = false;
            char quote = '\0';
            bool escaped = false;
            int objectDepth = 0;
            int arrayDepth = 0;

            for (int i = 0; i < input.Length - 1; i++)
            {
                char c = input[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    if (!insideQuotes)
                    {
                        insideQuotes = true;
                        quote = c;
                    }
                    else if (quote == c)
                    {
                        insideQuotes = false;
                        quote = '\0';
                    }

                    continue;
                }

                if (insideQuotes)
                    continue;

                if (c == '{') objectDepth++;
                else if (c == '}' && objectDepth > 0) objectDepth--;
                else if (c == '[') arrayDepth++;
                else if (c == ']' && arrayDepth > 0) arrayDepth--;
            }

            return objectDepth == 0 && arrayDepth == 0 && !insideQuotes;
        }

        private static List<TextSegment> SplitTopLevelSegments(string input, char separator)
        {
            List<TextSegment> parts = new List<TextSegment>();
            string safe = input ?? string.Empty;
            int start = 0;
            bool insideQuotes = false;
            char quote = '\0';
            bool escaped = false;
            int objectDepth = 0;
            int arrayDepth = 0;

            for (int i = 0; i < safe.Length; i++)
            {
                char c = safe[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    if (!insideQuotes)
                    {
                        insideQuotes = true;
                        quote = c;
                    }
                    else if (quote == c)
                    {
                        insideQuotes = false;
                        quote = '\0';
                    }

                    continue;
                }

                if (insideQuotes)
                    continue;

                if (c == '{') objectDepth++;
                else if (c == '}' && objectDepth > 0) objectDepth--;
                else if (c == '[') arrayDepth++;
                else if (c == ']' && arrayDepth > 0) arrayDepth--;
                else if (c == separator && objectDepth == 0 && arrayDepth == 0)
                {
                    parts.Add(new TextSegment(safe.Substring(start, i - start), start, i));
                    start = i + 1;
                }
            }

            if (start <= safe.Length)
                parts.Add(new TextSegment(safe.Substring(start), start, safe.Length));

            return parts;
        }

        private static bool EndsWithTopLevelSeparator(string input, char separator)
        {
            string safe = input ?? string.Empty;
            int index = safe.Length - 1;
            while (index >= 0 && char.IsWhiteSpace(safe[index]))
                index--;

            if (index < 0 || safe[index] != separator)
                return false;

            bool insideQuotes = false;
            char quote = '\0';
            bool escaped = false;
            int objectDepth = 0;
            int arrayDepth = 0;

            for (int i = 0; i < index; i++)
            {
                char c = safe[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    if (!insideQuotes)
                    {
                        insideQuotes = true;
                        quote = c;
                    }
                    else if (quote == c)
                    {
                        insideQuotes = false;
                        quote = '\0';
                    }

                    continue;
                }

                if (insideQuotes)
                    continue;

                if (c == '{') objectDepth++;
                else if (c == '}' && objectDepth > 0) objectDepth--;
                else if (c == '[') arrayDepth++;
                else if (c == ']' && arrayDepth > 0) arrayDepth--;
            }

            return objectDepth == 0 && arrayDepth == 0 && !insideQuotes;
        }

        private static bool ContainsTopLevelMissingCommaPattern(string content)
        {
            List<TextSegment> segments = SplitTopLevelSegments(content ?? string.Empty, ',');
            for (int i = 0; i < segments.Count; i++)
            {
                string segment = segments[i].Text ?? string.Empty;
                int firstSeparator = FindTopLevelNameValueSeparator(segment);
                if (firstSeparator < 0)
                    continue;

                string remainder = firstSeparator + 1 < segment.Length
                    ? segment.Substring(firstSeparator + 1)
                    : string.Empty;

                if (FindTopLevelNameValueSeparator(remainder) >= 0)
                    return true;
            }

            return false;
        }

        private static FieldInfo FindNextUnusedField(FieldInfo[] fields, string content)
        {
            HashSet<string> used = GetUsedTopLevelFieldNames(content);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] != null && !used.Contains(fields[i].Name))
                    return fields[i];
            }

            return null;
        }

        private static FieldInfo FindNextUnusedFieldAfter(FieldInfo[] fields, string content, string currentFieldName)
        {
            HashSet<string> used = GetUsedTopLevelFieldNames(content);
            if (!string.IsNullOrEmpty(currentFieldName))
                used.Add(currentFieldName);

            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] != null && !used.Contains(fields[i].Name))
                    return fields[i];
            }

            return null;
        }

        private static bool HasUnusedFieldAfter(Type objectType, string currentFieldName, string currentObjectValue)
        {
            FieldInfo[] fields = GetSerializableFields(objectType);
            if (fields.Length == 0)
                return false;

            string content = GetObjectContent(currentObjectValue);
            return FindNextUnusedFieldAfter(fields, content, currentFieldName) != null;
        }

        private static HashSet<string> GetUsedTopLevelFieldNames(string content)
        {
            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<TextSegment> segments = SplitTopLevelSegments(content ?? string.Empty, ',');
            for (int i = 0; i < segments.Count; i++)
            {
                string token = (segments[i].Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(token))
                    continue;

                int separator = FindTopLevelNameValueSeparator(token);
                if (separator < 0)
                    continue;

                string fieldName = Unquote(token.Substring(0, separator).Trim());
                if (!string.IsNullOrEmpty(fieldName))
                    used.Add(fieldName);
            }

            return used;
        }

        private static string GetObjectContent(string value)
        {
            string safe = value ?? string.Empty;
            int openIndex = safe.IndexOf('{');
            if (openIndex < 0 || openIndex + 1 >= safe.Length)
                return string.Empty;

            string content = safe.Substring(openIndex + 1);
            if (IsBalancedAndClosed(safe, '{', '}'))
            {
                int last = content.LastIndexOf('}');
                if (last >= 0)
                    content = content.Substring(0, last);
            }

            return content;
        }

        private static bool IsValueComplete(string value, Type type)
        {
            string safe = value ?? string.Empty;
            string trimmed = safe.Trim();

            if (string.IsNullOrEmpty(trimmed))
                return false;

            if (type == typeof(string) || type == typeof(char))
                return QuotedStringIsClosed(trimmed);

            if (type == typeof(bool))
                return string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase);

            if (type != null && type.IsEnum)
                return EnumNameEquals(type, trimmed);

            if (type == typeof(int))
            {
                int parsed;
                return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
            }

            if (type == typeof(long))
            {
                long parsed;
                return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
            }

            if (type == typeof(float))
            {
                float parsed;
                return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
            }

            if (type == typeof(double))
            {
                double parsed;
                return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
            }

            if (NeoCommandRegistry.IsCustomJsonParameterType(type))
                return IsBalancedAndClosed(trimmed, '{', '}');

            if (IsCollectionLike(type))
                return IsBalancedAndClosed(trimmed, '[', ']');

            if (IsDictionaryLike(type))
                return IsBalancedAndClosed(trimmed, '{', '}');

            return true;
        }

        private static bool IsBalancedAndClosed(string value, char open, char close)
        {
            string safe = (value ?? string.Empty).Trim();
            if (safe.Length < 2 || safe[0] != open || safe[safe.Length - 1] != close)
                return false;

            bool insideQuotes = false;
            char quote = '\0';
            bool escaped = false;
            int depth = 0;

            for (int i = 0; i < safe.Length; i++)
            {
                char c = safe[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    if (!insideQuotes)
                    {
                        insideQuotes = true;
                        quote = c;
                    }
                    else if (quote == c)
                    {
                        insideQuotes = false;
                        quote = '\0';
                    }

                    continue;
                }

                if (insideQuotes)
                    continue;

                if (c == open) depth++;
                else if (c == close) depth--;

                if (depth < 0)
                    return false;

                if (depth == 0 && i < safe.Length - 1)
                    return false;
            }

            return depth == 0 && !insideQuotes;
        }

        private static bool QuotedStringIsClosed(string value)
        {
            string safe = (value ?? string.Empty).Trim();
            if (safe.Length < 2)
                return false;

            char first = safe[0];
            if (first != '"' && first != '\'')
                return false;

            int closingQuoteIndex = FindClosingQuoteIndex(safe, 0, first);
            if (closingQuoteIndex <= 0)
                return false;

            // A string value is complete only when the closing quote ends the token.
            // This prevents malformed object entries like
            //   ItemId: "" DisplayName: ""
            // from being treated as a valid ItemId value followed by a new field.
            // The autocomplete will stop and avoid drawing a duplicate field ghost.
            for (int i = closingQuoteIndex + 1; i < safe.Length; i++)
            {
                if (!char.IsWhiteSpace(safe[i]))
                    return false;
            }

            return true;
        }

        private static int FindClosingQuoteIndex(string value, int openingQuoteIndex, char quoteCharacter)
        {
            string safe = value ?? string.Empty;
            if (openingQuoteIndex < 0 || openingQuoteIndex >= safe.Length || safe[openingQuoteIndex] != quoteCharacter)
                return -1;

            bool escaped = false;
            for (int i = openingQuoteIndex + 1; i < safe.Length; i++)
            {
                char c = safe[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == quoteCharacter)
                    return i;
            }

            return -1;
        }

        private static bool IsSelectableType(Type type)
        {
            return type == typeof(bool) || (type != null && type.IsEnum);
        }

        private static bool SelectableValueIsExact(Type type, string value)
        {
            string safe = NormalizeTypedValueFilter(value);
            if (type == typeof(bool))
                return string.Equals(safe, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(safe, "false", StringComparison.OrdinalIgnoreCase);

            if (type != null && type.IsEnum)
                return EnumNameEquals(type, safe);

            return false;
        }


        private static bool EnumNameEquals(Type type, string value)
        {
            if (type == null || !type.IsEnum)
                return false;

            string[] names = Enum.GetNames(type);
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string NormalizeTypedValueFilter(string value)
        {
            string safe = (value ?? string.Empty).Trim();
            if (safe.Length >= 2)
            {
                char first = safe[0];
                char last = safe[safe.Length - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                    safe = safe.Substring(1, safe.Length - 2);
                else if (first == '"' || first == '\'')
                    safe = safe.Substring(1);
            }

            return safe.Trim();
        }

        private static int FindTopLevelNameValueSeparator(string token)
        {
            if (string.IsNullOrEmpty(token))
                return -1;

            bool insideQuotes = false;
            char quote = '\0';
            bool escaped = false;
            int objectDepth = 0;
            int arrayDepth = 0;

            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    if (!insideQuotes)
                    {
                        insideQuotes = true;
                        quote = c;
                    }
                    else if (quote == c)
                    {
                        insideQuotes = false;
                        quote = '\0';
                    }

                    continue;
                }

                if (insideQuotes)
                    continue;

                if (c == '{') objectDepth++;
                else if (c == '}' && objectDepth > 0) objectDepth--;
                else if (c == '[') arrayDepth++;
                else if (c == ']' && arrayDepth > 0) arrayDepth--;

                if ((c == ':' || c == ';') && objectDepth == 0 && arrayDepth == 0)
                    return i;
            }

            return -1;
        }

        private static string Unquote(string value)
        {
            string safe = (value ?? string.Empty).Trim();
            if (safe.Length < 2)
                return safe;

            bool quoted = (safe[0] == '"' && safe[safe.Length - 1] == '"') ||
                          (safe[0] == '\'' && safe[safe.Length - 1] == '\'');
            return quoted ? safe.Substring(1, safe.Length - 2) : safe;
        }

        private static string GetCommandNameFromNormalizedInput(string normalizedInput)
        {
            string safe = normalizedInput ?? string.Empty;
            int end = safe.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            return end < 0 ? safe : safe.Substring(0, end);
        }

        public static string GetCommandTokenWithoutPrefix(string input)
        {
            string safeInput = input ?? string.Empty;
            string trimmedStart = safeInput.TrimStart();
            if (!trimmedStart.StartsWith(CommandPrefix.ToString(), StringComparison.Ordinal))
                return string.Empty;

            string withoutPrefix = trimmedStart.Substring(1);
            int spaceIndex = withoutPrefix.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            return spaceIndex < 0 ? withoutPrefix : withoutPrefix.Substring(0, spaceIndex);
        }

        public static bool LooksLikeCommandInput(string input)
        {
            string safeInput = input ?? string.Empty;
            return safeInput.TrimStart().StartsWith(CommandPrefix.ToString(), StringComparison.Ordinal);
        }

        private static bool CommandNameIsFinishedAfterPrefix(string input)
        {
            string safeInput = input ?? string.Empty;
            string trimmedStart = safeInput.TrimStart();
            if (!trimmedStart.StartsWith(CommandPrefix.ToString(), StringComparison.Ordinal))
                return false;

            string withoutPrefix = trimmedStart.Substring(1);
            return CommandNameIsFinished(withoutPrefix);
        }

        private static bool CommandNameIsFinished(string normalizedInput)
        {
            return !string.IsNullOrEmpty(normalizedInput) && normalizedInput.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0;
        }

        private static CompletionParameter[] GetCompletionParameters(NeoCommandInfo command)
        {
            if (command == null)
                return new CompletionParameter[0];

            List<CompletionParameter> parameters = new List<CompletionParameter>();
            if (command.RequiresTargetArgument)
                parameters.Add(CompletionParameter.Target());

            ParameterInfo[] methodParameters = command.Parameters;
            if (methodParameters != null)
            {
                for (int i = 0; i < methodParameters.Length; i++)
                {
                    ParameterInfo parameter = methodParameters[i];
                    if (parameter != null)
                        parameters.Add(CompletionParameter.Method(parameter.Name, parameter.ParameterType));
                }
            }

            return parameters.ToArray();
        }

        private static string BuildCommandParameterListHint(NeoCommandInfo command)
        {
            CompletionParameter[] parameters = GetCompletionParameters(command);
            if (parameters.Length == 0)
                return string.Empty;

            List<string> parts = new List<string>();
            for (int i = 0; i < parameters.Length; i++)
                parts.Add(BuildValueLabel(parameters[i].Type, parameters[i].Name, parameters[i].IsTarget));

            return string.Join("  ", parts.ToArray());
        }

        private static string BuildNextArgumentHint(string normalizedInput, NeoCommandInfo command)
        {
            ParameterContext context;
            if (!TryResolveParameterContext(normalizedInput, command, out context))
                return command != null ? command.Signature : string.Empty;

            HintContext hintContext = ResolveHintContext(context.CurrentValue, context.Parameter.Type, context.Parameter.Name);
            return BuildValueLabel(hintContext.Type, hintContext.Name, context.Parameter.IsTarget);
        }

        private static HintContext ResolveHintContext(string value, Type type, string name)
        {
            if (NeoCommandRegistry.IsCustomJsonParameterType(type))
            {
                HintContext nested;
                if (TryResolveActiveHintInCustomObject(value, type, out nested))
                    return nested;
            }

            return new HintContext(type, name);
        }

        private static bool TryResolveActiveHintInCustomObject(string value, Type objectType, out HintContext hint)
        {
            hint = default(HintContext);
            FieldInfo[] fields = GetSerializableFields(objectType);
            if (fields.Length == 0)
                return false;

            string safeValue = value ?? string.Empty;
            int openIndex = safeValue.IndexOf('{');
            if (openIndex < 0)
            {
                hint = new HintContext(fields[0].FieldType, fields[0].Name);
                return true;
            }

            if (IsBalancedAndClosed(safeValue, '{', '}'))
                return false;

            string content = safeValue.Substring(openIndex + 1);
            if (string.IsNullOrWhiteSpace(content) || EndsWithTopLevelSeparator(content, ','))
            {
                FieldInfo next = FindNextUnusedField(fields, content);
                if (next != null)
                {
                    hint = new HintContext(next.FieldType, next.Name);
                    return true;
                }

                return false;
            }

            List<TextSegment> segments = SplitTopLevelSegments(content, ',');
            if (segments.Count == 0)
                return false;

            TextSegment last = segments[segments.Count - 1];
            int separator = FindTopLevelNameValueSeparator(last.Text);
            if (separator < 0)
                return false;

            string fieldName = Unquote(last.Text.Substring(0, separator).Trim());
            FieldInfo field = FindSerializableField(fields, fieldName);
            if (field == null)
                return false;

            int valueStart = separator + 1;
            while (valueStart < last.Text.Length && char.IsWhiteSpace(last.Text[valueStart]))
                valueStart++;

            string rawValue = last.Text.Substring(valueStart);
            if (NeoCommandRegistry.IsCustomJsonParameterType(field.FieldType) && !IsValueComplete(rawValue, field.FieldType))
            {
                HintContext nested;
                if (TryResolveActiveHintInCustomObject(rawValue, field.FieldType, out nested))
                {
                    hint = nested;
                    return true;
                }
            }

            if (IsCollectionLike(field.FieldType) && !IsValueComplete(rawValue, field.FieldType))
            {
                Type elementType = GetListElementType(field.FieldType);
                if (NeoCommandRegistry.IsCustomJsonParameterType(elementType) && TryResolveActiveHintInCollection(rawValue, elementType, out hint))
                    return true;
            }

            hint = new HintContext(field.FieldType, field.Name);
            return true;
        }

        private static bool TryResolveActiveHintInCollection(string value, Type elementType, out HintContext hint)
        {
            hint = default(HintContext);
            string safeValue = value ?? string.Empty;
            int openIndex = safeValue.IndexOf('[');
            if (openIndex < 0)
            {
                hint = new HintContext(elementType, "item");
                return true;
            }

            if (IsBalancedAndClosed(safeValue, '[', ']'))
                return false;

            string content = safeValue.Substring(openIndex + 1);
            if (string.IsNullOrWhiteSpace(content) || EndsWithTopLevelSeparator(content, ','))
            {
                hint = new HintContext(elementType, "item");
                return true;
            }

            List<TextSegment> segments = SplitTopLevelSegments(content, ',');
            if (segments.Count == 0)
                return false;

            TextSegment last = segments[segments.Count - 1];
            if (NeoCommandRegistry.IsCustomJsonParameterType(elementType) && !IsValueComplete(last.Text, elementType))
                return TryResolveActiveHintInCustomObject(last.Text, elementType, out hint);

            hint = new HintContext(elementType, "item");
            return true;
        }

        private static string BuildValueLabel(Type type, string name, bool isTarget)
        {
            string label = string.IsNullOrEmpty(name) ? "value" : name;
            string typeName = isTarget ? "target" : GetDisplayTypeName(type);
            return "(" + typeName + ")" + label;
        }

        private static string BuildValueWritingHint(Type type, string name)
        {
            string label = string.IsNullOrEmpty(name) ? "value" : name;

            if (type == typeof(string) || type == typeof(char))
                return "Write " + label + " as quoted text. Example: \"PlayerData\".";
            if (type == typeof(bool))
                return "Select true or false.";
            if (type == typeof(int) || type == typeof(long))
                return "Write " + label + " as a whole number. Example: 123.";
            if (type == typeof(float) || type == typeof(double))
                return "Write " + label + " as a decimal number. Example: 1.0.";
            if (type != null && type.IsEnum)
                return "Select one of the enum values.";
            if (IsDictionaryLike(type))
                return "Write key/value pairs inside braces. Example: { \"key\": 1 }.";
            if (IsCollectionLike(type))
                return "Write items inside brackets. Add another item with a comma.";
            if (NeoCommandRegistry.IsCustomJsonParameterType(type))
                return "Write an object with fields inside braces. Press Tab to move field by field.";

            return "Write a value compatible with " + GetDisplayTypeName(type) + ".";
        }

        public static string GetSchemaForType(Type type)
        {
            if (type == null)
                return string.Empty;

            string cached;
            if (SchemaCache.TryGetValue(type, out cached))
                return cached;

            string schema = BuildSchemaForType(type);
            SchemaCache[type] = schema;
            return schema;
        }

        private static string BuildSchemaForType(Type type)
        {
            if (type == null)
                return string.Empty;
            if (IsPrimitiveLike(type) || type.IsEnum)
                return GetJsonPlaceholder(type);

            List<string> fields = new List<string>();
            FieldInfo[] serializableFields = GetSerializableFields(type);
            for (int i = 0; i < serializableFields.Length; i++)
            {
                FieldInfo field = serializableFields[i];
                if (field != null)
                    fields.Add(field.Name + ":" + GetJsonPlaceholder(field.FieldType));
            }

            return fields.Count == 0 ? "{}" : "{" + string.Join(",", fields.ToArray()) + "}";
        }

        private static NeoCommandSuggestion Empty(string input)
        {
            return new NeoCommandSuggestion
            {
                Completion = input ?? string.Empty,
                DisplayText = string.Empty,
                Hint = string.Empty,
                HasSuggestion = false,
                CursorIndex = (input ?? string.Empty).Length,
                Command = null,
                IsTargetSuggestion = false,
                IsInformationalOnly = false,
                Description = string.Empty
            };
        }

        private static bool TryGetSuffixAfterCursorCompletion(string prefix, string suffix, string completion, out string preservedSuffix)
        {
            preservedSuffix = string.Empty;

            if (string.IsNullOrEmpty(suffix))
                return true;

            string safePrefix = prefix ?? string.Empty;
            string safeCompletion = completion ?? string.Empty;
            if (!safeCompletion.StartsWith(safePrefix, StringComparison.Ordinal))
                return false;

            string inserted = safeCompletion.Substring(safePrefix.Length);
            int overlap = GetSafeInsertedSuffixLeadingOverlap(inserted, suffix);
            if (overlap <= 0)
                overlap = GetSafeInsertedSuffixTrailingOverlap(inserted, suffix);

            if (overlap > 0)
            {
                preservedSuffix = suffix.Substring(overlap);
                return true;
            }

            // When the cursor is inside already typed content, a completion built only from the
            // prefix can insert placeholders or closing braces before the real suffix. Examples:
            //   { "key": 5|4       -> prefix completion would become { "key": 5 }4
            //   { "key": 1, |...  -> prefix completion could duplicate the next pair
            // In those cases, abort the prefix merge and let the full-input suggestion path decide.
            if (ContainsNonWhitespace(suffix))
                return false;

            string remaining = suffix;
            while (!string.IsNullOrEmpty(remaining))
            {
                if (CompletionAlreadyInsertedLeadingSuffix(prefix, completion, remaining[0]))
                {
                    remaining = remaining.Substring(1);
                    continue;
                }

                if (char.IsWhiteSpace(remaining[0]))
                {
                    int nextMeaningfulIndex = FindNextNonWhitespaceIndex(remaining, 1);
                    if (nextMeaningfulIndex > 0 && CompletionAlreadyInsertedLeadingSuffix(prefix, completion, remaining[nextMeaningfulIndex]))
                    {
                        remaining = remaining.Substring(nextMeaningfulIndex + 1);
                        continue;
                    }
                }

                break;
            }

            preservedSuffix = remaining;
            return true;
        }

        private static int GetSafeInsertedSuffixLeadingOverlap(string inserted, string suffix)
        {
            if (string.IsNullOrEmpty(inserted) || string.IsNullOrEmpty(suffix))
                return 0;

            int max = Mathf.Min(inserted.Length, suffix.Length);
            for (int length = max; length > 0; length--)
            {
                if (string.Compare(inserted, 0, suffix, 0, length, StringComparison.OrdinalIgnoreCase) != 0)
                    continue;

                if (!ContainsNonWhitespace(suffix, 0, length))
                    continue;

                if (OverlapLeavesSafeSuffix(suffix, length))
                    return length;
            }

            return 0;
        }

        private static int GetSafeInsertedSuffixTrailingOverlap(string inserted, string suffix)
        {
            if (string.IsNullOrEmpty(inserted) || string.IsNullOrEmpty(suffix))
                return 0;

            int max = Mathf.Min(inserted.Length, suffix.Length);
            for (int length = max; length > 0; length--)
            {
                int insertedStart = inserted.Length - length;
                if (string.Compare(inserted, insertedStart, suffix, 0, length, StringComparison.OrdinalIgnoreCase) != 0)
                    continue;

                if (!ContainsNonWhitespace(suffix, 0, length))
                    continue;

                if (OverlapLeavesSafeSuffix(suffix, length))
                    return length;
            }

            return 0;
        }

        private static bool OverlapLeavesSafeSuffix(string suffix, int overlapLength)
        {
            if (string.IsNullOrEmpty(suffix) || overlapLength >= suffix.Length)
                return true;

            int next = FindNextNonWhitespaceIndex(suffix, overlapLength);
            if (next < 0)
                return true;

            char c = suffix[next];
            return c == ',' || c == ';' || c == '}' || c == ']';
        }

        private static bool ContainsNonWhitespace(string value)
        {
            return ContainsNonWhitespace(value, 0, (value ?? string.Empty).Length);
        }

        private static bool ContainsNonWhitespace(string value, int start, int length)
        {
            string safe = value ?? string.Empty;
            int safeStart = Mathf.Clamp(start, 0, safe.Length);
            int safeEnd = Mathf.Clamp(safeStart + Mathf.Max(0, length), safeStart, safe.Length);
            for (int i = safeStart; i < safeEnd; i++)
            {
                if (!char.IsWhiteSpace(safe[i]))
                    return true;
            }

            return false;
        }

        private static int FindNextNonWhitespaceIndex(string value, int startIndex)
        {
            if (string.IsNullOrEmpty(value))
                return -1;

            for (int i = Mathf.Max(0, startIndex); i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return i;
            }

            return -1;
        }

        private static bool CompletionAlreadyInsertedLeadingSuffix(string prefix, string completion, char suffixCharacter)
        {
            if (string.IsNullOrEmpty(completion) || completion.Length <= (prefix ?? string.Empty).Length)
                return false;

            string inserted = completion.Substring((prefix ?? string.Empty).Length);
            if (suffixCharacter == '"' || suffixCharacter == '\'')
                return HasUnclosedQuote(prefix, suffixCharacter) && inserted.IndexOf(suffixCharacter) >= 0;
            if (suffixCharacter == ']')
                return CountUnclosedDelimiters(prefix, '[', ']') > 0 && inserted.IndexOf(']') >= 0;
            if (suffixCharacter == '}')
                return CountUnclosedDelimiters(prefix, '{', '}') > 0 && inserted.IndexOf('}') >= 0;
            return false;
        }

        private static bool HasUnclosedQuote(string value, char quoteCharacter)
        {
            string safe = value ?? string.Empty;
            bool insideQuotes = false;
            bool escaped = false;
            for (int i = 0; i < safe.Length; i++)
            {
                char c = safe[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (c == quoteCharacter)
                    insideQuotes = !insideQuotes;
            }
            return insideQuotes;
        }

        private static int CountUnclosedDelimiters(string value, char open, char close)
        {
            string safe = value ?? string.Empty;
            bool insideQuotes = false;
            char quote = '\0';
            bool escaped = false;
            int count = 0;
            for (int i = 0; i < safe.Length; i++)
            {
                char c = safe[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    if (!insideQuotes)
                    {
                        insideQuotes = true;
                        quote = c;
                    }
                    else if (quote == c)
                    {
                        insideQuotes = false;
                        quote = '\0';
                    }
                    continue;
                }
                if (insideQuotes)
                    continue;
                if (c == open) count++;
                else if (c == close && count > 0) count--;
            }
            return count;
        }

        private static bool TryGetTargetSuggestionContext(string input, NeoCommandInfo command, out string targetPrefix)
        {
            targetPrefix = string.Empty;
            if (command == null || !command.RequiresTargetArgument)
                return false;

            string safeInput = input ?? string.Empty;
            string trimmedStart = safeInput.TrimStart();
            if (!trimmedStart.StartsWith(CommandPrefix.ToString(), StringComparison.Ordinal))
                return false;

            string normalized = trimmedStart.Substring(1);
            string commandName = GetCommandNameFromNormalizedInput(normalized);
            if (string.IsNullOrEmpty(commandName))
                return false;

            int commandEnd = commandName.Length;
            string rest = commandEnd < normalized.Length ? normalized.Substring(commandEnd) : string.Empty;
            if (string.IsNullOrEmpty(rest) || !char.IsWhiteSpace(rest[0]))
                return false;

            List<ArgumentToken> args = SplitArgumentsWithBounds(rest);
            bool endsWithTopLevelWhitespace = EndsWithTopLevelWhitespace(rest);
            if (args.Count == 0 && endsWithTopLevelWhitespace)
            {
                targetPrefix = string.Empty;
                return true;
            }

            if (args.Count == 1 && !endsWithTopLevelWhitespace)
            {
                targetPrefix = args[0].Value;
                return true;
            }

            return false;
        }

        private static NeoCommandSuggestion[] GetTargetArgumentSuggestions(string input, NeoCommandInfo command, string targetPrefix)
        {
            if (command == null || !command.RequiresTargetArgument)
                return EmptySuggestions;

            TargetCompletion[] targets = GetMatchingTargetCompletions(command, targetPrefix);
            if (targets.Length == 0)
                return EmptySuggestions;

            string normalizedInput = (input ?? string.Empty).TrimStart();
            string leading = (input ?? string.Empty).Substring(0, (input ?? string.Empty).Length - normalizedInput.Length);
            string commandPrefix = leading + CommandPrefix + command.Name;

            NeoCommandSuggestion[] suggestions = new NeoCommandSuggestion[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                TargetCompletion target = targets[i];
                string completion = commandPrefix + " " + QuoteArgument(target.CompletionValue);
                if (CommandHasArgumentsAfterTarget(command))
                    completion += " ";

                suggestions[i] = new NeoCommandSuggestion
                {
                    Completion = completion,
                    DisplayText = target.DisplayText,
                    Hint = target.Hint,
                    HasSuggestion = true,
                    CursorIndex = completion.Length,
                    Command = command,
                    IsTargetSuggestion = true,
                    IsInformationalOnly = false,
                    Description = target.Hint
                };
            }

            return suggestions;
        }

        private static bool CommandHasArgumentsAfterTarget(NeoCommandInfo command)
        {
            return command != null && command.Parameters != null && command.Parameters.Length > 0;
        }

        private static TargetCompletion[] GetMatchingTargetCompletions(NeoCommandInfo command, string targetPrefix)
        {
            if (command == null || command.TargetType == null || !typeof(MonoBehaviour).IsAssignableFrom(command.TargetType))
                return new TargetCompletion[0];

            string normalizedPrefix = NormalizeTargetFilter(targetPrefix);
            MonoBehaviour[] found = NeoCommandTargetCache.FindActiveTargets(command.TargetType);
            List<MonoBehaviour> targets = new List<MonoBehaviour>();
            for (int i = 0; i < found.Length; i++)
            {
                MonoBehaviour target = found[i];
                if (!TargetMatchesFilter(target, normalizedPrefix))
                    continue;

                targets.Add(target);
            }

            Dictionary<string, int> nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < targets.Count; i++)
            {
                string name = GetTargetBaseName(targets[i]);
                int count;
                nameCounts.TryGetValue(name, out count);
                nameCounts[name] = count + 1;
            }

            List<TargetCompletion> completions = new List<TargetCompletion>();
            for (int i = 0; i < targets.Count; i++)
            {
                MonoBehaviour target = targets[i];
                string baseName = GetTargetBaseName(target);
                string identifier = NeoUnityObjectUtility.GetObjectIdentifier(target);
                bool duplicateName = nameCounts.ContainsKey(baseName) && nameCounts[baseName] > 1;
                string completionValue = duplicateName && !string.IsNullOrEmpty(identifier) ? baseName + " #" + identifier : baseName;
                string hint = target.GetType().Name;
                if (target.gameObject != null && !string.Equals(target.gameObject.name, target.name, StringComparison.Ordinal))
                    hint += " on " + target.gameObject.name;
                completions.Add(new TargetCompletion(completionValue, completionValue, hint));
            }

            return completions.ToArray();
        }

        private static bool TargetMatchesFilter(MonoBehaviour target, string normalizedPrefix)
        {
            if (target == null)
                return false;
            if (string.IsNullOrEmpty(normalizedPrefix))
                return true;
            string baseName = GetTargetBaseName(target);
            if (StartsWithIgnoreCase(baseName, normalizedPrefix)) return true;
            if (target.gameObject != null && StartsWithIgnoreCase(target.gameObject.name, normalizedPrefix)) return true;
            if (StartsWithIgnoreCase(target.name, normalizedPrefix)) return true;
            string identifier = NeoUnityObjectUtility.GetObjectIdentifier(target);
            if (StartsWithIgnoreCase(identifier, normalizedPrefix)) return true;
            if (target.gameObject != null)
            {
                string gameObjectIdentifier = NeoUnityObjectUtility.GetObjectIdentifier(target.gameObject);
                if (StartsWithIgnoreCase(gameObjectIdentifier, normalizedPrefix)) return true;
            }
            return false;
        }

        private static string GetTargetBaseName(MonoBehaviour target)
        {
            return target != null && target.gameObject != null ? target.gameObject.name : (target != null ? target.name : string.Empty);
        }

        private static string NormalizeTargetFilter(string value)
        {
            string result = (value ?? string.Empty).Trim();
            if (result.Length >= 2)
            {
                char first = result[0];
                char last = result[result.Length - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                    result = result.Substring(1, result.Length - 2);
                else if (first == '"' || first == '\'')
                    result = result.Substring(1);
            }
            if (result.StartsWith("#", StringComparison.Ordinal))
                result = result.Substring(1);
            return result.Trim();
        }

        private static bool StartsWithIgnoreCase(string value, string prefix)
        {
            return !string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(prefix) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string QuoteArgument(string value)
        {
            string safeValue = value ?? string.Empty;
            safeValue = safeValue.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return "\"" + safeValue + "\"";
        }

        private static FieldInfo[] GetSerializableFields(Type type)
        {
            return NeoCommandTypeUtility.GetSerializableFields(type);
        }

        private static FieldInfo FindSerializableField(FieldInfo[] fields, string name)
        {
            return NeoCommandTypeUtility.FindSerializableField(fields, name);
        }

        private static bool IsDictionaryLike(Type type)
        {
            return NeoCommandTypeUtility.IsDictionaryLike(type);
        }

        private static bool IsCollectionLike(Type type)
        {
            return NeoCommandTypeUtility.IsCollectionLike(type);
        }

        private static Type GetListElementType(Type type)
        {
            return NeoCommandTypeUtility.GetCollectionElementType(type);
        }

        private static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
        {
            return NeoCommandTypeUtility.TryGetDictionaryTypes(type, out keyType, out valueType);
        }

        private static bool TryGetCollectionElementType(Type type, out Type elementType)
        {
            return NeoCommandTypeUtility.TryGetCollectionElementType(type, out elementType);
        }

        private static bool IsPrimitiveLike(Type type)
        {
            return type == typeof(string) || type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double) || type == typeof(bool);
        }

        private static string GetDisplayTypeName(Type type)
        {
            if (type == null)
                return "value";
            if (type.IsArray)
                return GetDisplayTypeName(type.GetElementType()) + "[]";
            if (type.IsGenericType)
            {
                string typeName = type.Name;
                int tickIndex = typeName.IndexOf('`');
                if (tickIndex >= 0)
                    typeName = typeName.Substring(0, tickIndex);
                Type[] arguments = type.GetGenericArguments();
                string[] argumentNames = new string[arguments.Length];
                for (int i = 0; i < arguments.Length; i++)
                    argumentNames[i] = GetDisplayTypeName(arguments[i]);
                return typeName + "<" + string.Join(", ", argumentNames) + ">";
            }
            return NeoCommandRegistry.GetFriendlyTypeName(type);
        }

        private static string GetJsonPlaceholder(Type type)
        {
            if (type == null) return "null";
            if (type == typeof(string)) return "\"value\"";
            if (type == typeof(char)) return "\"a\"";
            if (type == typeof(bool)) return "true";
            if (type == typeof(float) || type == typeof(double)) return "1.0";
            if (type == typeof(int) || type == typeof(long)) return "1";
            if (type.IsEnum)
            {
                string[] names = Enum.GetNames(type);
                return names.Length > 0 ? names[0] : type.Name;
            }
            if (IsDictionaryLike(type))
            {
                Type keyType;
                Type valueType;
                TryGetDictionaryTypes(type, out keyType, out valueType);
                return "{ " + GetDictionaryKeyPlaceholder(keyType) + ": " + GetJsonPlaceholder(valueType) + " }";
            }
            if (IsCollectionLike(type))
            {
                string element = GetJsonPlaceholder(GetListElementType(type));
                return string.IsNullOrEmpty(element) || element == "null" ? "[]" : "[" + element + "]";
            }
            if (NeoCommandRegistry.IsCustomJsonParameterType(type))
                return GetSchemaForType(type);
            return "null";
        }

        private static string GetDictionaryKeyPlaceholder(Type keyType)
        {
            if (keyType == typeof(string) || keyType == typeof(char)) return "\"key\"";
            if (keyType == typeof(int) || keyType == typeof(long)) return "1";
            if (keyType != null && keyType.IsEnum)
            {
                string[] names = Enum.GetNames(keyType);
                return names.Length > 0 ? names[0] : "key";
            }
            return "\"key\"";
        }

        private static int GetDictionaryPairCursor(string pair, Type keyType)
        {
            if (keyType == typeof(string) || keyType == typeof(char))
            {
                int quote = pair.IndexOf('"');
                return quote >= 0 ? quote + 1 : pair.Length;
            }
            return pair.Length;
        }

        private struct ArgumentToken
        {
            public ArgumentToken(string value, int start, int end)
            {
                Value = value ?? string.Empty;
                Start = start;
                End = end;
            }
            public readonly string Value;
            public readonly int Start;
            public readonly int End;
        }

        private struct TextSegment
        {
            public TextSegment(string text, int start, int end)
            {
                Text = text ?? string.Empty;
                Start = start;
                End = end;
            }
            public readonly string Text;
            public readonly int Start;
            public readonly int End;
        }

        private struct CompletionParameter
        {
            public static CompletionParameter Target()
            {
                return new CompletionParameter(true, "target name", typeof(string));
            }
            public static CompletionParameter Method(string name, Type type)
            {
                return new CompletionParameter(false, name, type);
            }
            private CompletionParameter(bool isTarget, string name, Type type)
            {
                IsTarget = isTarget;
                Name = name ?? string.Empty;
                Type = type;
            }
            public readonly bool IsTarget;
            public readonly string Name;
            public readonly Type Type;
        }

        private struct CompletionBuild
        {
            public CompletionBuild(string text, int cursorIndex)
            {
                Text = text ?? string.Empty;
                CursorIndex = Mathf.Clamp(cursorIndex, 0, Text.Length);
            }
            public readonly string Text;
            public readonly int CursorIndex;
        }

        private struct ParameterContext
        {
            public ParameterContext(CompletionParameter parameter, int parameterIndex, string currentValue, string prefixBeforeValueInNormalized, int valueStartInNormalized)
            {
                Parameter = parameter;
                ParameterIndex = parameterIndex;
                CurrentValue = currentValue ?? string.Empty;
                PrefixBeforeValueInNormalized = prefixBeforeValueInNormalized ?? string.Empty;
                ValueStartInNormalized = valueStartInNormalized;
            }
            public readonly CompletionParameter Parameter;
            public readonly int ParameterIndex;
            public readonly string CurrentValue;
            public readonly string PrefixBeforeValueInNormalized;
            public readonly int ValueStartInNormalized;
        }

        private struct SelectableValueContext
        {
            public SelectableValueContext(NeoCommandInfo command, Type type, string prefixBeforeValue, string typedValue, string suffixAfterValue, string displayName)
            {
                Command = command;
                Type = type;
                PrefixBeforeValue = prefixBeforeValue ?? string.Empty;
                TypedValue = typedValue ?? string.Empty;
                SuffixAfterValue = suffixAfterValue ?? string.Empty;
                DisplayName = string.IsNullOrEmpty(displayName) ? "value" : displayName;
            }
            public readonly NeoCommandInfo Command;
            public readonly Type Type;
            public readonly string PrefixBeforeValue;
            public readonly string TypedValue;
            public readonly string SuffixAfterValue;
            public readonly string DisplayName;
        }

        private struct HintContext
        {
            public HintContext(Type type, string name)
            {
                Type = type;
                Name = string.IsNullOrEmpty(name) ? "value" : name;
            }
            public readonly Type Type;
            public readonly string Name;
        }

        private struct TargetCompletion
        {
            public TargetCompletion(string completionValue, string displayText, string hint)
            {
                CompletionValue = completionValue ?? string.Empty;
                DisplayText = displayText ?? string.Empty;
                Hint = hint ?? string.Empty;
            }
            public readonly string CompletionValue;
            public readonly string DisplayText;
            public readonly string Hint;
        }
    }
}
#endif
