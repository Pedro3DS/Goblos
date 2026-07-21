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
    ///
    /// O _eyePoint não precisa mais ser arrastado manualmente no Inspector: o
    /// PlayerSetup chama SetEyePoint assim que a câmera do dono é criada.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Detecção")]
        [Tooltip("Setado automaticamente pelo PlayerSetup quando a câmera do dono é criada. Pode deixar vazio no prefab.")]
        [SerializeField] private Transform _eyePoint;
        [SerializeField] private float _interactionRange = 2.5f;
        [SerializeField] private LayerMask _interactableMask = ~0;

        [Header("Fallback (opcional)")]
        [SerializeField] private bool _useLegacyKeyFallback = true;
        [SerializeField] private KeyCode _legacyInteractKey = KeyCode.E;
        [SerializeField] private InputActionReference _getKey;

        private NetworkObject _networkObject;
        private NetworkInteractable _focused;

        public NetworkInteractable Focused => _focused;
        public bool HasFocus => _focused != null;

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
        }

        /// <summary>Chamado pelo PlayerSetup assim que a câmera do dono é criada.</summary>
        public void SetEyePoint(Transform eyePoint)
        {
            _eyePoint = eyePoint;
        }

        private void Update()
        {
            if (!_networkObject.IsOwner) return;

            UpdateFocus();
            if(_getKey.action.WasPressedThisFrame()) TryInteract();
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
        void OnDrawGizmos()
        {
            if (_eyePoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(_eyePoint.position, _eyePoint.forward * _interactionRange);
                if (_focused != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(_eyePoint.position, _focused.transform.position);
                }
                if(Physics.Raycast(_eyePoint.position, _eyePoint.forward, out var hit, _interactionRange, _interactableMask))
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawSphere(hit.point, 0.05f);
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
            Debug.Log($"[{this}]TryInteract");
            _focused.RequestInteractRpc(_networkObject.OwnerClientId);
        }
    }
}