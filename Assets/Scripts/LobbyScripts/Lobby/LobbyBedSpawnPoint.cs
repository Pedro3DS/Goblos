using TMPro;
using UnityEngine;

namespace Project.Lobby
{
    /// <summary>
    /// Componente puramente visual/posicional de uma cama de palha no lobby.
    /// Não tem NetworkBehaviour - quem manda no estado é o LobbyManager (servidor),
    /// este script só aplica o que já veio sincronizado.
    /// </summary>
    public class LobbyBedSpawnPoint : MonoBehaviour
    {
        [SerializeField] private int _bedIndex;

        [Header("Pontos de referência")]
        [Tooltip("Onde o player fica em pé/sentado ao lado da cama.")]
        [SerializeField] private Transform _sitPoint;
        [Tooltip("Onde o player fica posicionado ao deitar.")]
        [SerializeField] private Transform _layPoint;

        [Header("Visual")]
        [Tooltip("Partes da cama que recebem a cor do dono (ex: o tecido/palha tingida).")]
        [SerializeField] private Renderer[] _colorableRenderers;
        [SerializeField] private string _colorPropertyName = "_BaseColor";
        [SerializeField] private TMP_Text _nameplateText;

        private MaterialPropertyBlock _propertyBlock;
        private int _colorPropertyId;

        public int BedIndex => _bedIndex;
        public Transform SitPoint => _sitPoint;
        public Transform LayPoint => _layPoint;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _colorPropertyId = Shader.PropertyToID(_colorPropertyName);
        }

        public void ApplyPlayerData(string playerName, Color color)
        {
            if (_nameplateText != null)
                _nameplateText.text = playerName;

            SetColor(color);
        }

        public void ResetBed()
        {
            if (_nameplateText != null)
                _nameplateText.text = string.Empty;

            SetColor(Color.white);
        }

        private void SetColor(Color color)
        {
            if (_colorableRenderers == null) return;

            foreach (var rend in _colorableRenderers)
            {
                if (rend == null) continue;

                rend.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(_colorPropertyId, color);
                rend.SetPropertyBlock(_propertyBlock);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_sitPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_sitPoint.position, 0.2f);
            }

            if (_layPoint != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(_layPoint.position, new Vector3(0.5f, 0.1f, 1.8f));
            }
        }
#endif
    }
}
