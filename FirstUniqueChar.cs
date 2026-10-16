using System.Collections.Generic;

public class Solution 
{
    public static int FirstUniqChar(string s) 
    {
        var counts = new Dictionary<char, int>();
        
        // Pass 1: Count frequencies
        foreach (char c in s) 
        {
            counts[c] = counts.GetValueOrDefault(c, 0) + 1;
        }
        
        // Pass 2: Find first character with count == 1
        for (int i = 0; i < s.Length; i++) 
        {
            if (counts[s[i]] == 1) 
            {
                return i;
            }
        }
        
        return -1;
    }
}