using System;
using System.IO;
using UnityEngine;

namespace Project.Save
{
    /// <summary>
    /// Persistência simples em JSON dos slots de sala. Cada slot é um arquivo separado
    /// em Application.persistentDataPath, então dá pra fazer backup/copiar manualmente
    /// se precisar durante o desenvolvimento.
    /// </summary>
    public static class SaveSlotManager
    {
        public const int SlotCount = 3;
        private const string FileNameFormat = "room_slot_{0}.json";

        public static int ActiveSlotIndex { get; private set; } = -1;

        public static RoomSaveData[] LoadAllSlots()
        {
            var slots = new RoomSaveData[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                slots[i] = LoadSlot(i);
            return slots;
        }

        public static RoomSaveData LoadSlot(int slotIndex)
        {
            var path = GetPath(slotIndex);

            if (!File.Exists(path))
                return RoomSaveData.CreateEmpty(slotIndex);

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<RoomSaveData>(json);
                data.HasData = true;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSlotManager] Falha ao ler slot {slotIndex}: {e.Message}");
                return RoomSaveData.CreateEmpty(slotIndex);
            }
        }

        public static void SaveSlot(RoomSaveData data)
        {
            data.HasData = true;
            data.LastPlayedAtUtc = DateTime.UtcNow.ToString("O");
            if (string.IsNullOrEmpty(data.CreatedAtUtc))
                data.CreatedAtUtc = data.LastPlayedAtUtc;

            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetPath(data.SlotIndex), json);
        }

        public static void DeleteSlot(int slotIndex)
        {
            var path = GetPath(slotIndex);
            if (File.Exists(path))
                File.Delete(path);
        }

        public static void SetActiveSlot(int slotIndex)
        {
            ActiveSlotIndex = slotIndex;
        }

        private static string GetPath(int slotIndex)
        {
            return Path.Combine(Application.persistentDataPath, string.Format(FileNameFormat, slotIndex));
        }
    }
}
