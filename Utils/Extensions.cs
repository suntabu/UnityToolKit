using System;

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
    }
}
