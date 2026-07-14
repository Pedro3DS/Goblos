using System;
using System.Text;
using UnityEngine;

namespace Project.Connection
{
    /// <summary>
    /// Dados que o cliente manda no handshake de conexão (NetworkConfig.ConnectionData).
    /// Mantenha pequeno - isso trafega antes até de qualquer NetworkObject existir.
    /// Se depois quiser mandar o SteamId aqui também, é só adicionar um campo.
    /// </summary>
    [Serializable]
    public struct ConnectionPayload
    {
        public string PlayerName;

        public byte[] ToBytes()
        {
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
        }

        public static ConnectionPayload FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return default;

            var json = Encoding.UTF8.GetString(bytes);
            return JsonUtility.FromJson<ConnectionPayload>(json);
        }
    }
}
