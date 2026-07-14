using Project.Connection;
using UnityEngine;

namespace Project.Core
{
    /// <summary>
    /// Coloque este componente num objeto da PRIMEIRA cena do jogo (Boot/Menu).
    /// Ele garante que o ConnectionManager (e outros singletons persistentes)
    /// existam antes de qualquer UI tentar usá-los, e nunca duplica se a cena
    /// de menu for recarregada (ex: usuário volta pro menu depois de sair da sala).
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private ConnectionManager _connectionManagerPrefab;

        private void Awake()
        {
            if (ConnectionManager.Instance == null)
            {
                Instantiate(_connectionManagerPrefab);
            }
        }
    }
}
