using UnityEngine;

namespace Project.Lobby
{
    /// <summary>
    /// Aplica a cor recebida via NetworkVariable no(s) renderer(s) do personagem.
    /// Usa MaterialPropertyBlock para não instanciar um material novo por player
    /// (evita leak de memória e permite SRP batching).
    /// </summary>
    public class PlayerVisualCustomizer : MonoBehaviour
    {
        [Tooltip("Parte(s) do modelo que recebem a cor de identificação (ex: torso, capuz).")]
        [SerializeField] private Renderer[] _customizableRenderers;

        [Tooltip("Nome da propriedade de cor no shader. URP/Lit = _BaseColor, Built-in Standard = _Color.")]
        [SerializeField] private string _colorPropertyName = "_BaseColor";

        [SerializeField] private LobbyPlayerColorPalette _palette;

        private MaterialPropertyBlock _propertyBlock;
        private int _colorPropertyId;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _colorPropertyId = Shader.PropertyToID(_colorPropertyName);
        }

        public void ApplyColor(int colorIndex)
        {
            if (_palette == null || _customizableRenderers == null) return;

            var color = _palette.GetColor(colorIndex);

            foreach (var rend in _customizableRenderers)
            {
                if (rend == null) continue;

                rend.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(_colorPropertyId, color);
                rend.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
