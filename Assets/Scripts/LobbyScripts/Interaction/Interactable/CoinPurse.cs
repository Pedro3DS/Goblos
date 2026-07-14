using Unity.Netcode;
using UnityEngine;

namespace Project.Player
{
    // ===================== SINGLEPLAYER =====================
    // "NetworkBehaviour" -> "MonoBehaviour", NetworkVariable<int> -> int comum,
    // e tire o "Server" dos nomes de método (viram chamadas diretas).
    // ==========================================================

    /// <summary>
    /// "Saquinho" de moedas do player. Separado da PlayerInventory (mochila) de
    /// propósito: moeda não ocupa slot, só conta até a capacidade máxima que o
    /// player consegue carregar. Ver TeamCoinVault pra entrega/soma do time.
    /// </summary>
    public class CoinPurse : NetworkBehaviour
    {
        [SerializeField] private int _maxCarryCapacity = 50;

        private readonly NetworkVariable<int> _carriedCoins =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int CarriedCoins => _carriedCoins.Value;
        public int MaxCarryCapacity => _maxCarryCapacity;
        public bool IsFull => _carriedCoins.Value >= _maxCarryCapacity;

        /// <summary>Servidor apenas.</summary>
        public bool ServerTryAddCoins(int amount)
        {
            if (!IsServer) return false;
            if (IsFull) return false;

            _carriedCoins.Value = Mathf.Min(_carriedCoins.Value + amount, _maxCarryCapacity);
            return true;
        }

        /// <summary>
        /// Servidor apenas. Chamado ao entregar as moedas num ponto de depósito
        /// (ver TeamCoinVault). Retorna quanto foi depositado.
        /// </summary>
        public int ServerDepositAll()
        {
            if (!IsServer) return 0;

            int deposited = _carriedCoins.Value;
            _carriedCoins.Value = 0;
            return deposited;
        }
    }
}
