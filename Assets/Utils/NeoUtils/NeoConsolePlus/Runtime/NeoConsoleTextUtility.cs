#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Text;
using UnityEngine;

namespace Neo.ConsolePlus
{
    internal static class NeoConsoleTextUtility
    {
        private const string Ellipsis = "...";
        private const string TransparentColorOpen = "<color=#00000000>";
        private const string TransparentColorClose = "</color>";

        public static string SanitizeCommandInput(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\r", " ").Replace("\n", " ");
        }

        public static string MakeSingleLine(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        }

        public static string StripRichTextTags(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string output = value;
            while (true)
            {
                int start = output.IndexOf('<');
                if (start < 0)
                    break;

                int end = output.IndexOf('>', start);
                if (end < 0)
                    break;

                output = output.Remove(start, end - start + 1);
            }

            return output;
        }


        public static bool TryDeletePreviousWord(string value, int selectionStart, int selectionEnd, out string newValue, out int newCursorIndex)
        {
            string safeValue = value ?? string.Empty;
            int start = Mathf.Clamp(Mathf.Min(selectionStart, selectionEnd), 0, safeValue.Length);
            int end = Mathf.Clamp(Mathf.Max(selectionStart, selectionEnd), 0, safeValue.Length);

            newValue = safeValue;
            newCursorIndex = start;

            if (start != end)
            {
                newValue = safeValue.Remove(start, end - start);
                newCursorIndex = start;
                return true;
            }

            if (start <= 0)
                return false;

            int deleteStart = FindPreviousWordBoundary(safeValue, start);
            if (deleteStart == start)
                deleteStart = Mathf.Max(0, start - 1);

            newValue = safeValue.Remove(deleteStart, start - deleteStart);
            newCursorIndex = deleteStart;
            return true;
        }

        public static bool TryDeleteNextWord(string value, int selectionStart, int selectionEnd, out string newValue, out int newCursorIndex)
        {
            string safeValue = value ?? string.Empty;
            int start = Mathf.Clamp(Mathf.Min(selectionStart, selectionEnd), 0, safeValue.Length);
            int end = Mathf.Clamp(Mathf.Max(selectionStart, selectionEnd), 0, safeValue.Length);

            newValue = safeValue;
            newCursorIndex = start;

            if (start != end)
            {
                newValue = safeValue.Remove(start, end - start);
                newCursorIndex = start;
                return true;
            }

            if (start >= safeValue.Length)
                return false;

            int deleteEnd = FindNextWordBoundary(safeValue, start);
            if (deleteEnd == start)
                deleteEnd = Mathf.Min(safeValue.Length, start + 1);

            newValue = safeValue.Remove(start, deleteEnd - start);
            newCursorIndex = start;
            return true;
        }

        private static int FindPreviousWordBoundary(string value, int cursorIndex)
        {
            int index = Mathf.Clamp(cursorIndex, 0, value != null ? value.Length : 0);
            if (string.IsNullOrEmpty(value) || index <= 0)
                return index;

            while (index > 0 && char.IsWhiteSpace(value[index - 1]))
                index--;

            if (index <= 0)
                return index;

            CharacterGroup group = GetCharacterGroup(value[index - 1]);
            while (index > 0 && GetCharacterGroup(value[index - 1]) == group)
                index--;

            return index;
        }

        private static int FindNextWordBoundary(string value, int cursorIndex)
        {
            int index = Mathf.Clamp(cursorIndex, 0, value != null ? value.Length : 0);
            if (string.IsNullOrEmpty(value) || index >= value.Length)
                return index;

            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;

            if (index >= value.Length)
                return index;

            CharacterGroup group = GetCharacterGroup(value[index]);
            while (index < value.Length && GetCharacterGroup(value[index]) == group)
                index++;

            return index;
        }

        private static CharacterGroup GetCharacterGroup(char value)
        {
            if (char.IsLetterOrDigit(value) || value == '_')
                return CharacterGroup.Word;

            if (char.IsWhiteSpace(value))
                return CharacterGroup.Whitespace;

            return CharacterGroup.Symbol;
        }

        private enum CharacterGroup
        {
            Word,
            Whitespace,
            Symbol
        }

