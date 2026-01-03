using System;
using System.Collections.Generic;

namespace StandUpTracker.Utils
{
    internal sealed class DescTimeComparer : IComparer<TimeSpan>
    {
        public int Compare(TimeSpan x, TimeSpan y) => -x.CompareTo(y);
    }

    internal static class SortedHelpers
    {
        public static IEnumerable<KeyValuePair<TimeSpan, string>> TopN(Dictionary<string, TimeSpan> dict, int n)
        {
            var sl = new SortedList<TimeSpan, string>(new DescTimeComparer());
            foreach (var kv in dict)
            {
                var key = kv.Value;
                while (sl.ContainsKey(key))
                    key += TimeSpan.FromMilliseconds(1); // prevent duplicate keys
                sl.Add(key, kv.Key);
                if (sl.Count > n)
                    sl.RemoveAt(sl.Count - 1);
            }
            foreach (var kv in sl)
                yield return kv;
        }
    }
}
