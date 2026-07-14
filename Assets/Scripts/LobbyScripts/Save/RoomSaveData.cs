using System;
using System.Collections.Generic;

namespace Project.Save
{
    /// <summary>
    /// Dados persistidos de UM slot de sala/mapa. Expanda os campos conforme sua
    /// geração de mapa evoluir (seed, posições de props colocados, progressão, etc).
    /// JsonUtility exige que a classe seja [Serializable] e sem propriedades complexas.
    /// </summary>
    [Serializable]
    public class RoomSaveData
    {
        public int SlotIndex;
        public string RoomName = "Nova Sala";
        public string CreatedAtUtc;
        public string LastPlayedAtUtc;

        // Exemplo de dados de progressão/mapa - ajuste para o seu formato real.
        public string MapSeed;
        public List<string> UnlockedItems = new();

        public bool HasData;

        public static RoomSaveData CreateEmpty(int slotIndex)
        {
            return new RoomSaveData
            {
                SlotIndex = slotIndex,
                HasData = false
            };
        }
    }
}
