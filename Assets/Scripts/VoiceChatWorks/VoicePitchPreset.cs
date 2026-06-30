using UnityEngine;

/// <summary>
/// Preset de pitch configurável pelo designer no Inspector.
/// Crie um asset por personagem/efeito (Normal, Voz Grave, Voz Aguda, Monstro...)
/// via botão direito > Create > Voice Chat > Pitch Preset, e atribua o preset desejado
/// ao VoiceChatManager — ou troque em runtime se o jogo permitir customizar a voz.
/// </summary>
[CreateAssetMenu(fileName = "VoicePitchPreset", menuName = "Voice Chat/Pitch Preset")]
public class VoicePitchPreset : ScriptableObject, IVoiceEffect
{
    [Header("Fator de pitch")]
    [Tooltip("1 = normal. Menor que 1 engrossa a voz (mais grave/lenta). Maior que 1 afina (mais aguda/rápida).")]
    [Range(0.5f, 2f)]
    public float pitchFactor = 1f;

    public float[] Apply(float[] samples, int sampleRate)
    {
        return VoiceDsp.ShiftPitch(samples, pitchFactor);
    }
}
