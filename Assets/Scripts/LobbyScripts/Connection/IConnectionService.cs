using System.Threading.Tasks;

namespace Project.Connection
{
    /// <summary>
    /// Abstrai COMO a conexão é estabelecida. Hoje: Unity Relay (gera código de sala).
    /// Amanhã: implemente SteamConnectionService usando Steam Lobbies + P2P e troque
    /// só a linha que instancia o serviço em ConnectionManager. Nada mais no projeto muda.
    /// </summary>
    public interface IConnectionService
    {
        Task InitializeAsync();

        /// <summary>Cria uma sessão de host e retorna o código que outros players usam para entrar.</summary>
        Task<string> CreateSessionAsync(int maxPlayers);

        /// <summary>Entra em uma sessão existente usando o código informado.</summary>
        Task JoinSessionAsync(string joinCode);

        void Shutdown();
    }
}
