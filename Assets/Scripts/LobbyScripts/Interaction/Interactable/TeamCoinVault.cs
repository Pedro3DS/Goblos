using Project.Player;
using Unity.Netcode;

namespace Project.Interaction
{
    /// <summary>
    /// Ponto de entrega: interagir com o cofre esvazia o CoinPurse do player e soma
    /// no total do time. É aqui que "no fim juntam tudo" acontece.
    /// </summary>
    public class TeamCoinVault : NetworkInteractable
    {
        private readonly NetworkVariable<int> _teamTotal =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int TeamTotal => _teamTotal.Value;

        public override string InteractionPrompt => "Depositar moedas";

        public override bool CanInteract(ulong interactingClientId) => true;

        protected override void OnInteract(ulong interactingClientId)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(interactingClientId, out var client)) return;
            if (client.PlayerObject == null) return;

            var purse = client.PlayerObject.GetComponent<CoinPurse>();
            if (purse == null) return;

            _teamTotal.Value += purse.ServerDepositAll();
        }
    }
}
