using UnityEngine;

namespace Project.Interaction
{
    /// <summary>
    /// Placeholder do altar. Por enquanto só loga a interação.
    /// Quando integrar Steamworks.NET (ou Facepunch.Steamworks), troque o corpo de OnInteract
    /// para abrir o overlay de convite, ex:
    ///   SteamFriends.ActivateGameOverlayInviteDialog(currentLobbyId);
    /// Nada no resto do sistema (LobbyManager, PlayerInteractor) precisa mudar.
    /// </summary>
    public class AltarInteractable : NetworkInteractable
    {
        public override string InteractionPrompt => "Chamar amigos (Steam)";

        public override bool CanInteract(ulong interactingClientId) => true;

        protected override void OnInteract(ulong interactingClientId)
        {
            Debug.Log($"[AltarInteractable] Client {interactingClientId} ativou o altar. " +
                      "TODO: integrar convite de amigos via Steam SDK aqui.");
        }
    }
}
