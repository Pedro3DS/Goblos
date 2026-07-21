using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Project.Lobby
{
    /// <summary>
    /// Autoridade única sobre o estado do lobby: quem ocupa qual cama, com qual cor,
    /// e se está deitado ou não. Toda a lógica de atribuição roda no servidor;
    /// os clientes só espelham o NetworkList<BedState> e atualizam a visual local.
    ///
    /// Coloque este componente em um objeto de cena (com NetworkObject) já presente
    /// na cena de Lobby, e arraste as 4 LobbyBedSpawnPoint na ordem desejada.
    /// </summary>
    public class LobbyManager : NetworkBehaviour
    {
        public const int MaxPlayers = 4;

        public static LobbyManager Instance { get; private set; }

        [SerializeField] private LobbyBedSpawnPoint[] _bedSpawnPoints = new LobbyBedSpawnPoint[MaxPlayers];
        [SerializeField] private LobbyPlayerColorPalette _colorPalette;
        [SerializeField] private GameObject PlayerPrefab;

        private NetworkList<BedState> _beds;

        public IReadOnlyList<BedState> Beds => (IReadOnlyList<BedState>)_beds;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            _beds = new NetworkList<BedState>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _beds.Count == 0)
            {
                for (int i = 0; i < MaxPlayers; i++)
                    _beds.Add(BedState.Empty);
            }

            // _beds.OnListChanged += HandleBedsChanged;
            // RefreshAllBedVisuals();

            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            _beds.OnListChanged -= HandleBedsChanged;

            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        // ---------- Atribuição de camas ----------

        /// <summary>
        /// Chamado pelo próprio LobbyPlayer no seu OnNetworkSpawn (lado servidor).
        /// Nesse ponto o NetworkObject do player JÁ existe e já está totalmente
        /// spawnado - não precisa de coroutine, polling ou timeout esperando nada.
        /// </summary>
        public void RegisterPlayer(LobbyPlayer player)
        {
            if (!IsServer) return;

            ulong clientId = player.OwnerClientId;

            int freeIndex = FindFreeBedIndex();
            if (freeIndex == -1)
            {
                Debug.LogWarning($"[LobbyManager] Sem camas livres para o client {clientId}. " +
                                  $"Isso não deveria acontecer se a aprovação de conexão limitar a {MaxPlayers} players.");
                return;
            }

            var colorIndex = freeIndex % Mathf.Max(1, _colorPalette.Count);

            _beds[freeIndex] = new BedState
            {
                OccupantClientId = clientId,
                ColorIndex = colorIndex,
                PlayerName = player.DisplayName.Value, // já setado antes de chamar RegisterPlayer
                IsLyingDown = false
            };

            player.BedIndex.Value = freeIndex;
            player.ColorIndex.Value = colorIndex;

            TeleportPlayerToBed(player, freeIndex, toLayPoint: false);
        }

        private int FindFreeBedIndex()
        {
            for (int i = 0; i < _beds.Count; i++)
            {
                if (!_beds[i].IsOccupied) return i;
            }
            return -1;
        }

        private void TeleportPlayerToBed(LobbyPlayer player, int bedIndex, bool toLayPoint)
        {
            var bed = _bedSpawnPoints[bedIndex];
            if (bed == null) return;

            var target = toLayPoint ? bed.LayPoint : bed.SitPoint;
            if (target == null) return;

            // Com CharacterController, setar transform.position direto enquanto o
            // componente está ativo é ignorado/estranho - precisa desligar, mover, religar.
            // var controller = player.GetComponent<CharacterController>();
            // if (controller != null) controller.enabled = false;

            player.TeleportRpc(target.position, target.rotation);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            for (int i = 0; i < _beds.Count; i++)
            {
                if (_beds[i].OccupantClientId == clientId)
                {
                    _beds[i] = BedState.Empty;
                    break;
                }
            }
        }

        // ---------- Ações do jogador ----------

        public bool IsBedOwner(int bedIndex, ulong clientId)
        {
            if (bedIndex < 0 || bedIndex >= _beds.Count) return false;
            return _beds[bedIndex].OccupantClientId == clientId;
        }

        public void ToggleLyingDown(ulong clientId)
        {
            if (!IsServer) return;

            for (int i = 0; i < _beds.Count; i++)
            {
                if (_beds[i].OccupantClientId != clientId) continue;

                var state = _beds[i];
                state.IsLyingDown = !state.IsLyingDown;
                _beds[i] = state;

                if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
                {
                    var lobbyPlayer = client.PlayerObject.GetComponent<LobbyPlayer>();
                    if (lobbyPlayer != null)
                    {
                        TeleportPlayerToBed(lobbyPlayer, i, toLayPoint: state.IsLyingDown);
                        lobbyPlayer.IsLyingDown.Value = state.IsLyingDown;
                    }
                }

                break;
            }
        }

        public void UpdatePlayerName(ulong clientId, string playerName)
        {
            if (!IsServer) return;

            for (int i = 0; i < _beds.Count; i++)
            {
                if (_beds[i].OccupantClientId != clientId) continue;

                var state = _beds[i];
                state.PlayerName = playerName;
                _beds[i] = state;
                break;
            }
        }

        public int GetBedIndexForClient(ulong clientId)
        {
            for (int i = 0; i < _beds.Count; i++)
            {
                if (_beds[i].OccupantClientId == clientId) return i;
            }
            return -1;
        }

        // ---------- Visual (roda em todo mundo, inclusive host) ----------

        private void HandleBedsChanged(NetworkListEvent<BedState> changeEvent)
        {
            RefreshAllBedVisuals();
        }

        private void RefreshAllBedVisuals()
        {
            if (_bedSpawnPoints == null) return;

            for (int i = 0; i < _bedSpawnPoints.Length && i < _beds.Count; i++)
            {
                var bedVisual = _bedSpawnPoints[i];
                if (bedVisual == null) continue;

                var state = _beds[i];

                if (!state.IsOccupied)
                {
                    bedVisual.ResetBed();
                }
                else
                {
                    bedVisual.ApplyPlayerData(state.PlayerName.ToString(), _colorPalette.GetColor(state.ColorIndex));
                }
            }
        }
    }
}
