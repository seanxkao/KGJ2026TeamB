using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class RainbowButtonTint : MonoBehaviour
{
    [Header("速度（色相變化速度）")]
    public float speed = 1f;

    [Header("飽和度 / 亮度")]
    [Range(0f, 1f)] public float saturation = 1f;
    [Range(0f, 1f)] public float value = 1f;

    private Button button;
    private float hue;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void Update()
    {
        // Hue 隨時間循環 (0~1)
        hue += Time.deltaTime * speed;
        if (hue > 1f) hue -= 1f;

        Color rainbow = Color.HSVToRGB(hue, saturation, value);

        var colors = button.colors;
        colors.normalColor = rainbow;
        colors.highlightedColor = rainbow * 1.2f;
        colors.pressedColor = rainbow * 0.8f;
        colors.selectedColor = rainbow;
        colors.disabledColor = Color.gray;

        button.colors = colors;
    }
}
