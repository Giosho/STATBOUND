using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// STATBOUND > Apply Textures: generates pixel-art textures (floor, wall, door, player) into
/// Assets/_Project/Textures and puts them on the Game scene objects that only had the flat square.
/// Safe to run more than once.
/// </summary>
public static class TextureSetup
{
    private const string GameScenePath = "Assets/_Project/Scripts/Scenes/Game.unity";
    private const string TextureFolder = "Assets/_Project/Textures";
    private const int Size = 16; // pixels per tile, also pixels per unit

    [MenuItem("STATBOUND/Apply Textures")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != GameScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(GameScenePath);
        }

        var floor = Save("Floor", FloorTexture(), true);
        var wall = Save("Wall", WallTexture(), true);
        var door = Save("Door", DoorTexture(), true);
        var player = Save("Player", PlayerTexture(), false);

        int count = 0;
        var room = GameObject.Find("Room");
        if (room != null)
        {
            foreach (var sr in room.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.GetComponent<DoorController>() != null)
                {
                    // Doors keep their tint: DoorController colors them by state.
                    ApplyTiled(sr, door, false);
                    count++;
                }
                else if (sr.name == "Floor")
                {
                    ApplyTiled(sr, floor, true);
                    count++;
                }
                else if (sr.name.StartsWith("Wall"))
                {
                    ApplyTiled(sr, wall, true);
                    count++;
                }
            }
        }

        var playerGo = GameObject.Find("Player");
        var playerSr = playerGo != null ? playerGo.GetComponent<SpriteRenderer>() : null;
        if (playerSr != null)
        {
            Undo.RecordObject(playerSr, "Apply Textures");
            playerSr.sprite = player;
            playerSr.color = Color.white;
            count++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("STATBOUND: textures applied to " + count + " objects.");
    }

    // Switches a scaled flat square to an unscaled, tiled sprite of the same world size,
    // and resizes its box colliders to match.
    private static void ApplyTiled(SpriteRenderer sr, Sprite sprite, bool clearTint)
    {
        var t = sr.transform;
        Vector2 scale = t.localScale;
        Vector2 worldSize = sr.drawMode == SpriteDrawMode.Simple && sr.sprite != null
            ? Vector2.Scale(sr.sprite.bounds.size, scale)
            : Vector2.Scale(sr.size, scale);

        Undo.RecordObject(t, "Apply Textures");
        Undo.RecordObject(sr, "Apply Textures");

        foreach (var box in sr.GetComponents<BoxCollider2D>())
        {
            Undo.RecordObject(box, "Apply Textures");
            box.size = Vector2.Scale(box.size, scale);
            box.offset = Vector2.Scale(box.offset, scale);
        }

        t.localScale = Vector3.one;
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = worldSize;
        if (clearTint) sr.color = Color.white;
    }

    private static Sprite Save(string name, Texture2D tex, bool tiling)
    {
        if (!AssetDatabase.IsValidFolder(TextureFolder))
            AssetDatabase.CreateFolder("Assets/_Project", "Textures");

        string path = TextureFolder + "/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Size;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = tiling ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        // Tiled draw mode needs a full-rect mesh.
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------- Pixel art ----------

    private static Texture2D NewTexture()
    {
        return new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
    }

    private static Color Shade(Color c, float amount)
    {
        return new Color(Mathf.Clamp01(c.r + amount), Mathf.Clamp01(c.g + amount), Mathf.Clamp01(c.b + amount), c.a);
    }

    private static Color Hex(uint rgb)
    {
        return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);
    }

    /// <summary>Dark stone slabs, 4 per tile, with grout and speckles.</summary>
    private static Texture2D FloorTexture()
    {
        var tex = NewTexture();
        var rng = new System.Random(11);
        Color baseColor = Hex(0x2B2638);

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            bool grout = x % 8 == 0 || y % 8 == 0;
            Color c = grout ? Shade(baseColor, -0.06f) : Shade(baseColor, (float)(rng.NextDouble() - 0.5) * 0.04f);

            // Top-left edge of each slab catches a little light.
            if (!grout && (x % 8 == 1 || y % 8 == 7)) c = Shade(c, 0.025f);
            if (!grout && rng.NextDouble() < 0.05) c = Shade(baseColor, 0.05f);

            tex.SetPixel(x, y, c);
        }

        tex.Apply();
        return tex;
    }

    /// <summary>Offset bricks with mortar lines and a highlight on each brick's top row.</summary>
    private static Texture2D WallTexture()
    {
        var tex = NewTexture();
        var rng = new System.Random(23);
        Color baseColor = Hex(0x5A4E6E);
        Color mortar = Hex(0x2E2639);

        for (int y = 0; y < Size; y++)
        {
            int row = y / 4;
            int shift = row % 2 == 0 ? 0 : 4;
            for (int x = 0; x < Size; x++)
            {
                int bx = (x + shift) % 8;
                Color c;
                if (y % 4 == 0 || bx == 0)
                    c = mortar;
                else
                {
                    // Each brick gets its own slight tone.
                    int brickId = row * 3 + (x + shift) / 8;
                    float tone = ((brickId * 37) % 7 - 3) * 0.012f;
                    c = Shade(baseColor, tone + (float)(rng.NextDouble() - 0.5) * 0.03f);
                    if (y % 4 == 3) c = Shade(c, 0.06f);
                    if (y % 4 == 1) c = Shade(c, -0.04f);
                }
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return tex;
    }

    /// <summary>Light planks with iron bands; kept bright so the door state tint shows through.</summary>
    private static Texture2D DoorTexture()
    {
        var tex = NewTexture();
        var rng = new System.Random(5);

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float v;
            bool band = y == 2 || y == 3 || y == 12 || y == 13;
            if (x == 0 || x == Size - 1 || y == 0 || y == Size - 1) v = 0.55f;
            else if (band) v = (x % 5 == 2) ? 1f : 0.7f; // rivets
            else if (x % 4 == 0) v = 0.72f; // plank seams
            else v = 0.9f + (float)(rng.NextDouble() - 0.5) * 0.1f - (y % 5 == 0 && x % 4 == 2 ? 0.08f : 0f);

            tex.SetPixel(x, y, new Color(v, v, v, 1f));
        }

        tex.Apply();
        return tex;
    }

    /// <summary>Small hooded adventurer facing the camera, in the player's old gold.</summary>
    private static Texture2D PlayerTexture()
    {
        string[] rows =
        {
            "................",
            ".....oooooo.....",
            "....oCCCCCCo....",
            "...oCCCCCCCCo...",
            "...oCCSSSSCCo...",
            "...oCSESSESCo...",
            "...oCSSSSSSCo...",
            "....oCSSSSCo....",
            "...oDCCCCCCDo...",
            "..oSDCCCCCCDSo..",
            "..oSDBBBBBBDSo..",
            "...oDCCCCCCDo...",
            "...oDCCCCCCDo...",
            "....oDDooDDo....",
            "....obbo.obbo...",
            "................",
        };

        var tex = NewTexture();
        for (int r = 0; r < Size; r++)
        {
            int y = Size - 1 - r; // texture rows go bottom-up
            for (int x = 0; x < Size; x++)
            {
                Color c;
                switch (rows[r][x])
                {
                    case 'o': c = Hex(0x1A1424); break;
                    case 'C': c = Hex(0xE8C26A); break;
                    case 'D': c = Hex(0xB8913F); break;
                    case 'S': c = Hex(0xF2C9A0); break;
                    case 'E': c = Hex(0x1A1424); break;
                    case 'B': c = Hex(0x5A3A22); break;
                    case 'b': c = Hex(0x3A2A20); break;
                    default: c = Color.clear; break;
                }
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return tex;
    }
}
