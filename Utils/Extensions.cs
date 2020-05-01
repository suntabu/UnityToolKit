using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityToolKit.Utils
{
    public static class Extensions
    {
        public static string ToVersionString(this DateTime date)
        {
            var year = date.Year;
            var month = date.Month;
            var day = date.Day;
            var hour = date.Hour;

            return year.ToString().Substring(2, 2) +
                   month.ToString("00") +
                   day.ToString("00") +
                   hour.ToString("00");
        }

        public static bool IsEmpty<T>(this ICollection<T> list)
        {
            if (list != null && list.Count > 0)
            {
                return false;
            }

            return true;
        }

        public static bool IsNotEmpty<T>(this ICollection<T> list)
        {
            return !list.IsEmpty();
        }

        public static T Item<T>(this ICollection<T> list, int index)
        {
            if (list.IsNotEmpty())
            {
                return list.ElementAt(index % list.Count);
            }

            return default(T);
        }
        
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();

            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        public static string GetHierarchyPath(this GameObject obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }

            return path.Substring(1);
        }

        public static string GetHierarchyPath(this Transform obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.parent;
                path = "/" + obj.name + path;
            }

            return path;
        }

        public static Transform FindByHierarchyPath(this string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            string[] names = path.Split('/');
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            Transform parent = null;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == names[0])
                {
                    parent = roots[i].transform;
                    break;
                }
            }

            int index = 1;
            while (parent != null && index < names.Length)
            {
                var child = parent.Find(names[index]);
                parent = child;

                index++;
            }

            return parent;
        }

        public static Texture2D CreateTextureFromAtlas(this Texture2D mainTexture, Rect rect, int fullWidth,
            int fullHeight)
        {
            var fullPixels = mainTexture.GetPixels32();
            int spriteX = (int) rect.x;
            int spriteY = (int) rect.y;
            int spriteWidth = (int) rect.width;
            int spriteHeight = (int) rect.height;

            int xmin = Mathf.Clamp(spriteX, 0, fullWidth);
            int ymin = Mathf.Clamp(spriteY, 0, fullHeight);
            int xmax = Mathf.Min(xmin + spriteWidth, fullWidth - 1);
            int ymax = Mathf.Min(ymin + spriteHeight, fullHeight - 1);
            int newWidth = Mathf.Clamp(spriteWidth, 0, fullWidth);
            int newHeight = Mathf.Clamp(spriteHeight, 0, fullHeight);

            if (newWidth == 0 || newHeight == 0) return null;

            Color32[] newPixels = new Color32[newWidth * newHeight];

            for (int y = 0; y < newHeight; ++y)
            {
                int cy = ymin + y;
                if (cy > ymax) cy = ymax;

                for (int x = 0; x < newWidth; ++x)
                {
                    int cx = xmin + x;
                    if (cx > xmax) cx = xmax;

                    int newIndex = y * newWidth + x;
                    int oldIndex = cy * fullWidth + cx;

                    newPixels[newIndex] = fullPixels[oldIndex];
                }
            }

            var tex = new Texture2D(newWidth, newHeight, TextureFormat.ARGB32, false);
            tex.SetPixels32(newPixels);

            return tex;
        }


        public static Rect BestFit(this Texture tex, float width, float height, float scale = 1,
            bool isRotate = false)
        {
            float tw, th, x, y, texRatio;
            if (!isRotate)
            {
                texRatio = tex.width * 1f / tex.height;
            }
            else
            {
                texRatio = tex.height * 1f / tex.width;
            }

            if (width <= 0)
            {
                th = height * scale;
                tw = th * texRatio;
                y = (height - th) * .5f;
                x = (width - tw) * .5f;
            }
            else if (height <= 0)
            {
                tw = width * scale;
                th = tw / texRatio;
                y = (height - th) * .5f;
                x = (width - tw) * .5f;
            }
            else
            {
                float ratio = width / height;

                if (ratio >= texRatio)
                {
                    th = height * scale;
                    tw = th * texRatio;
                    y = (height - th) * .5f;
                    x = (width - tw) * .5f;
                }
                else
                {
                    tw = width * scale;
                    th = tw / texRatio;
                    y = (height - th) * .5f;
                    x = (width - tw) * .5f;
                }
            }


            Rect rect = new Rect(x, y, tw, th);

            return rect;
        }
    }
}