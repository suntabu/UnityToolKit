using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ResourcesMergeTool : ScriptableWizard
{
    public string Folder = "Assets/";
    public string Target;

    void OnEnable()
    {
        if (Selection.activeObject != null)
        {
            string dirPath = AssetDatabase.GetAssetOrScenePath(Selection.activeObject);
            if (File.Exists(dirPath))
            {
                dirPath = dirPath.Substring(0, dirPath.LastIndexOf("/"));
            }
            Folder = dirPath;

            
        }
        
        Target = Path.Combine(Folder, "Resources");
    }

    [MenuItem("Tools/Merge resources folder")]
    public static void RegenerateGuids()
    {
        ResourcesMergeTool editor = ScriptableWizard.DisplayWizard<ResourcesMergeTool>("Merge Resources", "Merge");
        editor.minSize = new Vector2(600, 200);
    }

    private void OnWizardCreate()
    {
        if (string.IsNullOrEmpty(Folder) || string.IsNullOrEmpty(Target))
        {
            return;
        }

        Debug.Log(Target + "   <-----");
        List<string> filePaths = new List<string>();

        filePaths.AddRange(
            Directory.GetFiles( Folder, "*.*",
                SearchOption.AllDirectories)
        );


        var key = "/Resources/";
        for (int i = 0; i < filePaths.Count; i++)
        {
            var filePath = filePaths[i];
            EditorUtility.DisplayProgressBar("Merge Resources", filePath, i / (float) filePaths.Count);

            if (filePath.Contains(key) && !filePath.Contains(Target))
            {
                var index = filePath.IndexOf(key, StringComparison.CurrentCulture);
                var temp = filePath.Replace(key, "/");
                var newPath = Target + temp.Substring(index, temp.Length - index);
                Debug.Log(filePath + " --- > " + newPath);

                var targetParent = Path.GetDirectoryName(newPath);
                if (!Directory.Exists(targetParent))
                {
                    Directory.CreateDirectory(targetParent);
                }
                try
                {
                    if (File.Exists(filePath))
                    {
                        if (File.Exists(newPath))
                        {
                            Debug.LogWarning("found existed file, I'll delete it :" + newPath);
                            File.Delete(newPath);
                        }
                        File.Move(filePath, newPath);
                    }
                    else
                    {
                        Debug.LogError("can not find :" + filePath);
                    }
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    break;
                }
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();
    }
}