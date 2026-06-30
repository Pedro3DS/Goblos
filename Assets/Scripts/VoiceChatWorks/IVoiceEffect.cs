/// <summary>
/// Contrato para qualquer efeito de processamento de voz (pitch, robô, eco, etc).
/// Implementado como ScriptableObject (veja VoicePitchPreset) para que cada efeito
/// vire um asset configurável no editor, sem precisar recompilar código pra ajustar
/// parâmetros — e pra poder ter vários presets (Normal, Grave, Monstro...) ao mesmo tempo.
/// </summary>
public interface IVoiceEffect
{
    /// <summary>Aplica o efeito sobre o buffer de amostras e retorna o resultado processado.</summary>
    float[] Apply(float[] samples, int sampleRate);
}
