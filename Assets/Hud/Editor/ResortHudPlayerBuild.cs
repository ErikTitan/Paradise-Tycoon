using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ParadiseTycoon.Hud.Editor
{
    public static class ResortHudPlayerBuild
    {
        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            bool useMain = Array.IndexOf(args,"-hudUseMain") >= 0;
            if (useMain) ResortHudIntegration.IntegrateMain();
            else ResortHudBuilder.Build();
            ResortHudValidation.Validate();
            int index = Array.IndexOf(args,"-hudPlayerPath");
            if (index < 0 || index+1 >= args.Length) throw new ArgumentException("Provide -hudPlayerPath <absolute exe path>");
            string path = args[index+1];
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] {useMain ? "Assets/Scenes/Main.unity" : ResortHudBuilder.DemoPath}, locationPathName = path,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("HUD player build failed: " + report.summary.result);
            Debug.Log("SCRUM102_PLAYER_BUILD_OK " + report.summary.totalSize + " bytes");
        }
    }
}
