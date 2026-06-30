using System;

/// <summary>
/// Abstrai o transporte de rede usado para os pacotes de voz. Isso permite trocar
/// Steamworks.NET, Facepunch.Steamworks, ou até outro provedor de rede no futuro,
/// sem tocar no resto do pipeline de áudio (captura, efeitos, playback).
/// </summary>
public interface IVoiceNetworkTransport
{
    /// <summary>Disparado quando um pacote de voz chega de outro jogador.</summary>
    event Action<VoicePacket> OnVoicePacketReceived;

    /// <summary>True quando este peer é o host/servidor.</summary>
    bool IsHost { get; }

    /// <summary>Cliente -> Host: envia minha voz para o host retransmitir.</summary>
    void SendVoiceToHost(VoicePacket packet);

    /// <summary>Host -> Clientes: retransmite a voz recebida para todos, exceto quem enviou.</summary>
    void BroadcastVoice(VoicePacket packet, ulong excludeSteamId);
}
