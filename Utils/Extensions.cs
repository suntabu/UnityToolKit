using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
    }
}