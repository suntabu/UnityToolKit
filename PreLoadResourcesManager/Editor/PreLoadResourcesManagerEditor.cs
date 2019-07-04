namespace UnityToolKit.PreLoadResourcesManager.Editor
{
    using UnityEngine;
    using System.Collections;
#if UNITY_EDITOR
    using UnityEditor;
#endif
    using UnityEngine.UI;

//code lwj for manager preLoadTextures
    [CustomEditor(typeof(PreLoadResourcesManager))]
    public class PreLoadResourcesManagerEditor : Editor
    {
        enum displayFieldType
        {
            DisplayAsAutomaticFields,
            DisplayAsCustomizableGUIFields
        }

        displayFieldType DisplayFieldType;

        PreLoadResourcesManager t;
        SerializedObject GetTarget;
        SerializedProperty ThisList;
        int ListSize;

        void OnEnable()
        {
            t = (PreLoadResourcesManager) target;
            GetTarget = new SerializedObject(t);
            ThisList = GetTarget.FindProperty(
                "preLoadResources"); // Find the List in our script and create a refrence of it
        }

        public override void OnInspectorGUI()
        {
            //Update our list

            GetTarget.Update();

            //Choose how to display the list<> Example purposes only
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            //Resize our list
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Define the list size with a number");
            ListSize = ThisList.arraySize;
            ListSize = EditorGUILayout.IntField("List Size", ListSize);

            if (ListSize != ThisList.arraySize)
            {
                while (ListSize > ThisList.arraySize)
                {
                    ThisList.InsertArrayElementAtIndex(ThisList.arraySize);
                }
                while (ListSize < ThisList.arraySize)
                {
                    ThisList.DeleteArrayElementAtIndex(ThisList.arraySize - 1);
                }
            }


            EditorGUILayout.Space();
            EditorGUILayout.Space();

            //Display our list to the inspector window
            EditorGUILayout.BeginFadeGroup(1);
            for (int i = 0; i < ThisList.arraySize; i++)
            {
                SerializedProperty MyListRef = ThisList.GetArrayElementAtIndex(i);
                SerializedProperty path = MyListRef.FindPropertyRelative("path");
                SerializedProperty floder = MyListRef.FindPropertyRelative("folder");
                SerializedProperty type = MyListRef.FindPropertyRelative("type");
                SerializedProperty meshType = MyListRef.FindPropertyRelative("meshType");
                SerializedProperty pivot = MyListRef.FindPropertyRelative("pivot");
                SerializedProperty readable = MyListRef.FindPropertyRelative("readable");
                SerializedProperty cache = MyListRef.FindPropertyRelative("cache");
                SerializedProperty warpMode = MyListRef.FindPropertyRelative("warpMode");
                SerializedProperty sprite = MyListRef.FindPropertyRelative("sprite");
                SerializedProperty material = MyListRef.FindPropertyRelative("material");


                // Display the property fields in two ways.

                //				if(DisplayFieldType == 0){// Choose to display automatic or custom field types. This is only for example to help display automatic and custom fields.
                //1. Automatic, No customization <-- Choose me I'm automatic and easy to setup
                //				EditorGUILayout.PropertyField(path);
                //				Rect rect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));
                ////				temp_path = path.stringValue;
                ////				Debug.Log("<<<<<<<<<<<"+path.stringValue);
                //				temp_path = EditorGUI.TextField(rect, path.stringValue);
                //				if((Event .current .type == EventType .dragUpdated)&&  
                //					rect .Contains (Event .current .mousePosition))  
                //				{  
                //					Event.current.type = EventType.MouseUp;
                //					DragAndDrop.visualMode = DragAndDropVisualMode.None;  
                //					if(DragAndDrop .paths != null && DragAndDrop .paths .Length > 0)  
                //					{  
                //						Debug.Log ("--------"+DragAndDrop.paths[0]);
                ////						temp_path = DragAndDrop.paths[0];  
                //						path.stringValue = DragAndDrop.paths[0];
                ////						MyListRef.
                ////						serializedObject.ApplyModifiedProperties ();
                ////						path.stringValue = tempPath;
                ////						path = tempPath;
                //					}  
                //				} 
                //				path.objectReferenceValue = EditorGUILayout.ObjectField("My Custom Go", path.objectReferenceValue, typeof(DefaultAsset), false);
                //				Rect rect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));
                //				using (var h = new EditorGUILayout.HorizontalScope("Button"))
                //				{
                //					GUILayout.Label("----------path-----------");
                //
                //					if (GUI.Button(h.rect, GUIContent.none))
                //						Debug.Log("Se");
                //				}
                GUILayout.BeginHorizontal();
                path.stringValue = GUILayout.TextField(path.stringValue);
                if (GUILayout.Button("select"))
                {
                    string tempPath =
                        EditorUtility.OpenFilePanel("Load png Textures", Application.streamingAssetsPath, "");
                    path.stringValue = tempPath.Substring(Application.streamingAssetsPath.Length + 1);
                    //					string[] files = Directory.GetFiles(path1);

                    //					foreach (string file in files)
                    //						if (file.EndsWith(".png"))
                    //							File.Copy(file, EditorApplication.currentScene);
                }
                GUILayout.EndHorizontal();

                //				Debug.Log (path.objectReferenceValue.ToString());
                EditorGUILayout.PropertyField(floder);
                EditorGUILayout.PropertyField(type);
                EditorGUILayout.PropertyField(meshType);
                EditorGUILayout.PropertyField(pivot);
                EditorGUILayout.PropertyField(readable);
                EditorGUILayout.PropertyField(cache);
                EditorGUILayout.PropertyField(warpMode);
                if (type.enumValueIndex == 0)
                    EditorGUILayout.PropertyField(sprite);
                if (type.enumValueIndex == 1)
                    EditorGUILayout.PropertyField(material);


                // Array fields with remove at index
                EditorGUILayout.Space();
                EditorGUILayout.Space();

                //				}else{
                //					//Or
                //
                //					//2 : Full custom GUI Layout <-- Choose me I can be fully customized with GUI options.
                //					EditorGUILayout.LabelField("Customizable Field With GUI");
                //					MyGO.objectReferenceValue = EditorGUILayout.ObjectField("My Custom Go", MyGO.objectReferenceValue, typeof(GameObject), true);
                //					MyInt.intValue = EditorGUILayout.IntField("My Custom Int",MyInt.intValue);
                //					MyFloat.floatValue = EditorGUILayout.FloatField("My Custom Float",MyFloat.floatValue);
                //					MyVect3.vector3Value = EditorGUILayout.Vector3Field("My Custom Vector 3",MyVect3.vector3Value);
                //
                //
                //					// Array fields with remove at index
                //					EditorGUILayout.Space ();
                //					EditorGUILayout.Space ();
                //					EditorGUILayout.LabelField("Array Fields");
                //
                //					if(GUILayout.Button("Add New Index",GUILayout.MaxWidth(130),GUILayout.MaxHeight(20))){
                //						MyArray.InsertArrayElementAtIndex(MyArray.arraySize);
                //						MyArray.GetArrayElementAtIndex(MyArray.arraySize -1).intValue = 0;
                //					}
                //
                //					for(int a = 0; a < MyArray.arraySize; a++){
                //						EditorGUILayout.BeginHorizontal();
                //						EditorGUILayout.LabelField("My Custom Int (" + a.ToString() + ")",GUILayout.MaxWidth(120));
                //						MyArray.GetArrayElementAtIndex(a).intValue = EditorGUILayout.IntField("",MyArray.GetArrayElementAtIndex(a).intValue, GUILayout.MaxWidth(100));
                //						if(GUILayout.Button("-",GUILayout.MaxWidth(15),GUILayout.MaxHeight(15))){
                //							MyArray.DeleteArrayElementAtIndex(a);
                //						}
                //						EditorGUILayout.EndHorizontal();
                //					}
                //				}

                EditorGUILayout.Space();

                //Remove this index from the List
                if (GUILayout.Button("Remove"))
                {
                    ThisList.DeleteArrayElementAtIndex(i);
                }
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                EditorGUILayout.Space();

                EditorGUILayout.EndFadeGroup();
            }
            if (GUILayout.Button("Add New"))
            {
                t.preLoadResources.Add(new PreLoadResourcesManager.Asset());
            }
            if (GUILayout.Button("Load"))
            {
                t.StartPreLoad();
            }
            if (GUILayout.Button("Unload"))
            {
                foreach (PreLoadResourcesManager.Asset asset in t.preLoadResources)
                {
                    if (asset.type == PreLoadResourcesManager.AssetType.Sprite)
                    {
                        SpriteRenderer sr = asset.sprite.GetComponent<SpriteRenderer>();
                        Image img = asset.sprite.GetComponent<Image>();
                        Sprite sp = null;
                        if (sr != null)
                        {
                            sp = sr.sprite;
                            sr.sprite = null;
                        }
                        if (img != null)
                        {
                            sp = img.sprite;
                            img.sprite = null;
                        }
                        GameObject.DestroyImmediate(sp);
                    }
                }
            }


            //Apply the changes to our list
            GetTarget.ApplyModifiedProperties();
        }
    }
}