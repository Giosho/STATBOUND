using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>STATBOUND > Setup Stage 2: splits the walls, adds 4 doors and the question systems to the open scene.</summary>
public static class Stage2Setup
{
    private static readonly Color DefaultWallColor = new Color32(0x5A, 0x4E, 0x6E, 0xFF);

    [MenuItem("STATBOUND/Setup Stage 2 (doors + questions)")]
    public static void Run()
    {
        var room = GameObject.Find("Room");
        var player = GameObject.Find("Player");
        if (room == null || player == null || player.GetComponent<PlayerController>() == null)
        {
            EditorUtility.DisplayDialog("STATBOUND",
                "Open the Game scene first. It needs a 'Room' object and a 'Player' object with PlayerController (Stage 1).",
                "OK");
            return;
        }

        var floor = room.transform.Find("Floor");
        var sprite = floor != null && floor.GetComponent<SpriteRenderer>() != null
            ? floor.GetComponent<SpriteRenderer>().sprite
            : null;
        if (sprite == null)
        {
            EditorUtility.DisplayDialog("STATBOUND", "Room/Floor with a sprite was not found.", "OK");
            return;
        }

        // Clean up anything from a previous run, plus the Stage 1 single-piece walls.
        Color wallColor = DefaultWallColor;
        var oldTop = room.transform.Find("Wall_Top");
        if (oldTop != null && oldTop.GetComponent<SpriteRenderer>() != null)
            wallColor = oldTop.GetComponent<SpriteRenderer>().color;

        foreach (string n in new[] { "Wall_Top", "Wall_Bottom", "Wall_Left", "Wall_Right", "Walls", "Doors" })
        {
            var t = room.transform.Find(n);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        foreach (string n in new[] { "GameSystems", "QuestionUI" })
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }

        int wallLayer = LayerMask.NameToLayer("Walls");
        if (wallLayer < 0) wallLayer = 0;

        // Walls: room is 20 x 11 with a 2-unit gap in the middle of each side.
        var walls = new GameObject("Walls").transform;
        walls.SetParent(room.transform, false);
        MakeWall(walls, sprite, wallColor, wallLayer, "Wall_Top_L", new Vector2(-5.5f, 5f), new Vector2(9f, 1f));
        MakeWall(walls, sprite, wallColor, wallLayer, "Wall_Top_R", new Vector2(5.5f, 5f), new Vector2(9f, 1f));
        MakeWall(walls, sprite, wallColor, wallLayer, "Wall_Bottom_L", new Vector2(-5.5f, -5f), new Vector2(9f, 1f));
        MakeWall(walls, sprite, wallColor, wallLayer, "Wall_Bottom_R", new Vector2(5.5f, -5f), new Vector2(9f, 1f));
        MakeWall(walls, sprite, wallColor, wallLayer, "Wall_Left_T", new Vector2(-9.5f, 3.25f), new Vector2(1f, 4.5f));
        MakeWall(walls, sprite, wallColor, wallLayer, "Wall_Left_B", new Vector2(-9.5f, -3.25f), new Vector2(1f, 4.5f));
        MakeWall(walls, sprite, wallColor, wallLayer, "Wall_Right_T", new Vector2(9.5f, 3.25f), new Vector2(1f, 4.5f));
        MakeWall(walls, sprite, wallColor, wallLayer, "Wall_Right_B", new Vector2(9.5f, -3.25f), new Vector2(1f, 4.5f));

        // Doors: index = (int)DoorDirection (North, East, South, West).
        var doorsParent = new GameObject("Doors").transform;
        doorsParent.SetParent(room.transform, false);
        var doors = new DoorController[4];
        doors[(int)DoorDirection.North] = MakeDoor(doorsParent, sprite, DoorDirection.North, new Vector2(0f, 5f), new Vector2(2f, 1f));
        doors[(int)DoorDirection.East] = MakeDoor(doorsParent, sprite, DoorDirection.East, new Vector2(9.5f, 0f), new Vector2(1f, 2f));
        doors[(int)DoorDirection.South] = MakeDoor(doorsParent, sprite, DoorDirection.South, new Vector2(0f, -5f), new Vector2(2f, 1f));
        doors[(int)DoorDirection.West] = MakeDoor(doorsParent, sprite, DoorDirection.West, new Vector2(-9.5f, 0f), new Vector2(1f, 2f));

        // Systems.
        var health = player.GetComponent<HealthSystem>();
        if (health == null) health = player.AddComponent<HealthSystem>();

        var systems = new GameObject("GameSystems");
        var questions = systems.AddComponent<QuestionManager>();
        var roomController = systems.AddComponent<RoomController>();

        var rcSo = new SerializedObject(roomController);
        var doorsProp = rcSo.FindProperty("doors");
        doorsProp.arraySize = 4;
        for (int i = 0; i < 4; i++) doorsProp.GetArrayElementAtIndex(i).objectReferenceValue = doors[i];
        rcSo.FindProperty("questions").objectReferenceValue = questions;
        rcSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
        rcSo.FindProperty("playerHealth").objectReferenceValue = health;
        rcSo.ApplyModifiedPropertiesWithoutUndo();

        var ui = new GameObject("QuestionUI").AddComponent<QuestionPanelUI>();
        var uiSo = new SerializedObject(ui);
        uiSo.FindProperty("questions").objectReferenceValue = questions;
        uiSo.FindProperty("room").objectReferenceValue = roomController;
        uiSo.FindProperty("health").objectReferenceValue = health;
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        // Restart reloads by build index, so the scene must be in Build Settings.
        var scene = EditorSceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(scene.path))
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == scene.path))
            {
                list.Insert(0, new EditorBuildSettingsScene(scene.path, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("STATBOUND: Stage 2 set up. Press Play.");
    }

    private static void MakeWall(Transform parent, Sprite sprite, Color color, int layer, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = layer;
        go.transform.localPosition = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingLayerName = "Walls";

        ApplySize(go.transform, sprite, size);
        go.AddComponent<BoxCollider2D>().size = sprite.bounds.size;
    }

    private static DoorController MakeDoor(Transform parent, Sprite sprite, DoorDirection direction, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Door_" + direction);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Walls";
        sr.sortingOrder = 1;

        ApplySize(go.transform, sprite, size);

        var solid = go.AddComponent<BoxCollider2D>();
        solid.size = sprite.bounds.size;
        var passage = go.AddComponent<BoxCollider2D>();
        passage.size = sprite.bounds.size;
        passage.isTrigger = true;

        var door = go.AddComponent<DoorController>();
        var so = new SerializedObject(door);
        so.FindProperty("direction").enumValueIndex = (int)direction;
        so.ApplyModifiedPropertiesWithoutUndo();
        return door;
    }

    private static void ApplySize(Transform t, Sprite sprite, Vector2 worldSize)
    {
        Vector3 b = sprite.bounds.size;
        t.localScale = new Vector3(worldSize.x / b.x, worldSize.y / b.y, 1f);
    }
}
