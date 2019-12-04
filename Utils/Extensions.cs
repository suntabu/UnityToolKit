using System;

namespace UnityToolKit.Utils
{
    public static class Extensions
    {
        public static string ToVersionString(this DateTime date)
        {
            //TODO
            return date.ToShortDateString();
        }
    }
}