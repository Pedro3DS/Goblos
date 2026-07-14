using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace Project.Connection
{
    /// <summary>
    /// Backend de conexão usando o Multiplayer Services SDK (com.unity.services.multiplayer),
    /// que substitui a chamada direta ao com.unity.services.relay. A sessão já cuida de
    /// criar a alocação de Relay, configurar o transporte e iniciar o NetworkManager
    /// (host ou client) sozinha - por isso NÃO chamamos NetworkManager.Singleton.StartHost()
    /// nem StartClient() manualmente em nenhum lugar do ConnectionManager.
    /// </summary>
    public class MultiplayerServicesConnectionService : IConnectionService
    {
        private ISession _session;

        public async Task InitializeAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public async Task<string> CreateSessionAsync(int maxPlayers)
        {
            var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();

            var hostSession = await MultiplayerService.Instance.CreateSessionAsync(options);
            _session = hostSession;

            return hostSession.Code; // código que os outros players usam pra entrar
        }

        public async Task JoinSessionAsync(string joinCode)
        {
            _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
        }

        public async void Shutdown()
        {
            if (_session == null) return;

            await _session.LeaveAsync();
            _session = null;
        }
    }
}
