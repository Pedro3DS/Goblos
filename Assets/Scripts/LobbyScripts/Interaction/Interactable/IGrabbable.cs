using UnityEngine;

namespace Project.Items
{
    /// <summary>
    /// Contrato de "isso pode ser segurado na mão". Separado de IInteractable porque
    /// nem todo interagível é pegável (uma alavanca não vai pra sua mão) e porque
    /// nem todo projeto vai precisar da autoridade de rede do NetworkInteractable
    /// (ver nota de conversão SINGLEPLAYER no topo de GrabbableItem.cs).
    /// </summary>
    public interface IGrabbable
    {
        ItemDefinition Definition { get; }
        bool IsHeld { get; }

        /// <summary>Chamado depois que o item já está a caminho da mão (parented, física desligada).</summary>
        void OnGrabbed(Transform handSocket);

        /// <summary>Chamado quando o item volta pro chão sem impulso (drop).</summary>
        void OnDropped(Vector3 position, Quaternion rotation);

        /// <summary>Chamado quando o item é arremessado, já com o impulso físico aplicado.</summary>
        void OnThrown(Vector3 impulse);
    }
}
