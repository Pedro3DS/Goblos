using UnityEngine;

/// <summary>
/// Toca em streaming o áudio de voz recebido de UM jogador remoto específico.
/// Cada jogador remoto deve ter sua própria instância disso — idealmente anexada ao
/// próprio avatar dele, pra aproveitar a posição dele no áudio 3D posicional.
///
/// Usa um ring buffer simples: o VoiceChatManager empilha amostras decodificadas com
/// EnqueueAudio(), e o AudioClip "puxa" essas amostras continuamente através do
/// PCMReaderCallback (chamado pela própria Unity no thread de áudio).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class RemoteVoicePlayback : MonoBehaviour
{
    [Tooltip("Tamanho do buffer circular em amostras. Maior = mais tolerante a jitter de rede, porém mais latência.")]
    public int ringBufferSize = 16000; // ~1s a 16khz

    private AudioSource _audioSource;
    private float[] _ringBuffer;
    private int _writePos;
    private int _readPos;
    private int _available; // quantas amostras ainda não foram lidas pelo playback
    private readonly object _lock = new object();

    public ulong OwnerSteamId { get; private set; }

    public void Initialize(ulong ownerSteamId, int sampleRate, VoiceChatSettings settings)
    {
        OwnerSteamId = ownerSteamId;

        _audioSource = GetComponent<AudioSource>();
        _audioSource.loop = true;
        _audioSource.spatialBlend = settings.use3DPositionalAudio ? 1f : 0f;
        _audioSource.maxDistance = settings.maxAudibleDistance;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;

        _ringBuffer = new float[Mathf.Max(ringBufferSize, sampleRate)];

        // AudioClip "infinito" com PCMReaderCallback: a Unity chama OnAudioRead sempre que
        // precisa de mais amostras pra tocar, então não precisamos gerenciar o playback manualmente.
        AudioClip streamClip = AudioClip.Create(
            $"VoiceStream_{ownerSteamId}",
            sampleRate,
            1,
            sampleRate,
            true,
            OnAudioRead);

        _audioSource.clip = streamClip;
        _audioSource.Play();
    }

    /// <summary>Chamado pelo VoiceChatManager quando chega áudio decodificado deste jogador.</summary>
    public void EnqueueAudio(float[] samples)
    {
        lock (_lock)
        {
            foreach (float sample in samples)
            {
                _ringBuffer[_writePos] = sample;
                _writePos = (_writePos + 1) % _ringBuffer.Length;

                if (_available < _ringBuffer.Length) _available++;
                else _readPos = (_readPos + 1) % _ringBuffer.Length; // buffer cheio: descarta a amostra mais antiga
            }
        }
    }

    // Chamado pela Unity no thread de áudio — precisa ser rápido e evitar alocações.
    private void OnAudioRead(float[] data)
    {
        lock (_lock)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (_available <= 0)
                {
                    data[i] = 0f; // sem dados novos ainda -> silêncio, evita tocar lixo
                    continue;
                }

                data[i] = _ringBuffer[_readPos];
                _readPos = (_readPos + 1) % _ringBuffer.Length;
                _available--;
            }
        }
    }
}
