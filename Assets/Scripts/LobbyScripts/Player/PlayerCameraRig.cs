using StarterAssets;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Player
{
    /// <summary>
    /// Fica no prefab do player. Só o DONO ganha uma câmera (instancia o FP_Camera.prefab
    /// e aponta pro socket da cabeça). Cabeça/nariz somem só da própria visão via
    /// Renderer.enabled = false - isso é local, não sincroniza pela rede, então os
    /// outros clientes continuam vendo sua cabeça normalmente na cópia deles do objeto.
    /// </summary>
    public class PlayerCameraRig : NetworkBehaviour
    {
        [SerializeField] private CinemachineCamera _fpCameraPrefab;
        [Tooltip("Transform na altura dos olhos. A câmera segue a POSIÇÃO dele (hard lock); a rotação é auto-gerenciada pela câmera (CinemachinePanTilt).")]
        [SerializeField] private Transform _headSocket;
        [SerializeField] private FirstPersonController _movementController;

        [Header("Partes escondidas da própria visão (cabeça, nariz, etc)")]
        [SerializeField] private Renderer[] _selfHiddenRenderers;

        private CinemachineCamera _spawnedCamera;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                SetupOwnerCamera();
                HideSelfRenderers();
            }
            else
            {
                DisableRemoteOnlyComponents();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_spawnedCamera != null)
                Destroy(_spawnedCamera.gameObject);
        }

        private void SetupOwnerCamera()
        {
            // Com o spawn adiado (ver ConnectionManager), este objeto só é criado
            // DEPOIS que o cliente já está de fato na cena de Lobby - então a câmera
            // já nasce na cena certa, sem risco de ser destruída numa troca de cena.
            _spawnedCamera = Instantiate(_fpCameraPrefab);
            _spawnedCamera.Follow = _headSocket; // Body = Hard Lock to Target usa isso como posição
            _movementController.Initialize(_spawnedCamera.transform);
        }

        private void HideSelfRenderers()
        {
            if (_selfHiddenRenderers == null) return;

            foreach (var rend in _selfHiddenRenderers)
            {
                if (rend != null) rend.enabled = false;
            }
        }

        private void DisableRemoteOnlyComponents()
        {
            // Cópias remotas não devem processar input nem física de movimento local -
            // a posição delas vem do ClientNetworkTransform do dono.
            if (_movementController != null)
                _movementController.enabled = false;

            var characterController = GetComponent<CharacterController>();
            if (characterController != null)
                characterController.enabled = false;

            var starterInputs = GetComponent<StarterAssetsInputs>();
            if (starterInputs != null) Destroy(starterInputs);

            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null) Destroy(playerInput);
        }
    }
}
