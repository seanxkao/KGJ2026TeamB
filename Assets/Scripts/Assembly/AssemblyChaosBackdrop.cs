using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 在巨大內側球面上鋪滿世界座標文字（視覺上如同貼在天空盒內層），繁中要指定含 CJK 的 TMP 字型。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyChaosBackdrop : MonoBehaviour
    {
        [Tooltip("繁體中文顯示必備；建議 NotoSansTC-Regular SDF")]
        [SerializeField]
        TMP_FontAsset _fontAsset;

        [SerializeField]
        float _skyRadius = 260f;

        [SerializeField]
        [Tooltip("緯度方向格數（不含兩極可視情況下仍涵蓋大部分天幕）")]
        int _latitudeSegments = 8;

        [SerializeField]
        int _longitudeSegments = 14;

        [SerializeField]
        float _panelWidth = 32.5f;

        [SerializeField]
        float _panelHeight = 25f;

        [SerializeField]
        float _fontSize = 12f;

        [SerializeField]
        float _characterSpacing;

        [SerializeField]
        float _lineSpacing;

        [SerializeField]
        int _linesPerCell = 22;

        [SerializeField]
        string[] _phrases =
        {
            "全部黏起來",
            "黏在一起",
            "組起來",
            "組裝",
            "接起來",
            "連在一起",
            "黏一黏",
            "快黏",
            "組裝時間",
        };

        [SerializeField]
        float _hueCycleSpeed = 0.35f;

        [SerializeField]
        float _flashHz = 4f;

        [SerializeField]
        float _flashHueKick = 0.08f;

        [SerializeField]
        [Range(0f, 1f)]
        float _textSaturation = 0.92f;

        [SerializeField]
        [Range(0f, 1f)]
        float _textValue = 1f;

        [SerializeField]
        float _skyWobbleDegrees = 0.35f;

        [SerializeField]
        float _skyWobbleFreq = 0.18f;

        readonly List<TmpCell> _cells = new List<TmpCell>(512);
        Transform _skyRoot;
        Quaternion _skyRootBaseLocalRot = Quaternion.identity;

        struct TmpCell
        {
            public TextMeshPro Text;
            public float HuePhase;
        }

        void Awake()
        {
            BuildSkyDome();
        }

        void LateUpdate()
        {
            float t = Time.time;
            float flash = Mathf.Floor(t * _flashHz) * _flashHueKick;

            for (var i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                float h = Mathf.Repeat(c.HuePhase + t * _hueCycleSpeed + flash, 1f);
                var col = Color.HSVToRGB(h, _textSaturation, _textValue);
                c.Text.faceColor = (Color32)col;
            }

            if (_skyRoot != null)
            {
                float deg = _skyWobbleDegrees;
                _skyRoot.localRotation = _skyRootBaseLocalRot * Quaternion.Euler(
                    Mathf.Sin(t * _skyWobbleFreq * 1.1f) * deg,
                    Mathf.Sin(t * _skyWobbleFreq + 1.2f) * deg,
                    Mathf.Cos(t * _skyWobbleFreq * 0.9f) * deg * 0.5f);
            }
        }

        void BuildSkyDome()
        {
            var root = new GameObject("ChaosSkyDome").transform;
            root.SetParent(transform, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            _skyRoot = root;
            _skyRootBaseLocalRot = root.localRotation;

            int linesPerCard = ComputeLinesPerCard();
            int latN = ComputeLatitudeSegments(linesPerCard);

            for (var lat = 0; lat < latN; lat++)
            {
                float v = (lat + 0.5f) / latN;
                float phi = Mathf.PI * v - Mathf.PI * 0.5f;
                int lonN = ComputeLongitudeSegments(phi);

                for (var lon = 0; lon < lonN; lon++)
                {
                    float theta = 2f * Mathf.PI * (lon + 0.5f) / lonN;

                    var dir = SphericalToDirection(phi, theta);
                    var pos = dir * _skyRadius;
                    var rot = QuaternionFaceInward(dir);

                    var cell = new GameObject($"Sky_{lat}_{lon}");
                    cell.transform.SetParent(root, false);
                    cell.transform.SetPositionAndRotation(pos, rot);

                    var tmp = cell.AddComponent<TextMeshPro>();
                    ApplyFontAndCjkDefaults(tmp);
                    tmp.text = BuildCellText(lat, lon, linesPerCard);
                    tmp.fontSize = _fontSize;
                    tmp.characterSpacing = _characterSpacing;
                    tmp.lineSpacing = _lineSpacing;
                    tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
                    tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                    tmp.textWrappingMode = TextWrappingModes.Normal;
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    tmp.rectTransform.sizeDelta = new Vector2(_panelWidth, _panelHeight);

                    float phase = Hash01(lat, lon, 0);
                    _cells.Add(new TmpCell { Text = tmp, HuePhase = phase });
                }
            }
        }

        void ApplyFontAndCjkDefaults(TextMeshPro tmp)
        {
            if (_fontAsset != null)
            {
                tmp.font = _fontAsset;
            }
            else
            {
                Debug.LogWarning("[AssemblyChaosBackdrop] Missing TMP font asset. Chinese glyphs will not render unless the assigned TMP font includes CJK.");
            }

            // CJK 斷行依賴 TMP Settings 與字型本身；這裡確保多行中文面板會被建立。
            tmp.textWrappingMode = TextWrappingModes.Normal;
        }

        static Vector3 SphericalToDirection(float phi, float theta)
        {
            float cosPhi = Mathf.Cos(phi);
            return new Vector3(
                cosPhi * Mathf.Cos(theta),
                Mathf.Sin(phi),
                cosPhi * Mathf.Sin(theta)).normalized;
        }

        /// <summary>
        /// TMP 3D 文字的正面法線與 Transform.forward 相反，
        /// 所以要讓球心看到正面可讀字，需要讓 forward 指向外側。
        /// </summary>
        static Quaternion QuaternionFaceInward(Vector3 outwardFromCenter)
        {
            var dir = outwardFromCenter.normalized;
            var up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(dir, up);
        }

        int ComputeLinesPerCard()
        {
            float lineHeight = Mathf.Max(1f, _fontSize * 1.2f + Mathf.Max(0f, _lineSpacing));
            int visibleLines = Mathf.FloorToInt(_panelHeight / lineHeight);
            return Mathf.Clamp(visibleLines, 1, Mathf.Max(1, _linesPerCell));
        }

        int ComputeLatitudeSegments(int linesPerCard)
        {
            float effectiveCardHeight = Mathf.Max(_panelHeight * 0.95f, linesPerCard * (_fontSize * 1.35f));
            int adaptive = Mathf.CeilToInt((Mathf.PI * _skyRadius) / Mathf.Max(24f, effectiveCardHeight * 1.35f));
            return Mathf.Clamp(Mathf.Max(2, Mathf.Max(_latitudeSegments, adaptive)), 2, 18);
        }

        int ComputeLongitudeSegments(float phi)
        {
            float ringScale = Mathf.Max(0.22f, Mathf.Cos(phi));
            float circumference = 2f * Mathf.PI * _skyRadius * ringScale;
            float targetCardWidth = Mathf.Max(18f, _panelWidth * 1.05f);
            int adaptive = Mathf.CeilToInt(circumference / targetCardWidth);
            int minimumFromScene = Mathf.CeilToInt(_longitudeSegments * Mathf.Lerp(0.45f, 1.75f, Mathf.InverseLerp(0.22f, 1f, ringScale)));
            return Mathf.Clamp(Mathf.Max(3, Mathf.Max(adaptive, minimumFromScene)), 3, 36);
        }

        string BuildCellText(int lat, int lon, int linesPerCard)
        {
            if (_phrases == null || _phrases.Length == 0)
                return "全部黏起來";

            var sb = new StringBuilder(linesPerCard * 24);
            for (var i = 0; i < linesPerCard; i++)
            {
                int phraseIndex = (lat + lon * 7 + i * 3) % _phrases.Length;
                var phrase = _phrases[phraseIndex];
                if (string.IsNullOrEmpty(phrase))
                    phrase = "全部黏起來";

                sb.AppendLine(phrase);
            }

            return sb.ToString();
        }

        static float Hash01(int a, int b, int c)
        {
            var value = Mathf.Sin(a * 12.9898f + b * 78.233f + c * 43.7587f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }
    }
}