        public static bool TryGetInlineGhostParts(
            string input,
            int cursorIndex,
            string completion,
            out string hiddenPrefix,
            out string visibleGhost,
            out string hiddenSuffix)
        {
            string safeInput = input ?? string.Empty;
            string safeCompletion = completion ?? string.Empty;
            int safeCursorIndex = Mathf.Clamp(cursorIndex, 0, safeInput.Length);

            hiddenPrefix = string.Empty;
            visibleGhost = string.Empty;
            hiddenSuffix = string.Empty;

            if (string.IsNullOrEmpty(safeCompletion))
                return false;

            if (safeCompletion.StartsWith(safeInput, StringComparison.OrdinalIgnoreCase) && safeCompletion.Length > safeInput.Length)
            {
                hiddenPrefix = safeCompletion.Substring(0, safeInput.Length);
                visibleGhost = safeCompletion.Substring(safeInput.Length);
                return !string.IsNullOrEmpty(visibleGhost);
            }

            string prefixBeforeCursor = safeInput.Substring(0, safeCursorIndex);
            string suffixAfterCursor = safeInput.Substring(safeCursorIndex);
            if (safeCompletion.StartsWith(prefixBeforeCursor, StringComparison.OrdinalIgnoreCase) && safeCompletion.Length > prefixBeforeCursor.Length)
            {
                int visibleEnd = safeCompletion.Length;
                if (!string.IsNullOrEmpty(suffixAfterCursor) &&
                    safeCompletion.EndsWith(suffixAfterCursor, StringComparison.OrdinalIgnoreCase))
                {
                    visibleEnd = safeCompletion.Length - suffixAfterCursor.Length;
                }

                if (visibleEnd > prefixBeforeCursor.Length)
                {
                    hiddenPrefix = safeCompletion.Substring(0, prefixBeforeCursor.Length);
                    visibleGhost = safeCompletion.Substring(prefixBeforeCursor.Length, visibleEnd - prefixBeforeCursor.Length);
                    hiddenSuffix = visibleEnd < safeCompletion.Length ? safeCompletion.Substring(visibleEnd) : string.Empty;
                    return !string.IsNullOrEmpty(visibleGhost);
                }
            }

            string rightTrimmedInput = safeInput.TrimEnd();
            if (safeCursorIndex >= safeInput.Length &&
                rightTrimmedInput.Length != safeInput.Length &&
                safeCompletion.StartsWith(rightTrimmedInput, StringComparison.OrdinalIgnoreCase) &&
                safeCompletion.Length > rightTrimmedInput.Length)
            {
                hiddenPrefix = safeCompletion.Substring(0, rightTrimmedInput.Length);
                visibleGhost = safeCompletion.Substring(rightTrimmedInput.Length);
                return !string.IsNullOrEmpty(visibleGhost);
            }

            return false;
        }

        public static string BuildInlineGhostRichText(string hiddenPrefix, string visibleGhost, string hiddenSuffix)
        {
            StringBuilder builder = new StringBuilder();

            if (!string.IsNullOrEmpty(hiddenPrefix))
                builder.Append(TransparentColorOpen).Append(hiddenPrefix).Append(TransparentColorClose);

            builder.Append(visibleGhost ?? string.Empty);

            if (!string.IsNullOrEmpty(hiddenSuffix))
                builder.Append(TransparentColorOpen).Append(hiddenSuffix).Append(TransparentColorClose);

            return builder.ToString();
        }

        public static string GetInlineGhostSuffix(string input, string completion)
        {
            string safeInput = input ?? string.Empty;
            string safeCompletion = completion ?? string.Empty;

            if (string.IsNullOrEmpty(safeCompletion))
                return string.Empty;

            if (safeCompletion.StartsWith(safeInput, StringComparison.OrdinalIgnoreCase))
                return safeCompletion.Substring(safeInput.Length).TrimStart();

            string rightTrimmedInput = safeInput.TrimEnd();
            if (rightTrimmedInput.Length != safeInput.Length &&
                safeCompletion.StartsWith(rightTrimmedInput, StringComparison.OrdinalIgnoreCase))
            {
                return safeCompletion.Substring(rightTrimmedInput.Length).TrimStart();
            }

            return string.Empty;
        }

        public static bool IsNeoCommandLog(string message)
        {
            return !string.IsNullOrEmpty(message) && StripRichTextTags(message).Contains("[NeoCommand]");
        }

