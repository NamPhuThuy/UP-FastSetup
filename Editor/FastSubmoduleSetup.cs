using System;
using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Techies
{
    /// <summary>
    /// FastSubmoduleSetup provides a Unity Editor menu item to add Git submodules
    /// to the project based on a text file.
    /// 
    /// The input text file should contain one submodule entry per line,
    /// formatted as:
    /// 
    /// &lt;path&gt; &lt;url&gt;
    /// 
    /// Example:
    /// Assets/Submodules/MyLibrary https://github.com/user/MyLibrary.git
    /// "Assets/Submodules/My Library With Spaces" https://github.com/user/MyLibrary.git
    /// 
    /// Lines starting with '#' are treated as comments and ignored.
    /// Empty lines are also ignored.
    /// </summary>
    public class FastSubmoduleSetup
    {
        [MenuItem("Assets/UP-FastSetup/Add Submodules from file", true)]
        private static bool ValidateAddSubmodules()
        {
            string path = GetSelectObjectPath();
            return File.Exists(path) && path.EndsWith(".txt");
        }

        [MenuItem("Assets/UP-FastSetup/Add Submodules from file")]
        private static void AddSubmodules()
        {
            string projectPath = Directory.GetCurrentDirectory();
            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            string fullPath = Path.Combine(projectPath, assetPath);

            if (!File.Exists(fullPath))
            {
                UnityEngine.Debug.LogError("File not found: " + fullPath);
                return;
            }

            string[] lines = File.ReadAllLines(fullPath);

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                int lastSpaceIndex = trimmedLine.LastIndexOf(' ');
                if (lastSpaceIndex <= 0)
                {
                    UnityEngine.Debug.LogWarning($"Skipping invalid line: {trimmedLine}");
                    continue;
                }

                string path = trimmedLine.Substring(0, lastSpaceIndex).Trim();
                string url = trimmedLine.Substring(lastSpaceIndex + 1).Trim();

                // Handle quoted paths
                if (path.StartsWith("\"") && path.EndsWith("\""))
                {
                    path = path.Trim('"');
                }

                AddSubmodule(projectPath, path, url);
            }

            EditorUtility.DisplayDialog("Success", "Submodule setup complete. Check the console for details.", "OK");
        }

        private static void AddSubmodule(string projectPath, string relativePath, string url)
        {
            string submodulePath = Path.Combine(projectPath, relativePath);

            if (Directory.Exists(submodulePath) && Directory.GetFiles(submodulePath).Length > 0)
            {
                UnityEngine.Debug.Log($"Submodule path '{relativePath}' already exists and is not empty. Skipping.");
                return;
            }
            
            if (!Directory.Exists(Path.GetDirectoryName(submodulePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(submodulePath));
            }

            // Quote the relative path to handle spaces correctly.
            string gitCommand = $"submodule add {url} \"{relativePath}\"";

            ProcessStartInfo processInfo = new ProcessStartInfo("git", gitCommand)
            {
                WorkingDirectory = projectPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = Process.Start(processInfo);
            process.WaitForExit();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            if (process.ExitCode == 0)
            {
                UnityEngine.Debug.Log($"Successfully added submodule: {relativePath} from {url}\n{output}");
            }
            else
            {
                UnityEngine.Debug.LogError($"Failed to add submodule: {relativePath}\nError: {error}");
            }
        }

        private static string GetSelectObjectPath()
        {
            string projectPath = Directory.GetCurrentDirectory();
            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            string fullPath = Path.Combine(projectPath, assetPath);
            return fullPath;
        }
    }
}
