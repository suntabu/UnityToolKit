using System;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityToolKit.UnityObjects
{
    public class TextureUtils
    {
        public static Texture2D LoadFromBytes(byte[] bytes)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            tex.name = string.Format("bytes_{0}", System.Guid.NewGuid().ToString());
            
            return tex;
        }

        public static Texture2D LoadFromStreamingPath(string path)
        {
            try
            {
                byte[] fileBytes = PathUtils.GetStreamingAssetsFileBytes(path);
                Texture2D tex = LoadFromBytes(fileBytes);
                tex.name = Path.GetFileName(path) ?? Guid.NewGuid().ToString();
                return tex;
            }
            catch (Exception e)
            {
                Debug.Log(string.Format("{0}\n{1}", e.Message, e.StackTrace));
            }
            return null;
        }
    }
}