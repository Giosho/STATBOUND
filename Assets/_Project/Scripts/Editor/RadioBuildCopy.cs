using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>After a build, copies the project's "Radio" folder next to the game executable.</summary>
public class RadioBuildCopy : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string source = Path.Combine(projectRoot, "Radio");
        string buildDir = Path.GetDirectoryName(report.summary.outputPath);

        if (!Directory.Exists(source) || string.IsNullOrEmpty(buildDir)) return;

        string target = Path.Combine(buildDir, "Radio");
        CopyDirectory(source, target);
        Debug.Log("STATBOUND: copied the Radio folder to " + target);
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);

        foreach (string dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }
}
