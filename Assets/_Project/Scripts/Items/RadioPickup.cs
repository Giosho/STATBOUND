using UnityEngine;

/// <summary>The radio item lying in a room. Pixel-art sprite is generated in code.</summary>
public class RadioPickup : MonoBehaviour
{
    private const string Art =
        "..........a....." +
        "...........a...." +
        "............a..." +
        "..oooooooooooo.." +
        ".ollllllllllllo." +
        ".obsssssblddlbo." +
        ".obsososbllllbo." +
        ".obsssssbllllbo." +
        ".obsososbbbbbbo." +
        ".obsssssbdbbdbo." +
        ".obsososbbbbbbo." +
        ".obsssssbbbbbbo." +
        ".obbbbbbbbbbbbo." +
        "..oooooooooooo.." +
        "..o..........o.." +
        "................";

    private static Sprite radioSprite;
    private static Sprite glowSprite;

    private Transform art;
    private Transform glow;
    private SpriteRenderer artRenderer;
    private SpriteRenderer glowRenderer;
    private Collider2D trigger;
    private Vector3 basePosition;
    private float time;
    private float collectedTime = -1f;

    public static GameObject Spawn(Vector2 position)
    {
        var go = new GameObject("RadioPickup");
        go.transform.position = position;
        go.AddComponent<RadioPickup>();
        return go;
    }

    private void Awake()
    {
        var glowGo = new GameObject("Glow");
        glow = glowGo.transform;
        glow.SetParent(transform, false);
        glowRenderer = glowGo.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = GlowSprite();
        glowRenderer.color = new Color(1f, 0.83f, 0.37f, 0.45f);
        glowRenderer.sortingLayerName = "Entities";
        glowRenderer.sortingOrder = 4;

        var artGo = new GameObject("Radio");
        art = artGo.transform;
        art.SetParent(transform, false);
        art.localScale = Vector3.one * 1.4f;
        artRenderer = artGo.AddComponent<SpriteRenderer>();
        artRenderer.sprite = RadioSprite();
        artRenderer.sortingLayerName = "Entities";
        artRenderer.sortingOrder = 5;

        var circle = gameObject.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = 0.8f;
        trigger = circle;

        basePosition = art.localPosition;
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;

        if (collectedTime < 0f)
        {
            art.localPosition = basePosition + Vector3.up * (Mathf.Sin(time * 3f) * 0.12f);
            float pulse = 1f + Mathf.Sin(time * 4f) * 0.12f;
            glow.localScale = Vector3.one * pulse * 1.2f;
            return;
        }

        // Pickup animation: pop up, grow and fade.
        float p = (time - collectedTime) / 0.45f;
        art.localScale = Vector3.one * Mathf.Lerp(1.4f, 2.4f, p);
        art.localPosition = basePosition + Vector3.up * (p * 0.8f);
        artRenderer.color = new Color(1f, 1f, 1f, 1f - p);
        glowRenderer.color = new Color(1f, 0.83f, 0.37f, 0.45f * (1f - p));
        glow.localScale = Vector3.one * Mathf.Lerp(1.2f, 3f, p);

        if (p >= 1f) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collectedTime >= 0f) return;
        if (other.GetComponent<PlayerController>() == null) return;

        collectedTime = time;
        trigger.enabled = false;

        if (RadioController.Instance != null) RadioController.Instance.SetOwned(true);
        AudioManager.Play(Sfx.Pickup);
    }

    private static Sprite RadioSprite()
    {
        if (radioSprite != null) return radioSprite;

        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int row = 0; row < 16; row++)
        {
            for (int col = 0; col < 16; col++)
                tex.SetPixel(col, 15 - row, ColorFor(Art[row * 16 + col]));
        }

        tex.Apply();
        radioSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        return radioSprite;
    }

    private static Color ColorFor(char c)
    {
        switch (c)
        {
            case 'o': return new Color32(0x1A, 0x10, 0x20, 0xFF);
            case 'b': return new Color32(0xC9, 0x82, 0x2B, 0xFF);
            case 'l': return new Color32(0xE8, 0xA8, 0x4A, 0xFF);
            case 's': return new Color32(0x4A, 0x36, 0x55, 0xFF);
            case 'd': return new Color32(0xFF, 0xD4, 0x5E, 0xFF);
            case 'a': return new Color32(0xBF, 0xBF, 0xCF, 0xFF);
            default: return new Color(0f, 0f, 0f, 0f);
        }
    }

    private static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }

        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        return glowSprite;
    }
}
