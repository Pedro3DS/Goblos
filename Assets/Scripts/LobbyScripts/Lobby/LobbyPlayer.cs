using Project.Connection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Project.Lobby
{
    /// <summary>
    /// Fica no prefab do player. Guarda o estado individual do player dentro do lobby.
    /// Todas as variáveis são escrita-servidor / leitura-todos: o cliente nunca define
    /// sua própria cor ou nome diretamente, evitando cheating trivial.
    /// </summary>
    public class LobbyPlayer : NetworkBehaviour
    {
        public NetworkVariable<FixedString32Bytes> DisplayName = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> ColorIndex = new(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> BedIndex = new(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsLyingDown = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [SerializeField] private PlayerVisualCustomizer _visualCustomizer;

        public override void OnNetworkSpawn()
        {
            ColorIndex.OnValueChanged += HandleColorChanged;

            if (ColorIndex.Value >= 0)
                _visualCustomizer.ApplyColor(ColorIndex.Value);

            if (IsServer)
            {
                ApplyPendingConnectionName();
                LobbyManager.Instance?.RegisterPlayer(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            ColorIndex.OnValueChanged -= HandleColorChanged;
        }

        private void ApplyPendingConnectionName()
        {
            var name = ConnectionManager.Instance != null &&
                       ConnectionManager.Instance.TryGetPendingName(OwnerClientId, out var pendingName)
                ? pendingName
                : $"Player {OwnerClientId}";

            DisplayName.Value = name;
        }

        private void HandleColorChanged(int previousValue, int newValue)
        {
            if (newValue >= 0)
                _visualCustomizer.ApplyColor(newValue);
        }

        /// <summary>
        /// Chamado pelo LobbyManager (servidor) pra mover este player pra um ponto
        /// (cama, sit point, lay point). Precisa ser RPC porque a posição agora é
        /// client-authoritative (ClientNetworkTransform) - o servidor não tem mais
        /// permissão de escrever a posição de um objeto que não é dele. Só o próprio
        /// dono pode mover a si mesmo, e a mudança se propaga sozinha pra todo mundo.
        /// </summary>
        [Rpc(SendTo.Owner)]
        public void TeleportRpc(Vector3 position, Quaternion rotation)
        {
            var controller = GetComponent<CharacterController>();

            // Com CharacterController, setar transform.position direto enquanto o
            // componente está ativo é ignorado/estranho - precisa desligar, mover, religar.
            if (controller != null) controller.enabled = false;

            transform.SetPositionAndRotation(position, rotation);

            if (controller != null) controller.enabled = true;
        }
    }
}
