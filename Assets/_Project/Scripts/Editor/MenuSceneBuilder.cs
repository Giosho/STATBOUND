using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the MainMenu scene next to the Game scene and puts it first in Build Settings.
/// Runs by itself after scripts compile; STATBOUND > Rebuild Main Menu Scene forces it.
/// </summary>
[InitializeOnLoad]
public static class MenuSceneBuilder
{
    static MenuSceneBuilder()
    {
        EditorApplication.delayCall += EnsureMenuScene;
    }

    [MenuItem("STATBOUND/Rebuild Main Menu Scene")]
    public static void Rebuild()
    {
        BuildScene(true);
    }

    private static void EnsureMenuScene()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        BuildScene(false);
    }

    private static void BuildScene(bool force)
    {
        string gamePath = FindScenePath("Game");
        if (gamePath == null)
        {
            if (force) Debug.LogError("STATBOUND: could not find a scene called 'Game'.");
            return;
        }

        string menuPath = Path.GetDirectoryName(gamePath).Replace('\\', '/') + "/MainMenu.unity";

        if (force || !File.Exists(menuPath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UIFactory.Background;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            SceneManager.MoveGameObjectToScene(cameraGo, scene);

            var menuGo = new GameObject("MainMenu");
            menuGo.AddComponent<MainMenu>();
            SceneManager.MoveGameObjectToScene(menuGo, scene);

            EditorSceneManager.SaveScene(scene, menuPath);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log("STATBOUND: created " + menuPath);
        }

        EnsureBuildSettings(menuPath, gamePath);
    }

    private static void EnsureBuildSettings(string menuPath, string gamePath)
    {
        var wanted = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(menuPath, true),
            new EditorBuildSettingsScene(gamePath, true)
        };

        foreach (var s in EditorBuildSettings.scenes)
            if (s.path != menuPath && s.path != gamePath)
                wanted.Add(s);

        var current = EditorBuildSettings.scenes;
        bool same = current.Length == wanted.Count;
        for (int i = 0; same && i < current.Length; i++)
            same = current[i].path == wanted[i].path && current[i].enabled == wanted[i].enabled;

        if (!same) EditorBuildSettings.scenes = wanted.ToArray();
    }

    private static string FindScenePath(string sceneName)
    {
        foreach (string guid in AssetDatabase.FindAssets(sceneName + " t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/_Project") && Path.GetFileNameWithoutExtension(path) == sceneName)
                return path;
        }
        return null;
    }
}
