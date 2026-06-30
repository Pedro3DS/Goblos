using UnityEngine;

/// <summary>
/// Funções utilitárias de baixo nível para processamento de áudio (DSP).
/// Não depende de MonoBehaviour, então pode ser testada isoladamente.
/// </summary>
public static class VoiceDsp
{
    /// <summary>
    /// Desloca o pitch de um buffer de áudio através de resampling linear simples.
    /// pitchFactor menor que 1 = voz mais grave/lenta (engrossar).
    /// pitchFactor maior que 1 = voz mais aguda/rápida (afinar).
    ///
    /// Atenção: essa técnica altera pitch E duração juntos (o clássico efeito de
    /// "voice changer" barato/robótico em extremos). Pra preservar a duração original
    /// seria necessário um phase vocoder (ex: portar a lib SoundTouch), o que é bem
    /// mais caro de implementar e provavelmente overkill pra maioria dos jogos indie.
    /// </summary>
    public static float[] ShiftPitch(float[] input, float pitchFactor)
    {
        if (input == null || input.Length == 0) return input;
        if (Mathf.Approximately(pitchFactor, 1f)) return input;

        pitchFactor = Mathf.Clamp(pitchFactor, 0.25f, 4f);
        int outputLength = Mathf.Max(1, Mathf.RoundToInt(input.Length / pitchFactor));
        float[] output = new float[outputLength];

        for (int i = 0; i < outputLength; i++)
        {
            float srcPos = i * pitchFactor;
            int srcIndex = Mathf.FloorToInt(srcPos);
            float frac = srcPos - srcIndex;

            float a = input[Mathf.Clamp(srcIndex, 0, input.Length - 1)];
            float b = input[Mathf.Clamp(srcIndex + 1, 0, input.Length - 1)];

            output[i] = Mathf.Lerp(a, b, frac);
        }

        return output;
    }

    /// <summary>Converte amostras float (-1..1) para PCM16 little-endian — formato padrão e compacto pra enviar em rede.</summary>
    public static byte[] EncodeToPCM16(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)Mathf.Clamp(samples[i] * short.MaxValue, short.MinValue, short.MaxValue);
            bytes[i * 2] = (byte)(value & 0xFF);
            bytes[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        return bytes;
    }

    /// <summary>Converte PCM16 little-endian de volta para amostras float (-1..1).</summary>
    public static float[] DecodeFromPCM16(byte[] bytes, int offset, int length)
    {
        int sampleCount = length / 2;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short value = (short)(bytes[offset + i * 2] | (bytes[offset + i * 2 + 1] << 8));
            samples[i] = value / (float)short.MaxValue;
        }

        return samples;
    }

    /// <summary>Volume médio simplificado de um buffer — usado pro VAD (voice activity detection) e indicadores visuais.</summary>
    public static float CalculateLoudness(float[] samples)
    {
        if (samples == null || samples.Length == 0) return 0f;

        float total = 0f;
        for (int i = 0; i < samples.Length; i++) total += Mathf.Abs(samples[i]);

        return total / samples.Length;
    }
}
