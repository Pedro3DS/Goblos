using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Interaction
{
    /// <summary>
    /// Fica no prefab do player. Só roda a detecção quando IsOwner (evita todo mundo
    /// fazendo raycast pra todo mundo). Funciona tanto por Input System (recomendado,
    /// via "Invoke Unity Events" na action "Interact" do seu Input Actions) quanto
    /// por fallback de tecla, se você não quiser mexer no PlayerInput agora.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Detecção")]
        [SerializeField] private Transform _eyePoint;
        [SerializeField] private float _interactionRange = 2.5f;
        [SerializeField] private LayerMask _interactableMask = ~0;

        [Header("Fallback (opcional)")]
        [SerializeField] private bool _useLegacyKeyFallback = true;
        [SerializeField] private KeyCode _legacyInteractKey = KeyCode.E;

        private NetworkObject _networkObject;
        private NetworkInteractable _focused;

        public NetworkInteractable Focused => _focused;
        public bool HasFocus => _focused != null;

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
        }

        private void Update()
        {
            if (!_networkObject.IsOwner) return;

            UpdateFocus();

            if (_useLegacyKeyFallback && _focused != null && Input.GetKeyDown(_legacyInteractKey))
            {
                TryInteract();
            }
        }

        private void UpdateFocus()
        {
            _focused = null;
            if (_eyePoint == null) return;

            if (Physics.Raycast(_eyePoint.position, _eyePoint.forward, out var hit, _interactionRange, _interactableMask))
            {
                if (hit.collider.TryGetComponent<NetworkInteractable>(out var interactable))
                {
                    _focused = interactable;
                }
                else
                {
                    _focused = hit.collider.GetComponentInParent<NetworkInteractable>();
                }
            }
        }

        /// <summary>
        /// Ligue este método na action "Interact" do seu Input Actions asset
        /// (Player Input component -> Behavior: Invoke Unity Events -> Interact -> Interact.performed).
        /// </summary>
        public void OnInteractPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryInteract();
        }

        private void TryInteract()
        {
            if (_focused == null || !_networkObject.IsOwner) return;
            _focused.RequestInteractRpc(_networkObject.OwnerClientId);
        }
    }
}
