using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityToolKit.Utils
{
    public static class Extensions
    {
        public static string ToVersionString(this DateTime date)
        {
            //TODO
            return date.ToShortDateString();
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
    }
}