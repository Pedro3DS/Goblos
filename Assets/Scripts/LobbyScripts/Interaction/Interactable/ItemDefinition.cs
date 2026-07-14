using Unity.Netcode;
using UnityEngine;

namespace Project.Items
{
    public enum ItemType
    {
        Generic,
        Weapon,
        SpellObject,
        Flashlight,
        Coin,
        KeyItem
    }

    public enum HandRequirement
    {
        UmaMao,
        DuasMaos
    }

    public enum ItemSize
    {
        Pequeno,
        Medio,
        Grande
    }

    /// <summary>
    /// Define as regras de um TIPO de item (não é o item em si no mundo - isso é o
    /// GrabbableItem). Um ItemDefinition é compartilhado por todas as instâncias
    /// daquele item (ex: todas as "Espada Enferrujada" apontam pro mesmo asset).
    ///
    /// Não depende de Netcode - funciona igual em projeto singleplayer ou multiplayer.
    /// </summary>
    [CreateAssetMenu(menuName = "Sucuri/Itens/Item Definition", fileName = "ID_NovoItem")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identidade")]
        public string DisplayName = "Item Sem Nome";
        [TextArea] public string Description;
        public Sprite Icon;

        [Header("Classificação")]
        public ItemType Type = ItemType.Generic;
        public HandRequirement HandsRequired = HandRequirement.UmaMao;
        public ItemSize Size = ItemSize.Pequeno;

        [Header("Física / Movimento")]
        [Tooltip("Peso em kg. A soma do peso de tudo na(s) mão(s) reduz a velocidade do player (ver PlayerHandsController).")]
        public float WeightKg = 1f;

        [Header("Arremesso (segurar Q)")]
        public bool IsThrowable = true;
        public float ThrowForceMin = 4f;
        public float ThrowForceMax = 14f;
        [Tooltip("Segundos segurando Q até atingir a força máxima de arremesso.")]
        public float MaxChargeTimeSeconds = 1.2f;

        [Header("Mochila")]
        [Tooltip("Quantos slots da mochila esse item ocupa quando guardado. Sugestão: Pequeno=1, Médio=2, Grande=4.")]
        public int InventorySlotCost = 1;

        [Tooltip("Prefab com NetworkObject + GrabbableItem, usado quando o item é retirado da mochila. " +
                 "Precisa estar registrado nos Network Prefabs do NetworkManager.")]
        public NetworkObject WorldPrefab;
    }
}
