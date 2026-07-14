namespace Project.Interaction
{
    /// <summary>
    /// Contrato para qualquer objeto interagível no mundo (cama, altar, portas, etc).
    /// Implementado por NetworkInteractable para objetos que precisam de autoridade de servidor.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Texto exibido no HUD quando o player mira no objeto (ex: "Deitar", "Chamar amigos").</summary>
        string InteractionPrompt { get; }

        /// <summary>Validação de quem pode interagir. Sempre reavaliada no servidor antes de executar.</summary>
        bool CanInteract(ulong interactingClientId);
    }
}
