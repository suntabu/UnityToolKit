using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace UnityToolKit.UnityObjects
{
    public class PathUtils
    {
        
        /// <summary>
        /// read a file in streaming assets
        /// </summary>
        /// <param name="filepath">path in streaming assets folder</param>
        /// <param name="folderName">cache folder name</param>
        /// <param name="refresh">force refresh cached file with streamingassets file</param>
        /// <returns></returns>
        public static byte[] GetStreamingAssetsFileBytes(string filepath, string folderName = "suntabu",
            bool refresh = false)
        {
            filepath = filepath.TrimStart(ChTrims);
            string srcPath = Application.streamingAssetsPath + Path.AltDirectorySeparatorChar + filepath;

#if (UNITY_ANDROID) && !UNITY_EDITOR

//Debug.Log("Extracting file from: "+ srcPath);
//Debug.Log("Extracting to: "+ destPath);
            string destPath =
                Application.persistentDataPath + Path.AltDirectorySeparatorChar + folderName +
                Path.AltDirectorySeparatorChar + filepath;

            if (!refresh && File.Exists(destPath))
                return File.ReadAllBytes(destPath);


            using (WWW www = new WWW(srcPath))
            {
                while (!www.isDone)
                {
                    ;
                }

                if (!string.IsNullOrEmpty(www.error))
                {
                    Debug.LogWarning(www.error);
                    return null;
                }

                //create Directory
                String dirPath = Path.GetDirectoryName(destPath);
                if (!Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);

                File.WriteAllBytes(destPath, www.bytes);
                return www.bytes;
            }

#else
            return File.Exists(srcPath) ? File.ReadAllBytes(srcPath) : null;
#endif
        }

 
        public static IEnumerator GetFilePathAsync(string filepath, Action<string> callback, bool refresh = false,
            string folderName = "suntabu")
        {
            filepath = filepath.TrimStart(ChTrims);

#if (UNITY_ANDROID) && !UNITY_EDITOR
        string srcPath = Application.streamingAssetsPath + Path.AltDirectorySeparatorChar + filepath;
        string destPath =
            Application.persistentDataPath + Path.AltDirectorySeparatorChar + folderName +
            Path.AltDirectorySeparatorChar + filepath;

        if (!refresh && File.Exists(destPath))
        {
            callback(destPath);
        }
        else
        {
            using (WWW www = new WWW(srcPath))
            {
                yield return www;

                if (!string.IsNullOrEmpty(www.error))
                {
                    Debug.LogWarning(www.error);
                    callback(String.Empty);
                }

                //create Directory
                String dirPath = Path.GetDirectoryName(destPath);
                if (!Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);

                File.WriteAllBytes(destPath, www.bytes);
            }
            callback(destPath);
        }
#elif (UNITY_WEBGL) && !UNITY_EDITOR
		string srcPath = Application.streamingAssetsPath + Path.AltDirectorySeparatorChar + filepath;
		string destPath = Path.AltDirectorySeparatorChar + folderName + Path.AltDirectorySeparatorChar + filepath;

		if (!refresh && File.Exists(destPath)){
		callback (destPath);
		} else {
		using (WWW www = new WWW (srcPath)) {
		yield return www;

		if (!string.IsNullOrEmpty(www.error)) {
		Debug.LogWarning (www.error);
		callback (String.Empty);
		}

		//create Directory
		String dirPath = Path.GetDirectoryName (destPath);
		if (!Directory.Exists (dirPath))
		Directory.CreateDirectory (dirPath);

		File.WriteAllBytes(destPath, www.bytes);
		}
		callback (destPath);
		}
		#else
            string destPath = Application.streamingAssetsPath + Path.AltDirectorySeparatorChar + filepath;

            callback(File.Exists(destPath) ? destPath : String.Empty);
#endif

            yield break;
        }

        private static readonly char[] ChTrims =
        {
            '.',
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        };
        
        
#if UNITY_EDITOR
        public static string streamingAssetPath = "file://" + Application.streamingAssetsPath + "/";
        public static string persistentDataPath = "file://" + Application.persistentDataPath + "/";
        public static string temporaryCachePath = "file://" + Application.temporaryCachePath + "/";
#elif UNITY_ANDROID
		public static string streamingAssetPath = Application.streamingAssetsPath + "/";
		public static string persistentDataPath = "file://" + Application.persistentDataPath + "/";
		public static string temporaryCachePath = "file://" + Application.temporaryCachePath + "/";
		#else
		public static string streamingAssetPath = "file://" + Application.streamingAssetsPath + "/";
		public static string persistentDataPath = "file://" + Application.persistentDataPath + "/";
		public static string temporaryCachePath = "file://" + Application.temporaryCachePath + "/";
		#endif
    }
}