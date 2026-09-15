using UnityEditor;
using UnityEngine;
using System.IO;

public class WebGLBuilder
{
    [MenuItem("Build/Build WebGL (Auto Deploy to docs)")]
    [MenuItem("Build/Build WebGL")]
    public static void BuildWebGL()
    {
        string buildPath = Path.Combine(Application.dataPath, "..", "WebGL-Build");

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = buildPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        // WebGL settings
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        PlayerSettings.productName = "FIFA 26";
        PlayerSettings.companyName = "ashrafnsali";

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("WebGL Build SUCCESS: " + buildPath);
            AutoDeployToDocs(buildPath);
        }
        else
        {
            Debug.LogError("WebGL Build FAILED: " + report.summary.result);
        }
    }

    private static void AutoDeployToDocs(string buildPath)
    {
        try
        {
            string docsPath = Path.Combine(Application.dataPath, "..", "docs");
            if (!Directory.Exists(docsPath)) Directory.CreateDirectory(docsPath);

            CopyDirectory(buildPath, docsPath);
            Debug.Log("<color=green>[WebGLBuilder] Automatically deployed build files to docs/!</color>");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[WebGLBuilder] Failed to copy to docs: " + ex.Message);
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) return;

        DirectoryInfo[] dirs = dir.GetDirectories();
        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (DirectoryInfo subDir in dirs)
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    public static void BuildWebGLCLI()
    {
        BuildWebGL();
    }
}
