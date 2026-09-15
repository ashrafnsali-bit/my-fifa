using UnityEditor;
using UnityEngine;
using System.IO;

public class WebGLBuilder
{
    [MenuItem("Build/Build WebGL (Auto Deploy to docs)")]
    [MenuItem("Build/Build WebGL")]
    public static void BuildWebGL()
    {
        PerformBuild(false);
    }

    [MenuItem("Build/Build and Run WebGL (Clean + Auto Deploy)")]
    public static void BuildAndRunWebGL()
    {
        PerformBuild(true);
    }

    private static void PerformBuild(bool autoRun)
    {
        string buildPath = Path.Combine(Application.dataPath, "..", "WebGL-Build");

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = buildPath,
            target = BuildTarget.WebGL,
            options = autoRun ? (BuildOptions.AutoRunPlayer | BuildOptions.CleanBuildCache) : BuildOptions.None
        };

        // WebGL settings
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        PlayerSettings.productName = "FIFA 26";
        PlayerSettings.companyName = "ashrafnsali";

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("<color=green>[WebGLBuilder] WebGL Build SUCCESS: " + buildPath + "</color>");
            SyncBuildOutputs(buildPath);
        }
        else
        {
            Debug.LogError("[WebGLBuilder] WebGL Build FAILED: " + report.summary.result);
        }
    }

    public static void SyncBuildOutputs(string buildPath)
    {
        try
        {
            // 1. Sync to docs/ for GitHub Pages
            string docsPath = Path.Combine(Application.dataPath, "..", "docs");
            if (!Directory.Exists(docsPath)) Directory.CreateDirectory(docsPath);

            if (!Path.GetFullPath(buildPath).TrimEnd('\\', '/').Equals(Path.GetFullPath(docsPath).TrimEnd('\\', '/'), System.StringComparison.OrdinalIgnoreCase))
            {
                CopyDirectory(buildPath, docsPath);
                Debug.Log("<color=green>[WebGLBuilder] Automatically deployed build files to docs/!</color>");
            }

            // 2. Sync to folder if user builds to WebGL-Build or vice versa
            string siblingFolder = Path.Combine(Application.dataPath, "..", "..", "folder");
            if (Directory.Exists(siblingFolder) && !Path.GetFullPath(buildPath).TrimEnd('\\', '/').Equals(Path.GetFullPath(siblingFolder).TrimEnd('\\', '/'), System.StringComparison.OrdinalIgnoreCase))
            {
                CopyDirectory(buildPath, siblingFolder);
                Debug.Log("<color=green>[WebGLBuilder] Synchronized build files to 'folder' directory!</color>");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[WebGLBuilder] Failed to sync build: " + ex.Message);
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

public class WebGLPostprocessor : UnityEditor.Build.IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
    {
        if (report.summary.platform == BuildTarget.WebGL && report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            string outPath = report.summary.outputPath;
            WebGLBuilder.SyncBuildOutputs(outPath);
        }
    }
}
