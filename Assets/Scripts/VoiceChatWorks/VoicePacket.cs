using System.IO;

/// <summary>
/// Representa um pacote de voz pronto para ser enviado/recebido pela rede.
/// Layout binário: [SenderSteamId:8][Sequence:2][SampleRate:4][PayloadLength:4][Payload:N]
/// </summary>
public struct VoicePacket
{
    public ulong SenderSteamId;
    public ushort Sequence;
    public int SampleRate;
    public byte[] Payload;

    public byte[] Serialize()
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(SenderSteamId);
            writer.Write(Sequence);
            writer.Write(SampleRate);
            writer.Write(Payload.Length);
            writer.Write(Payload);
            return stream.ToArray();
        }
    }

    public static VoicePacket Deserialize(byte[] data)
    {
        using (var stream = new MemoryStream(data))
        using (var reader = new BinaryReader(stream))
        {
            var packet = new VoicePacket
            {
                SenderSteamId = reader.ReadUInt64(),
                Sequence = reader.ReadUInt16(),
                SampleRate = reader.ReadInt32()
            };

            int payloadLength = reader.ReadInt32();
            packet.Payload = reader.ReadBytes(payloadLength);
            return packet;
        }
    }
}
