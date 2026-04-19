using UnityEngine;

public class AudioScaleController : MonoBehaviour
{
    [Header("Scale 範圍")]
    public Vector3 minScale;
    public Vector3 maxScale = Vector3.one * 2f;
    public float mod;

    [Header("音量靈敏度")]
    public float sensitivity = 100f;

    [Header("平滑")]
    public float smoothSpeed = 10f;

    public AudioSource audioSource;
    private float[] samples = new float[256];
    private float currentVolume;

    void Start()
    {
        minScale = transform.localScale;
        maxScale = minScale * mod;
    }

    void Update()
    {
        float volume = GetVolume();

        // 平滑避免抖動
        currentVolume = Mathf.Lerp(currentVolume, volume, Time.deltaTime * smoothSpeed);

        // 0~1 映射到 scale
        Vector3 targetScale = Vector3.Lerp(minScale, maxScale, currentVolume);

        transform.localScale = targetScale;
    }

    float GetVolume()
    {
        audioSource.GetOutputData(samples, 0);

        float sum = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }

        float rms = Mathf.Sqrt(sum / samples.Length);

        return Mathf.Clamp01(rms * sensitivity);
    }
}
