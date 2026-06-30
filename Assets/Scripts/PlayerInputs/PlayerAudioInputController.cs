using UnityEngine;

public class PlayerAudioInputController : MonoBehaviour
{
    public int sampleWindow = 64;
    public AudioClip microphoneClip;
    public AudioSource microphoneSource;

    void Start()
    {
        MicrofoneToAudioClip();
        microphoneSource.clip = microphoneClip;
        microphoneSource.Play();
    }

    public void MicrofoneToAudioClip()
    {
        string microphoneName = Microphone.devices[0];

        microphoneClip = Microphone.Start(microphoneName, true, 20, AudioSettings.outputSampleRate);
    }

    public float GetLoudnessFromMicrofone()
    {
        return GetLoudnessFromAudioClip(Microphone.GetPosition(Microphone.devices[0]), microphoneClip);
    }

    public float GetLoudnessFromAudioClip(int clipPosition, AudioClip clip)
    {
        int startPos = clipPosition - sampleWindow;

        if(startPos < 0) return 0;

        float[] waveData = new float[sampleWindow];

        clip.GetData(waveData, startPos);

        float totalLoudness = 0;

        for(int i = 0; i < sampleWindow; i++) totalLoudness += Mathf.Abs(waveData[i]);

        return totalLoudness / sampleWindow;
    }
}
