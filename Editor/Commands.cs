using UnityEditor;
using UnityEngine;

namespace UnityLibrary
{
    public class Commands
    {
        [MenuItem("Commands/Folders/Open persistent")]
        public static void OpenPersitent()
        {
            Debug.Log(Application.persistentDataPath);
            Application.OpenURL(Application.persistentDataPath);
        }
    }
}