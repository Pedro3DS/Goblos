using System;
using Unity.Collections;
using Unity.Netcode;

namespace Project.Lobby
{
    /// <summary>
    /// Estado sincronizado de UMA cama do lobby. Fica dentro de um NetworkList no LobbyManager,
    /// então todo cliente recebe automaticamente qualquer mudança (cor, nome, ocupante, deitado).
    /// </summary>
    public struct BedState : INetworkSerializable, IEquatable<BedState>
    {
        public ulong OccupantClientId; // ulong.MaxValue = cama livre
        public int ColorIndex;         // -1 = sem cor atribuída
        public FixedString32Bytes PlayerName;
        public bool IsLyingDown;

        public static BedState Empty => new BedState
        {
            OccupantClientId = ulong.MaxValue,
            ColorIndex = -1,
            PlayerName = default,
            IsLyingDown = false
        };

        public bool IsOccupied => OccupantClientId != ulong.MaxValue;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref OccupantClientId);
            serializer.SerializeValue(ref ColorIndex);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref IsLyingDown);
        }

        public bool Equals(BedState other)
        {
            return OccupantClientId == other.OccupantClientId
                && ColorIndex == other.ColorIndex
                && PlayerName.Equals(other.PlayerName)
                && IsLyingDown == other.IsLyingDown;
        }

        public override bool Equals(object obj) => obj is BedState other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(OccupantClientId, ColorIndex, PlayerName, IsLyingDown);
    }
}
