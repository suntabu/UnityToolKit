using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityLibrary
{
    public class Commands
    {
        [MenuItem("Command/Clear All (PlayerPrefs, Persistent, TemporaryCache)")]
        public static void Clear()
        {
            PlayerPrefs.DeleteAll();
            if (Directory.Exists(Application.persistentDataPath))
                Directory.Delete(Application.persistentDataPath, true);
            if (Directory.Exists(Application.temporaryCachePath))
            {
                Directory.Delete(Application.temporaryCachePath, true);
            }

            AssetDatabase.Refresh();
        }

        [MenuItem("Commands/Folder/Open/PersistentDataPath")]
        static void OpenPersistentDataPath()
        {
            System.Diagnostics.Process p = System.Diagnostics.Process.Start(Application.persistentDataPath);
            p.Close();
        }

        [MenuItem("Commands/Folder/Open/TemporaryCachePath")]
        static void OpenTemporaryCachePath()
        {
            System.Diagnostics.Process p = System.Diagnostics.Process.Start(Application.temporaryCachePath);
            p.Close();
        }
    }
}