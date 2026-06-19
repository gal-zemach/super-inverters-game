using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EditorTools
{
	public static class WebGLBuildPipeline
	{
		private const string OutputPath = "Web build";

		public static void BuildWebGL()
		{
			var scenes = EditorBuildSettings.scenes;
			var enabled = new System.Collections.Generic.List<string>();
			foreach (var scene in scenes)
			{
				if (scene.enabled && !string.IsNullOrEmpty(scene.path))
					enabled.Add(scene.path);
			}

			if (enabled.Count == 0)
			{
				Debug.LogError("WebGL build aborted: no enabled scenes in Build Settings.");
				EditorApplication.Exit(1);
				return;
			}

			var options = new BuildPlayerOptions
			{
				scenes = enabled.ToArray(),
				locationPathName = OutputPath,
				target = BuildTarget.WebGL,
				options = BuildOptions.CleanBuildCache
			};

			BuildReport report = BuildPipeline.BuildPlayer(options);
			var summary = report.summary;
			if (summary.result != BuildResult.Succeeded)
			{
				Debug.LogError($"WebGL build failed: {summary.result}");
				EditorApplication.Exit(1);
				return;
			}

			Debug.Log($"WebGL build succeeded → {OutputPath} ({summary.totalSize} bytes)");
			EditorApplication.Exit(0);
		}
	}
}
