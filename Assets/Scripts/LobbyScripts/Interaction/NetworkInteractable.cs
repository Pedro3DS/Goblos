using Unity.Netcode;
using UnityEngine;

namespace Project.Interaction
{
    /// <summary>
    /// Base para qualquer objeto de cena que precisa reagir a uma interação de forma
    /// autoritativa (o servidor decide se acontece, o cliente só solicita).
    /// Herde disso para criar novas interações (cama, altar, alavancas, etc).
    /// </summary>
    public abstract class NetworkInteractable : NetworkBehaviour, IInteractable
    {
        public abstract string InteractionPrompt { get; }

        public abstract bool CanInteract(ulong interactingClientId);

        /// <summary>Lógica de efeito colateral da interação. Roda SOMENTE no servidor.</summary>
        protected abstract void OnInteract(ulong interactingClientId);

        /// <summary>
        /// Chamado pelo cliente (via PlayerInteractor) quando o jogador aperta o botão de interagir.
        /// O servidor revalida CanInteract antes de aplicar qualquer efeito - nunca confie no cliente.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestInteractRpc(ulong requestingClientId)
        {
            if (!CanInteract(requestingClientId))
            {
                Debug.Log($"[{name}] Interação negada para o client {requestingClientId}.");
                return;
            }

            OnInteract(requestingClientId);
        }
    }
}
