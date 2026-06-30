using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
// using Steamworks;
// using Steamworks.Data;

/// <summary>
/// Implementação de IVoiceNetworkTransport usando Facepunch.Steamworks (SteamNetworkingSockets).
///
/// IMPORTANTE — leia antes de usar:
/// Este script assume que você JÁ TEM um SocketManager (no host) e um ConnectionManager
/// (nos clientes) cuidando da conexão principal do seu jogo, estilo "listen server"/host
/// autoritativo. Em vez de abrir uma conexão P2P separada só pra voz (handshake extra e
/// outro canal exposto), reaproveitamos a MESMA conexão já estabelecida pro gameplay, e
/// usamos um byte de cabeçalho pra multiplexar "isto é um pacote de voz" vs "isto é
/// estado de jogo". Isso também evita o problema que o próprio time da Facepunch relatou
/// usando SteamNetworking (P2P legado) pra voz no Rust: P2P direto entre clientes pode
/// expor IP; relaying tudo através do host evita isso.
///
/// A API exata de SendMessage/OnMessage pode variar entre versões do Facepunch.Steamworks —
/// confira a assinatura na versão instalada no seu projeto e ajuste se necessário.
/// </summary>
public class SteamVoiceNetworkTransport : IVoiceNetworkTransport
{
    // Byte usado pra marcar "isto é um pacote de voz" no início da mensagem, distinguindo
    // de qualquer outra mensagem de gameplay que trafegue pela mesma conexão.
    private const byte VoiceMessageTag = 0x56; // 'V'

    public event Action<VoicePacket> OnVoicePacketReceived;

    public bool IsHost { get; private set; }

    // Lado cliente: conexão única com o host.
    // private Connection _hostConnection;

    // Lado host: todas as conexões de clientes ativas.
    // private IReadOnlyList<Connection> _clientConnections;

    public SteamVoiceNetworkTransport(bool isHost)
    {
        IsHost = isHost;
    }

    /// <summary>Chame quando a conexão com o host for estabelecida (lado cliente).</summary>
    // public void SetHostConnection(Connection connection) => _hostConnection = connection;

    /// <summary>Chame sempre que a lista de clientes conectados mudar (lado host).</summary>
    // public void SetClientConnections(IReadOnlyList<Connection> connections) => _clientConnections = connections;

    public void SendVoiceToHost(VoicePacket packet)
    {
        if (IsHost) return; // host não precisa enviar pra si mesmo

        byte[] payload = BuildWireMessage(packet);
        // _hostConnection.SendMessage(payload, SendType.Unreliable);
    }

    public void BroadcastVoice(VoicePacket packet, ulong excludeSteamId)
    {
        // if (!IsHost || _clientConnections == null) return;

        // byte[] payload = BuildWireMessage(packet);

        // foreach (var connection in _clientConnections)
        // {
        //     // Pra filtrar o excludeSteamId de fato, você precisa mapear Connection -> SteamId
        //     // (geralmente já existe no seu gerenciador de jogadores — ex: via connection.UserData,
        //     // dependendo de como você configurou o SocketManager). Plugue esse mapeamento aqui.
        //     connection.SendMessage(payload, SendType.Unreliable);
        // }
    }

    /// <summary>
    /// Chame este método de dentro do seu callback OnMessage existente do SocketManager/
    /// ConnectionManager, repassando os bytes crus recebidos. Ele identifica se é um
    /// pacote de voz (pelo tag) e dispara o evento; senão, ignora silenciosamente
    /// (deixando seu código de gameplay tratar a mensagem normalmente).
    /// </summary>
    public void HandleIncomingMessage(IntPtr data, int size)
    {
        if (size < 1) return;

        byte[] raw = new byte[size];
        Marshal.Copy(data, raw, 0, size);

        if (raw[0] != VoiceMessageTag) return; // não é pacote de voz

        byte[] packetBytes = new byte[size - 1];
        Array.Copy(raw, 1, packetBytes, 0, size - 1);

        VoicePacket packet = VoicePacket.Deserialize(packetBytes);
        OnVoicePacketReceived?.Invoke(packet);
    }

    private byte[] BuildWireMessage(VoicePacket packet)
    {
        byte[] serialized = packet.Serialize();
        byte[] wireMessage = new byte[serialized.Length + 1];
        wireMessage[0] = VoiceMessageTag;
        Array.Copy(serialized, 0, wireMessage, 1, serialized.Length);
        return wireMessage;
    }
}
