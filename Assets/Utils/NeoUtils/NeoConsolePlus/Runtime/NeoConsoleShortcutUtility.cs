using UnityEngine;

namespace Neo.ConsolePlus
{
    internal static class NeoConsoleShortcutUtility
    {
        public static bool IsTab(Event current)
        {
            return current != null && (current.keyCode == KeyCode.Tab || current.character == '\t');
        }

        public static bool IsSubmit(Event current)
        {
            return current != null && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter);
        }

        public static bool IsDeletePreviousWord(Event current)
        {
            return current != null &&
                   current.type == EventType.KeyDown &&
                   current.keyCode == KeyCode.Backspace &&
                   HasWordDeleteModifier(current);
        }

        public static bool IsDeleteNextWord(Event current)
        {
            return current != null &&
                   current.type == EventType.KeyDown &&
                   current.keyCode == KeyCode.Delete &&
                   HasWordDeleteModifier(current);
        }

        public static bool IsVerticalArrow(Event current)
        {
            return current != null && (current.keyCode == KeyCode.UpArrow || current.keyCode == KeyCode.DownArrow);
        }

        public static int GetVerticalArrowDirection(Event current)
        {
            if (current == null)
                return 0;

            if (current.keyCode == KeyCode.UpArrow)
                return -1;

            if (current.keyCode == KeyCode.DownArrow)
                return 1;

            return 0;
        }

        public static bool IsHistoryNavigation(Event current)
        {
            return IsVerticalArrow(current) && HasHistoryModifier(current);
        }

        public static bool IsSuggestionNavigation(Event current)
        {
            return IsVerticalArrow(current) && !HasHistoryModifier(current);
        }

        private static bool HasHistoryModifier(Event current)
        {
            return HasControlOrCommandModifier(current);
        }

        private static bool HasWordDeleteModifier(Event current)
        {
            return HasControlOrCommandModifier(current);
        }

        private static bool HasControlOrCommandModifier(Event current)
        {
            if (current == null)
                return false;

            return (current.modifiers & EventModifiers.Control) != 0 ||
                   (current.modifiers & EventModifiers.Command) != 0;
        }
    }
}
