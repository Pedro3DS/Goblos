using Project.Interaction;
using StarterAssets;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Player
{
    /// <summary>
    /// Hub único de inicialização do player quando ele spawna na rede. Roda uma vez
    /// (OnNetworkSpawn) e decide dois caminhos bem diferentes:
    ///
    ///   DONO: cria a câmera FP (FP_Camera.prefab), esconde a própria cabeça da visão,
    ///   e entrega a referência da câmera pra quem precisar dela - FirstPersonController
    ///   (pra copiar o yaw), PlayerInteractor (pra fazer o raycast de interação a partir
    ///   dos olhos) e PlayerHandsController (pra direção de arremesso). Ninguém mais
    ///   precisa saber COMO a câmera foi criada, só recebe a Transform já pronta daqui.
    ///
    ///   NÃO-DONO: desliga tudo que é só local (movimento, input, character controller,
    ///   raycast de interação) porque a posição desses clients vem do
    ///   ClientNetworkTransform do dono.
    ///
    /// Substitui o antigo PlayerCameraRig: mesma responsabilidade de câmera + esconder
    /// renderers, só que agora também faz a "fiação" (wiring) que antes cada script
    /// (Interactor, HandsController) tinha que resolver sozinho, cada um com seu
    /// próprio jeito de achar "pra onde o player tá olhando".
    /// </summary>
    public class PlayerSetup : NetworkBehaviour
    {
        [Header("Câmera")]
        [SerializeField] private CinemachineCamera _fpCameraPrefab;
        [Tooltip("Transform na altura dos olhos. A câmera segue a POSIÇÃO dele (hard lock); a rotação é auto-gerenciada pela câmera (CinemachinePanTilt).")]
        [SerializeField] private Transform _headSocket;

        [Header("Partes escondidas da própria visão (cabeça, nariz, etc)")]
        [SerializeField] private Renderer[] _selfHiddenRenderers;

        [Header("Referências a inicializar (todas no mesmo prefab do player)")]
        [SerializeField] private FirstPersonController _movementController;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private PlayerHandsController _handsController;

        private CinemachineCamera _spawnedCamera;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                SetupOwner();
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

        private void SetupOwner()
        {
            // Com o spawn adiado (ver ConnectionManager), este objeto só é criado
            // DEPOIS que o cliente já está de fato na cena de Lobby - então a câmera
            // já nasce na cena certa, sem risco de ser destruída numa troca de cena.
            _spawnedCamera = Instantiate(_fpCameraPrefab);
            _spawnedCamera.Follow = _headSocket; // Body = Hard Lock to Target usa isso como posição

            // O CinemachineCamera atualiza a PRÓPRIA transform pra refletir a pose
            // calculada a cada frame - então dá pra usar ela direto como "pra onde eu
            // tô olhando", sem precisar achar/expor a Camera física de saída (a que
            // tem o CinemachineBrain) pra cada script que precisa de direção.
            Transform lookReference = _spawnedCamera.transform;

            if (_movementController != null) _movementController.Initialize(lookReference);
            if (_interactor != null) _interactor.SetEyePoint(lookReference);
            if (_handsController != null) _handsController.SetAimTransform(lookReference);

            HideSelfRenderers();
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

            // PlayerInteractor já se auto-desliga verificando IsOwner no Update, mas
            // sem eyePoint setado ele nem chegaria a fazer o raycast - deixamos
            // explícito aqui mesmo assim, pra não depender de detalhe de implementação
            // de outro script.
            if (_interactor != null) _interactor.enabled = false;
        }
    }
}