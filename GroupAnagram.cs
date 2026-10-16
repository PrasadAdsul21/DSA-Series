using System;
using System.Collections.Generic;
using System.Linq;

public class Solution 
{
    public static IList<IList<string>> GroupAnagrams(string[] strs) 
    {
        var map = new Dictionary<string, List<string>>();

        foreach (var s in strs) 
        {
            char[] chars = s.ToCharArray();
            Array.Sort(chars);
            string key = new string(chars);

            if (!map.ContainsKey(key)) 
            {
                map[key] = new List<string>();
            }
            map[key].Add(s);
        }

        return map.Values.Cast<IList<string>>().ToList();
    }
}