using UnityEngine;

public class RotateFromAudiooClip : MonoBehaviour
{

    public GameObject objectToRotate;

    public float MaxRotate;
    public float MinRotate;
    public PlayerAudioInputController playerAudioInput;

    public float loudnessSensibility = 100f;
    public float threshold = 0.1f;


    // Update is called once per frame
    void Update()
    {
        float loudness = playerAudioInput.GetLoudnessFromMicrofone() * loudnessSensibility;

        if(loudness < threshold) loudness = 0;

        objectToRotate.transform.localEulerAngles = Vector3.Lerp(
            new Vector3(MinRotate,0,-90f),
            new Vector3(MaxRotate,0,-90f),
            loudness);
    }
}
