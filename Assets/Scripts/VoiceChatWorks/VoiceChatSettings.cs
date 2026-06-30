using UnityEngine;

/// <summary>
/// Configurações globais do sistema de voice chat, centralizadas em um ScriptableObject
/// pra você poder ter presets diferentes (ex: teste local vs produção) sem editar código.
/// </summary>
[CreateAssetMenu(fileName = "VoiceChatSettings", menuName = "Voice Chat/Settings")]
public class VoiceChatSettings : ScriptableObject
{
    [Header("Captura")]
    [Tooltip("Taxa de amostragem usada na captura/transmissão. 16000hz já é ótimo pra voz e economiza bastante banda comparado a 44100hz.")]
    public int sampleRate = 16000;

    [Header("Detecção de fala (VAD)")]
    [Tooltip("Se ligado, não envia pacotes quando o jogador está em silêncio — economiza banda.")]
    public bool useVoiceActivation = true;

    [Tooltip("Abaixo desse volume médio o áudio é considerado silêncio e não é enviado pela rede.")]
    public float silenceThreshold = 0.02f;

    [Header("Rede")]
    [Tooltip("Quantos pacotes de voz por segundo são enviados no máximo (throttle), pra não inundar a conexão.")]
    public int maxPacketsPerSecond = 20;

    [Header("Espacialização")]
    public bool use3DPositionalAudio = true;

    [Tooltip("Distância máxima em que a voz de outro jogador ainda é audível.")]
    public float maxAudibleDistance = 25f;
}
