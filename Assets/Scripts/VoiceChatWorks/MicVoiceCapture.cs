using System;
using UnityEngine;

/// <summary>
/// Captura contínua do microfone para o voice chat.
///
/// Diferente do PlayerAudioInputController original (que lia só uma "janela" fixa de
/// amostras a cada frame — ótimo pra um indicador visual, mas insuficiente pra transmitir
/// voz sem buracos), aqui lemos TODAS as amostras novas desde a última leitura, tratando
/// corretamente o caso em que o cursor circular do microfone "dá a volta" no clip.
///
/// Dica de integração: se você ainda quer usar o RotateFromAudiooClip pra um indicador
/// visual de "estou falando", não chame Microphone.Start em outro lugar — a Unity só
/// permite uma gravação ativa por dispositivo. Em vez disso, escute o evento
/// OnLoudnessChanged daqui e plugue no seu script de rotação.
/// </summary>
public class MicVoiceCapture : MonoBehaviour
{
    public VoiceChatSettings settings;

    /// <summary>Disparado a cada novo chunk de áudio capturado (já filtrado pelo VAD, se ativo).</summary>
    public event Action<float[]> OnSamplesReady;

    /// <summary>Disparado todo frame com o volume médio atual — útil pra UI/indicadores visuais.</summary>
    public event Action<float> OnLoudnessChanged;

    private AudioClip _micClip;
    private string _micDevice;
    private int _lastReadPos;

    void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("MicVoiceCapture: nenhum microfone encontrado.");
            return;
        }

        _micDevice = Microphone.devices[0];
        // loop=true, lengthSec=1: a Unity vai sobrescrever o clip continuamente em loop de 1s.
        _micClip = Microphone.Start(_micDevice, true, 1, settings.sampleRate);
        _lastReadPos = 0;
    }

    void Update()
    {
        if (_micClip == null) return;

        int currentPos = Microphone.GetPosition(_micDevice);
        int clipSamples = _micClip.samples;

        int newSampleCount = currentPos - _lastReadPos;
        if (newSampleCount < 0) newSampleCount += clipSamples; // o cursor deu a volta no clip circular
        if (newSampleCount <= 0) return;

        float[] buffer = new float[newSampleCount];

        if (_lastReadPos + newSampleCount <= clipSamples)
        {
            _micClip.GetData(buffer, _lastReadPos);
        }
        else
        {
            // a leitura cruza o fim do clip circular: precisa ler em duas partes
            int firstPartLength = clipSamples - _lastReadPos;
            float[] firstPart = new float[firstPartLength];
            _micClip.GetData(firstPart, _lastReadPos);

            int secondPartLength = newSampleCount - firstPartLength;
            float[] secondPart = new float[secondPartLength];
            _micClip.GetData(secondPart, 0);

            Array.Copy(firstPart, buffer, firstPartLength);
            Array.Copy(secondPart, 0, buffer, firstPartLength, secondPartLength);
        }

        _lastReadPos = currentPos;

        float loudness = VoiceDsp.CalculateLoudness(buffer);
        OnLoudnessChanged?.Invoke(loudness);

        bool isSilence = settings.useVoiceActivation && loudness < settings.silenceThreshold;
        if (!isSilence) OnSamplesReady?.Invoke(buffer);
    }

    void OnDestroy()
    {
        if (_micDevice != null && Microphone.IsRecording(_micDevice))
            Microphone.End(_micDevice);
    }
}
