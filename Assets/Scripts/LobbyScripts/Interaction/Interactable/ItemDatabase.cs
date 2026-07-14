using System.Collections.Generic;
using UnityEngine;

namespace Project.Items
{
    /// <summary>
    /// Tabela de lookup ItemDefinition <-> id numérico. Necessária porque não dá pra
    /// mandar uma referência de ScriptableObject direto pela rede - mandamos um índice
    /// (ushort) e cada máquina resolve pro asset local através desse database.
    ///
    /// IMPORTANTE: todos os clientes e o servidor precisam ter o MESMO asset (mesma
    /// ordem de itens na lista), senão o índice aponta pra itens diferentes em cada
    /// máquina. Sugestão: um único asset, referenciado por PlayerInventory.
    ///
    /// Em singleplayer esse arquivo continua útil (evita duplicar lógica de lookup),
    /// mas deixa de ser obrigatório - PlayerInventory pode guardar a referência do
    /// ItemDefinition direto na lista.
    /// </summary>
    [CreateAssetMenu(menuName = "Sucuri/Itens/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> _items = new();

        public int GetId(ItemDefinition definition) => _items.IndexOf(definition);

        public ItemDefinition GetDefinition(int id)
        {
            if (id < 0 || id >= _items.Count) return null;
            return _items[id];
        }
    }
}
