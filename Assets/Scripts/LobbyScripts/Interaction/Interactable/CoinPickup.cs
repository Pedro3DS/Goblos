using Project.Player;
using Unity.Netcode;
using UnityEngine;

namespace Project.Interaction
{
    // ===================== SINGLEPLAYER =====================
    // Sem Netcode: vira "MonoBehaviour : IInteractable" comum, e a chamada pro
    // CoinPurse.ServerTryAddCoins vira um método público normal (sem "Server" no nome).
    // ==========================================================

    /// <summary>
    /// Moeda: não ocupa mão nem slot de mochila, só soma direto no saquinho do player
    /// que pegou (ver CoinPurse). Ideal pra "pegar e já era", sem passo intermediário.
    /// </summary>
    public class CoinPickup : NetworkInteractable
    {
        [SerializeField] private int _value = 1;

        public override string InteractionPrompt => "Pegar moeda";

        public override bool CanInteract(ulong interactingClientId) => true;

        protected override void OnInteract(ulong interactingClientId)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(interactingClientId, out var client)) return;
            if (client.PlayerObject == null) return;

            var purse = client.PlayerObject.GetComponent<CoinPurse>();
            if (purse == null) return;

            if (purse.ServerTryAddCoins(_value))
            {
                NetworkObject.Despawn();
            }
            // se o saquinho já tá cheio, a moeda continua no chão em vez de desaparecer.
        }
    }
}
