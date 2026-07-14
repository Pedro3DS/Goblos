using UnityEngine;

namespace Project.Lobby
{
    /// <summary>
    /// Paleta central de cores dos players. Mantém a ordem fixa (índice = cor),
    /// já que o índice é o que trafega pela rede (NetworkVariable<int>), nunca a Color em si.
    /// </summary>
    [CreateAssetMenu(fileName = "LobbyPlayerColorPalette", menuName = "Project/Lobby/Player Color Palette")]
    public class LobbyPlayerColorPalette : ScriptableObject
    {
        [SerializeField]
        private ColorEntry[] _colors =
        {
            new ColorEntry { Name = "Vermelho", Color = new Color(0.85f, 0.15f, 0.15f) },
            new ColorEntry { Name = "Azul",     Color = new Color(0.15f, 0.35f, 0.85f) },
            new ColorEntry { Name = "Verde",    Color = new Color(0.20f, 0.75f, 0.30f) },
            new ColorEntry { Name = "Amarelo",  Color = new Color(0.95f, 0.85f, 0.15f) },
        };

        public int Count => _colors.Length;

        public Color GetColor(int index)
        {
            if (index < 0 || index >= _colors.Length) return Color.white;
            return _colors[index].Color;
        }

        public string GetName(int index)
        {
            if (index < 0 || index >= _colors.Length) return "Sem cor";
            return _colors[index].Name;
        }

        [System.Serializable]
        private struct ColorEntry
        {
            public string Name;
            public Color Color;
        }
    }
}
