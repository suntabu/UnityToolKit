using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;

/// <summary>
/// 给脚本添加和修改命名空间
/// </summary>
public class AddNamespaceTool : ScriptableWizard {

	public string folder="Assets/";
	public string namespaceName;

	void OnEnable(){
		if(Selection.activeObject != null)
		{
			string dirPath = AssetDatabase.GetAssetOrScenePath(Selection.activeObject);
			if(File.Exists(dirPath)){
				dirPath = dirPath.Substring(0,dirPath.LastIndexOf("/"));
			}
			folder = dirPath;
		}
	}

	[MenuItem("Tools/Add Namespace",false,20000)]
	static void CreateWizard () {
		AddNamespaceTool editor = ScriptableWizard.DisplayWizard<AddNamespaceTool>("Add Namespace", "Add");
		editor.minSize = new Vector2(300,200);
	}
	public void OnWizardCreate(){
		//save settting

		if(!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(namespaceName))
		{

			List<string> filesPaths = new List<string>();
			filesPaths.AddRange(
				Directory.GetFiles(Path.GetFullPath(".") + Path.DirectorySeparatorChar + folder, "*.cs", SearchOption.AllDirectories)
			);

			int counter = -1;
			foreach (string filePath in filesPaths) {
				EditorUtility.DisplayProgressBar("Add Namespace", filePath, counter / (float) filesPaths.Count);
				counter++;
		
				string contents = File.ReadAllText(filePath);

				string result = "";
				bool havsNS = contents.Contains("namespace ");
				string t = havsNS ? "" : "\t";

				using(TextReader reader = new StringReader(contents))
				{
					int index = 0;
					bool addedNS = false;
					while (reader.Peek() != -1)
					{
						string line = reader.ReadLine();

						if(line.IndexOf("using")>-1){
							result += line+"\n";
						}else if(!addedNS && !havsNS){
							result += "\nnamespace "+namespaceName+" {";
							addedNS = true;
							result += t+line+"\n";
						}else{
							if(havsNS && line.Contains("namespace ")){
								if(line.Contains("{")){
									result += "namespace "+namespaceName+" {\n";
								}else{
									result += "namespace "+namespaceName+"\n";
								}
							}
							else
							{
								result += t+line+"\n";
							}
						}
						++index;
					}
					reader.Close();
				}
				if(!havsNS){
					result += "}";
				}
				File.WriteAllText(filePath, result);
			}

			EditorUtility.ClearProgressBar();
			AssetDatabase.Refresh();
		}
	}
}
