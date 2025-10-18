using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Techies
{
    public class FastProjectStructureSetup
    {
        #region Menu Items

        [MenuItem("Assets/FastSetup/Generate Structure from file", true)]
        private static bool ValidateGenerateStructure()
        {
            string path = GetSelectObjectPath();
            return File.Exists(path) && path.EndsWith(".txt");
        }

        [MenuItem("Assets/FastSetup/Generate Structure from file")]
        private static void GenerateStructure()
        {
            string projectPath = Directory.GetCurrentDirectory();
            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            string fullPath = Path.Combine(projectPath, assetPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogError("File not found: " + fullPath);
                return;
            }

            // Read and parse the file
            string[] lines = File.ReadAllLines(fullPath);
            var items = ParseStructureFile(lines);

            if (items.Count == 0)
            {
                EditorUtility.DisplayDialog("No Items", "No valid paths found in the file.", "OK");
                return;
            }

            // Separate scripts and folders
            var scripts = items.Where(x => x.path.EndsWith(".cs")).ToList();
            var folders = items.Where(x => !x.path.EndsWith(".cs")).Select(x => x.path).ToList();

            // Get available templates
            string[] templatePaths = GetScriptTemplates();
            if (scripts.Count > 0 && templatePaths.Length == 0)
            {
                EditorUtility.DisplayDialog("No Templates Found",
                    "No script templates found. Scripts will not be created.",
                    "OK");
                return;
            }

            // Create folders first
            int foldersCreated = 0;
            foreach (string folderPath in folders)
            {
                if (CreateFolder(folderPath, projectPath))
                    foldersCreated++;
            }

            // Create scripts with their respective templates
            int scriptsCreated = 0;
            foreach (var (scriptPath, templateSuffix) in scripts)
            {
                string templateContent = GetTemplateContent(templatePaths, templateSuffix);
                if (templateContent != null)
                {
                    if (CreateScriptFromTemplate(scriptPath, templateContent, projectPath))
                        scriptsCreated++;
                }
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success",
                $"Created:\n- {foldersCreated} folder(s)\n- {scriptsCreated} script(s)",
                "OK");
        }

        #endregion

        #region File Parsing

        private static List<(string path, string templateSuffix)> ParseStructureFile(string[] lines)
        {
            var result = new List<(string path, string templateSuffix)>();
            var pathStack = new Stack<string>();

            foreach (string line in lines)
            {
                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    continue;

                // Calculate indent level (spaces or tabs)
                int currentIndentLevel = GetIndentLevel(line);
                string itemName = line.Trim().TrimStart('-').Trim();

                if (string.IsNullOrEmpty(itemName))
                    continue;

                // Pop stack when going back to previous or same level
                while (pathStack.Count > currentIndentLevel)
                {
                    pathStack.Pop();
                }

                // Extract template suffix if present (format: "ScriptName.cs @TemplateSuffix")
                string templateSuffix = null;
                int suffixIndex = itemName.IndexOf(" @");
                if (suffixIndex > 0)
                {
                    templateSuffix = itemName.Substring(suffixIndex + 2).Trim();
                    itemName = itemName.Substring(0, suffixIndex).Trim();
                }

                // Build full path
                string fullPath = pathStack.Count > 0
                    ? string.Join("/", pathStack.Reverse()) + "/" + itemName
                    : itemName;

                result.Add((fullPath, templateSuffix));

                // Add to stack if it's a folder (not ending with .cs)
                if (!itemName.EndsWith(".cs"))
                {
                    pathStack.Push(itemName);
                }
            }

            return result;
        }

        private static int GetIndentLevel(string line)
        {
            int spaces = 0;
            foreach (char c in line)
            {
                if (c == ' ')
                    spaces++;
                else if (c == '\t')
                    spaces += 4; // Treat tab as 4 spaces
                else
                    break;
            }
    
            // Each indent level is 4 spaces (standard indentation)
            return spaces / 4;
        }

        #endregion

        #region Template Management

        private static string[] GetScriptTemplates()
        {
            var templates = new List<string>();

            // Check Assets/ScriptTemplates
            string projectPath = Directory.GetCurrentDirectory();
            string customPath = Path.Combine(projectPath, "Assets", "ScriptTemplates");
            if (Directory.Exists(customPath))
            {
                templates.AddRange(Directory.GetFiles(customPath, "*.txt", SearchOption.AllDirectories));
            }

            // Check Unity's default templates
            string editorPath = EditorApplication.applicationContentsPath;
            string unityTemplatesPath = Path.Combine(editorPath, "Resources", "ScriptTemplates");
            if (Directory.Exists(unityTemplatesPath))
            {
                templates.AddRange(Directory.GetFiles(unityTemplatesPath, "*.txt", SearchOption.TopDirectoryOnly));
            }

            return templates.ToArray();
        }

        private static int ShowTemplateSelectionDialog(string[] templateNames)
        {
            var window = ScriptableObject.CreateInstance<TemplateSelectionWindow>();
            window.templateNames = templateNames;
            window.ShowModal();
            return window.selectedIndex;
        }

        #endregion

        #region Folder Creation

        private static bool CreateFolder(string folderPath, string projectPath)
        {
            try
            {
                // Normalize path
                folderPath = folderPath.Replace("\\", "/");

                // Ensure it starts with Assets/
                if (!folderPath.StartsWith("Assets/"))
                    folderPath = "Assets/" + folderPath;

                string fullPath = Path.Combine(projectPath, folderPath);

                if (Directory.Exists(fullPath))
                {
                    Debug.Log($"Folder already exists: {folderPath}");
                    return false;
                }

                Directory.CreateDirectory(fullPath);
                Debug.Log($"Created folder: {folderPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create folder {folderPath}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Script Generation

        private static bool CreateScriptFromTemplate(string scriptPath, string templateContent, string projectPath)
        {
            try
            {
                // Normalize path separators
                scriptPath = scriptPath.Replace("\\", "/");

                // Ensure it starts with Assets/
                if (!scriptPath.StartsWith("Assets/"))
                    scriptPath = "Assets/" + scriptPath;

                // Ensure it ends with .cs
                if (!scriptPath.EndsWith(".cs"))
                    scriptPath += ".cs";

                string fullPath = Path.Combine(projectPath, scriptPath);
                string directory = Path.GetDirectoryName(fullPath);
                string fileName = Path.GetFileNameWithoutExtension(fullPath);

                // Create directory if it doesn't exist
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Check if file already exists
                if (File.Exists(fullPath))
                {
                    Debug.LogWarning($"Script already exists: {scriptPath}");
                    return false;
                }

                // Replace template placeholders
                string scriptContent = templateContent;
                scriptContent = scriptContent.Replace("#SCRIPTNAME#", fileName);
                scriptContent = scriptContent.Replace("#NAME#", fileName);
                scriptContent = scriptContent.Replace("#NOTRIM#", "");

                // Replace namespace if needed (extract from path)
                string namespaceName = ExtractNamespaceFromPath(scriptPath);
                scriptContent = scriptContent.Replace("#NAMESPACE#", namespaceName);

                // Write file
                File.WriteAllText(fullPath, scriptContent);
                Debug.Log($"Created script: {scriptPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create script {scriptPath}: {ex.Message}");
                return false;
            }
        }

        private static string ExtractNamespaceFromPath(string path)
        {
            // Extract namespace from path (e.g., Assets/_Project/Scripts/UI -> Project.Scripts.UI)
            var parts = path.Split('/').Skip(1).Take(path.Split('/').Length - 2);
            return string.Join(".", parts.Where(p => !string.IsNullOrEmpty(p)));
        }

        #endregion

        #region Utilities
        
        private static string GetTemplateContent(string[] templatePaths, string templateSuffix)
        {
            // If no suffix provided, use default template
            if (string.IsNullOrEmpty(templateSuffix))
            {
                string defaultTemplate = templatePaths.FirstOrDefault(t => 
                    t.Contains("81-C# Script-NewBehaviourScript.cs"));
        
                if (defaultTemplate == null)
                    defaultTemplate = templatePaths.FirstOrDefault();
        
                return defaultTemplate != null ? File.ReadAllText(defaultTemplate) : null;
            }

            // TODO: Match template based on suffix
            // Placeholder for custom template matching logic
            // Example: Find template that contains the suffix in its name
            string matchedTemplate = templatePaths.FirstOrDefault(t => 
                Path.GetFileNameWithoutExtension(t).Contains(templateSuffix));

            if (matchedTemplate != null)
            {
                return File.ReadAllText(matchedTemplate);
            }

            // Fallback to default if no match found
            Debug.LogWarning($"Template suffix '{templateSuffix}' not found. Using default template.");
            string fallbackTemplate = templatePaths.FirstOrDefault(t => 
                t.Contains("81-C# Script-NewBehaviourScript.cs")) ?? templatePaths.FirstOrDefault();
    
            return fallbackTemplate != null ? File.ReadAllText(fallbackTemplate) : null;
        }
        

        private static string GetSelectObjectPath()
        {
            string projectPath = Directory.GetCurrentDirectory();
            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            string fullPath = Path.Combine(projectPath, assetPath);
            return fullPath;
        }

        #endregion
    }

    #region Template Selection Window

    public class TemplateSelectionWindow : EditorWindow
    {
        public string[] templateNames;
        public int selectedIndex = -1;
        private Vector2 scrollPosition;

        private void OnGUI()
        {
            titleContent = new GUIContent("Select Script Template");
            minSize = new Vector2(400, 300);

            GUILayout.Label("Choose a template for the scripts:", EditorStyles.boldLabel);
            GUILayout.Space(10);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            for (int i = 0; i < templateNames.Length; i++)
            {
                if (GUILayout.Button(templateNames[i], GUILayout.Height(30)))
                {
                    selectedIndex = i;
                    Close();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                selectedIndex = -1;
                Close();
            }
        }
    }

    #endregion
}