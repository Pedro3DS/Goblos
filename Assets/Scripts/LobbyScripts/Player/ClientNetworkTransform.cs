using Unity.Netcode.Components;
using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// O NetworkTransform padrão do NGO é servidor-autoritativo (o cliente pediria
    /// pro servidor mover e esperaria confirmação - ruim pra responsividade de FPS).
    /// Essa variante deixa o DONO decidir a posição direto, e os outros clientes
    /// só recebem o resultado. Padrão comum para personagens com CharacterController.
    /// Coloque este componente no lugar do NetworkTransform normal, no prefab do player.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
