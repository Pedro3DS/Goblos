using System;
using System.Collections.Generic;
using Project.Save;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Connection
{
    /// <summary>
    /// Singleton persistente (DontDestroyOnLoad) que centraliza criar/entrar em sala.
    /// A UI do menu só chama StartHost/StartClient e escuta os eventos - não conhece
    /// Multiplayer Services, Netcode ou aprovação de conexão diretamente.
    ///
    /// IMPORTANTE sobre spawn de player: o player NÃO nasce automaticamente na conexão.
    /// A aprovação sempre nega CreatePlayerObject, e o spawn de verdade só acontece
    /// depois que o cliente já está de fato na cena de Lobby (via eventos do
    /// NetworkSceneManager). Isso evita todo o problema de objetos nascendo na cena
    /// de Menu e sendo destruídos/perdendo referência na troca de cena.
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        public static ConnectionManager Instance { get; private set; }

        [SerializeField] private int _maxPlayers = 4;
        [SerializeField] private string _lobbySceneName = "Lobby";
        [Tooltip("Prefab do player. NÃO precisa (e não deve) estar no campo 'Default Player Prefab' do NetworkManager - o spawn é manual.")]
        [SerializeField] private NetworkObject _playerPrefab;

        private IConnectionService _connectionService;
        private readonly Dictionary<ulong, string> _pendingPlayerNames = new();
        private readonly HashSet<ulong> _spawnedClientIds = new();

        public string CurrentJoinCode { get; private set; }

        public event Action<string> OnHostReady;      // string = joinCode
        public event Action OnClientJoined;
        public event Action<string> OnConnectionFailed; // string = motivo

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Troque aqui por SteamConnectionService quando integrar Steamworks.
            _connectionService = new MultiplayerServicesConnectionService();
        }

        private void Start()
        {
            var netManager = NetworkManager.Singleton;

            // Força isso por código: sem ConnectionApproval = true, o ApprovalCheck
            // fica "definido mas nunca chamado" (o próprio Netcode avisa isso no Console),
            // e o spawn manual de player não tem como funcionar.
            netManager.NetworkConfig.ConnectionApproval = true;
            netManager.ConnectionApprovalCallback = ApprovalCheck;

            netManager.OnServerStarted += HandleServerStarted;
            netManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        public async void StartHost(string playerName, int saveSlotIndex)
        {
            try
            {
                await _connectionService.InitializeAsync();

                SaveSlotManager.SetActiveSlot(saveSlotIndex);

                var payload = new ConnectionPayload { PlayerName = playerName };
                NetworkManager.Singleton.NetworkConfig.ConnectionData = payload.ToBytes();

                // CreateSessionAsync já inicia o host sozinho (Multiplayer Services SDK) -
                // não chamamos NetworkManager.Singleton.StartHost() aqui.
                CurrentJoinCode = await _connectionService.CreateSessionAsync(_maxPlayers);

                OnHostReady?.Invoke(CurrentJoinCode);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConnectionManager] Falha ao criar sala: {e.Message}");
                OnConnectionFailed?.Invoke("Não foi possível criar a sala. Tente novamente.");
            }
        }

        public async void StartClient(string joinCode, string playerName)
        {
            try
            {
                await _connectionService.InitializeAsync();

                var payload = new ConnectionPayload { PlayerName = playerName };
                NetworkManager.Singleton.NetworkConfig.ConnectionData = payload.ToBytes();

                // JoinSessionAsync já inicia o client sozinho.
                await _connectionService.JoinSessionAsync(joinCode);

                OnClientJoined?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConnectionManager] Falha ao entrar na sala: {e.Message}");
                OnConnectionFailed?.Invoke("Código inválido ou sala indisponível.");
            }
        }

        private void ApprovalCheck(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            var payload = ConnectionPayload.FromBytes(request.Payload);
            var connectedCount = NetworkManager.Singleton.ConnectedClientsIds.Count;

            if (connectedCount >= _maxPlayers)
            {
                response.Approved = false;
                response.Reason = "Sala cheia.";
                response.Pending = false;
                return;
            }

            _pendingPlayerNames[request.ClientNetworkId] = string.IsNullOrWhiteSpace(payload.PlayerName)
                ? $"Player {request.ClientNetworkId}"
                : payload.PlayerName;

            response.Approved = true;
            response.CreatePlayerObject = false; // spawn manual - ver SpawnPlayerForClient
            response.Pending = false;
        }

        public bool TryGetPendingName(ulong clientId, out string playerName)
        {
            return _pendingPlayerNames.TryGetValue(clientId, out playerName);
        }

        public void LoadLobbyScene()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(_lobbySceneName, LoadSceneMode.Single);
            }
        }

        // ---------- Spawn adiado do player (só depois de já estar na cena de Lobby) ----------

        private void HandleServerStarted()
        {
            if (!NetworkManager.Singleton.IsServer) return;

            var sceneManager = NetworkManager.Singleton.SceneManager;
            sceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
            sceneManager.OnSynchronizeComplete += HandleClientSynchronized;
        }

        /// <summary>
        /// Dispara quando TODOS os clientes que já estavam conectados terminam de
        /// carregar uma cena (ex: a transição Menu -> Lobby feita pelo host).
        /// </summary>
        private void HandleLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (sceneName != _lobbySceneName) return;

            foreach (var clientId in clientsCompleted)
                SpawnPlayerForClient(clientId);
        }

        /// <summary>
        /// Dispara por cliente quando ele termina a sincronização inicial - cobre o
        /// caso de alguém entrar DEPOIS que a sala já está rodando na cena de Lobby.
        /// </summary>
        private void HandleClientSynchronized(ulong clientId)
        {
            if (SceneManager.GetActiveScene().name != _lobbySceneName) return;
            SpawnPlayerForClient(clientId);
        }

        private void SpawnPlayerForClient(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            if (!_spawnedClientIds.Add(clientId)) return; // já spawnado, evita duplicar

            if (_playerPrefab == null)
            {
                Debug.LogError("[ConnectionManager] _playerPrefab não está atribuído no Inspector.");
                return;
            }

            var instance = Instantiate(_playerPrefab);
            instance.SpawnAsPlayerObject(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            _spawnedClientIds.Remove(clientId);
            _pendingPlayerNames.Remove(clientId);
        }

        public void Shutdown()
        {
            NetworkManager.Singleton.Shutdown();
            _connectionService.Shutdown();
            CurrentJoinCode = null;
            _pendingPlayerNames.Clear();
            _spawnedClientIds.Clear();
        }
    }
}