        public static bool IsPlainTextTruncatedToFit(string value, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(value) || style == null || width <= 8f)
                return false;

            return style.CalcSize(new GUIContent(value)).x > width;
        }

        public static string TruncatePlainTextToFit(string value, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(value) || style == null)
                return string.Empty;

            if (style.CalcSize(new GUIContent(value)).x <= width)
                return value;

            if (style.CalcSize(new GUIContent(Ellipsis)).x > width)
                return string.Empty;

            int low = 0;
            int high = value.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = value.Substring(0, mid).TrimEnd() + Ellipsis;
                if (style.CalcSize(new GUIContent(candidate)).x <= width)
                    low = mid;
                else
                    high = mid - 1;
            }

            return value.Substring(0, Mathf.Clamp(low, 0, value.Length)).TrimEnd() + Ellipsis;
        }

        public static bool IsRichTextTruncatedToMaxLines(string value, GUIStyle style, float width, int maxLines)
        {
            if (string.IsNullOrEmpty(value) || style == null || width <= 8f || maxLines <= 0)
                return false;

            return !FitsWithinLineLimit(MakeSingleLine(value), style, width, maxLines);
        }

        public static string TruncateRichTextToMaxLines(string value, GUIStyle style, float width, int maxLines)
        {
            if (string.IsNullOrEmpty(value) || style == null || width <= 8f || maxLines <= 0)
                return string.Empty;

            string singleLineValue = MakeSingleLine(value);
            if (FitsWithinLineLimit(singleLineValue, style, width, maxLines))
                return singleLineValue;

            string colorPrefix;
            string suffix;
            if (TrySplitRichColorPrefix(singleLineValue, out colorPrefix, out suffix))
            {
                string plainSuffix = StripRichTextTags(suffix);
                return colorPrefix + TruncatePlainTextToMaxLines(plainSuffix, style, width, maxLines, colorPrefix);
            }

            return TruncatePlainTextToMaxLines(StripRichTextTags(singleLineValue), style, width, maxLines, string.Empty);
        }

        public static string TruncateRichTextToFit(string value, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(value) || style == null || width <= 8f)
                return string.Empty;

            if (style.CalcSize(new GUIContent(value)).x <= width)
                return value;

            string colorPrefix;
            string suffix;
            if (TrySplitRichColorPrefix(value, out colorPrefix, out suffix))
            {
                float prefixWidth = style.CalcSize(new GUIContent(colorPrefix)).x;
                string truncatedSuffix = TruncatePlainTextToFit(suffix, style, Mathf.Max(8f, width - prefixWidth));
                return colorPrefix + truncatedSuffix;
            }

            return TruncatePlainTextToFit(StripRichTextTags(value), style, width);
        }

        public static bool FitsWithinLineLimit(string value, GUIStyle style, float width, int maxLines)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            if (style == null || maxLines <= 0)
                return false;

            float maxHeight = Mathf.Max(style.lineHeight + 2f, style.lineHeight * maxLines + 2f);
            return style.CalcHeight(new GUIContent(value), Mathf.Max(8f, width)) <= maxHeight;
        }

        public static bool TrySplitRichColorPrefix(string value, out string colorPrefix, out string suffix)
        {
            colorPrefix = string.Empty;
            suffix = string.Empty;

            if (string.IsNullOrEmpty(value) || !value.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
                return false;

            const string closeTag = "</color>";
            int closeIndex = value.IndexOf(closeTag, StringComparison.OrdinalIgnoreCase);
            if (closeIndex < 0)
                return false;

            colorPrefix = value.Substring(0, closeIndex + closeTag.Length);
            suffix = value.Substring(closeIndex + closeTag.Length);
            return true;
        }

        private static string TruncatePlainTextToMaxLines(string value, GUIStyle style, float width, int maxLines, string prefix)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string safePrefix = prefix ?? string.Empty;
            if (FitsWithinLineLimit(safePrefix + value, style, width, maxLines))
                return value;

            int low = 0;
            int high = value.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = safePrefix + value.Substring(0, mid).TrimEnd() + Ellipsis;
                if (FitsWithinLineLimit(candidate, style, width, maxLines))
                    low = mid;
                else
                    high = mid - 1;
            }

            return value.Substring(0, Mathf.Clamp(low, 0, value.Length)).TrimEnd() + Ellipsis;
        }
    }
}
#endif
