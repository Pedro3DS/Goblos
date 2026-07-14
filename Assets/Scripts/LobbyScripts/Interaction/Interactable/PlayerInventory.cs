using Project.Interaction;
using Project.Items;
using Unity.Netcode;
using UnityEngine;

namespace Project.Player
{
    // ===================== SINGLEPLAYER =====================
    // Troque NetworkList<InventoryEntry> por List<InventoryEntry> comum, apague
    // INetworkSerializable/IEquatable da struct (não precisa mais) e tire o "Server"
    // dos nomes de método - eles viram chamadas diretas.
    // ==========================================================

    public struct InventoryEntry : INetworkSerializable, System.IEquatable<InventoryEntry>
    {
        public ushort ItemId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ItemId);
        }

        public bool Equals(InventoryEntry other) => ItemId == other.ItemId;
    }

    /// <summary>
    /// "Mochila": guarda itens que não estão nas mãos, limitada por slots totais -
    /// não por quantidade de itens (um item Grande custa mais slots que um Pequeno,
    /// ver ItemDefinition.InventorySlotCost). Guardar/retirar despawna/spawna o
    /// objeto físico do item (via ItemDatabase + ItemDefinition.WorldPrefab).
    /// </summary>
    public class PlayerInventory : NetworkBehaviour
    {
        [SerializeField] private ItemDatabase _itemDatabase;
        [SerializeField] private int _totalSlots = 12;

        private readonly NetworkList<InventoryEntry> _entries = new NetworkList<InventoryEntry>();

        public int TotalSlots => _totalSlots;

        public int UsedSlots
        {
            get
            {
                int total = 0;
                foreach (var entry in _entries)
                {
                    var def = _itemDatabase.GetDefinition(entry.ItemId);
                    if (def != null) total += def.InventorySlotCost;
                }
                return total;
            }
        }

        /// <summary>Servidor apenas. Guarda um item que já está na mão/mundo, e despawna o objeto físico.</summary>
        public bool ServerTryStore(GrabbableItem item)
        {
            if (!IsServer) return false;

            var def = item.Definition;
            int id = _itemDatabase.GetId(def);
            if (id < 0) return false; // item não registrado no database

            if (UsedSlots + def.InventorySlotCost > _totalSlots) return false;

            _entries.Add(new InventoryEntry { ItemId = (ushort)id });
            item.NetworkObject.Despawn(true);
            return true;
        }

        /// <summary>Servidor apenas. Retira um item da mochila e spawna o objeto físico em spawnPoint.</summary>
        public bool ServerTryRetrieve(int entryIndex, Transform spawnPoint, out GrabbableItem spawned)
        {
            spawned = null;
            if (!IsServer) return false;
            if (entryIndex < 0 || entryIndex >= _entries.Count) return false;

            var entry = _entries[entryIndex];
            var def = _itemDatabase.GetDefinition(entry.ItemId);
            if (def == null || def.WorldPrefab == null) return false;

            var instance = Instantiate(def.WorldPrefab, spawnPoint.position, spawnPoint.rotation);
            instance.Spawn();
            spawned = instance.GetComponent<GrabbableItem>();

            _entries.RemoveAt(entryIndex);
            return true;
        }
    }
}
