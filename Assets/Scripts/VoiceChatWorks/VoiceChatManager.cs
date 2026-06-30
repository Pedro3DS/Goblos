using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquestra todo o pipeline de voice chat: captura local -> efeito de pitch ->
/// codificação PCM16 -> envio pela rede (Steam), e recepção -> decodificação ->
/// playback nos jogadores remotos.
///
/// Fluxo: Microfone -> Efeito de pitch -> PCM16 -> Steam (host) -> Steam (broadcast)
/// -> PCM16 -> AudioSource remoto.
/// </summary>
public class VoiceChatManager : MonoBehaviour
{
    [Header("Dependências")]
    public VoiceChatSettings settings;
    public MicVoiceCapture micCapture;

    [Header("Efeito de voz ativo (local)")]
    [Tooltip("Preset de pitch aplicado à SUA voz antes de enviar pra rede. Troque em runtime pra mudar o efeito (ex: personagem disfarçado).")]
    public VoicePitchPreset activePitchPreset;

    [Header("Prefab do jogador remoto")]
    [Tooltip("Precisa ter um RemoteVoicePlayback (e um AudioSource) no componente raiz. Idealmente, anexe ao avatar do jogador remoto em vez de instanciar solto.")]
    public RemoteVoicePlayback remoteVoicePrefab;

    private IVoiceNetworkTransport _transport;
    private ulong _localSteamId;
    private ushort _sequence;
    private readonly Dictionary<ulong, RemoteVoicePlayback> _remotePlayers = new();
    private float _lastSendTime;

    /// <summary>Chame depois de criar o transporte (Steam) e saber o SteamId local.</summary>
    public void Initialize(IVoiceNetworkTransport transport, ulong localSteamId)
    {
        _transport = transport;
        _localSteamId = localSteamId;

        _transport.OnVoicePacketReceived += HandleVoicePacketReceived;
        micCapture.OnSamplesReady += HandleLocalSamplesReady;
    }

    private void OnDestroy()
    {
        if (_transport != null) _transport.OnVoicePacketReceived -= HandleVoicePacketReceived;
        if (micCapture != null) micCapture.OnSamplesReady -= HandleLocalSamplesReady;
    }

    private void HandleLocalSamplesReady(float[] rawSamples)
    {
        // Throttle: nunca manda mais pacotes por segundo do que o configurado, pra não
        // inundar a conexão Steam com mensagens pequenas demais.
        float minInterval = 1f / settings.maxPacketsPerSecond;
        if (Time.unscaledTime - _lastSendTime < minInterval) return;
        _lastSendTime = Time.unscaledTime;

        float[] processed = activePitchPreset != null
            ? activePitchPreset.Apply(rawSamples, settings.sampleRate)
            : rawSamples;

        byte[] encoded = VoiceDsp.EncodeToPCM16(processed);

        var packet = new VoicePacket
        {
            SenderSteamId = _localSteamId,
            Sequence = _sequence++,
            SampleRate = settings.sampleRate,
            Payload = encoded
        };

        if (_transport.IsHost)
            _transport.BroadcastVoice(packet, excludeSteamId: _localSteamId);
        else
            _transport.SendVoiceToHost(packet);
    }

    private void HandleVoicePacketReceived(VoicePacket packet)
    {
        // Se eu sou o host, além de tocar localmente, preciso retransmitir pros outros clientes.
        if (_transport.IsHost)
            _transport.BroadcastVoice(packet, excludeSteamId: packet.SenderSteamId);

        if (packet.SenderSteamId == _localSteamId) return; // nunca toca a própria voz de volta

        RemoteVoicePlayback player = GetOrCreateRemotePlayer(packet.SenderSteamId);
        float[] samples = VoiceDsp.DecodeFromPCM16(packet.Payload, 0, packet.Payload.Length);
        player.EnqueueAudio(samples);
    }

    private RemoteVoicePlayback GetOrCreateRemotePlayer(ulong steamId)
    {
        if (_remotePlayers.TryGetValue(steamId, out var existing)) return existing;

        // Idealmente, em vez de instanciar um objeto solto, pegue o avatar JÁ existente
        // do jogador (pra herdar a posição dele e ter espacialização correta). Ajuste essa
        // busca pro seu sistema de spawn de jogadores.
        RemoteVoicePlayback instance = Instantiate(remoteVoicePrefab);
        instance.Initialize(steamId, settings.sampleRate, settings);
        _remotePlayers[steamId] = instance;
        return instance;
    }
}
