using UnityEngine;
using UnityEngine.UI;

/// <summary>Faint maths symbols drifting upwards behind the main menu.</summary>
public class FloatingNumbers : MonoBehaviour
{
    private static readonly string[] Glyphs =
        { "1", "2", "3", "4", "5", "7", "9", "12", "2,5", "μ", "Me", "Mo", "Σ", "÷", "=", "+" };

    private struct Drifter
    {
        public RectTransform Rect;
        public float Speed;
        public float Sway;
        public float Phase;
        public float BaseX;
    }

    [SerializeField] private int count = 30;

    private Drifter[] drifters;

    private void Start()
    {
        drifters = new Drifter[count];

        for (int i = 0; i < count; i++)
        {
            var rect = UIFactory.Rect("Glyph", transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 120f);

            var text = rect.gameObject.AddComponent<Text>();
            text.font = UIFactory.Font;
            text.text = Glyphs[Random.Range(0, Glyphs.Length)];
            text.fontSize = Random.Range(30, 96);
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.55f, 0.42f, 0.85f, Random.Range(0.05f, 0.14f));
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            float x = Random.Range(-780f, 780f);
            rect.anchoredPosition = new Vector2(x, Random.Range(-460f, 460f));

            drifters[i] = new Drifter
            {
                Rect = rect,
                Speed = Random.Range(14f, 42f),
                Sway = Random.Range(8f, 30f),
                Phase = Random.Range(0f, 6.28f),
                BaseX = x
            };
        }
    }

    private void Update()
    {
        if (drifters == null) return;

        float t = Time.unscaledTime;
        for (int i = 0; i < drifters.Length; i++)
        {
            var d = drifters[i];
            Vector2 p = d.Rect.anchoredPosition;
            p.y += d.Speed * Time.unscaledDeltaTime;
            p.x = d.BaseX + Mathf.Sin(t * 0.5f + d.Phase) * d.Sway;
            if (p.y > 470f) p.y = -470f;
            d.Rect.anchoredPosition = p;
        }
    }
}
