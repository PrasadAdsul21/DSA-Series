using System;
using System.Collections.Generic;

public class Solution 
{
    public bool IsValid(string s) 
    {
        Stack<char> stack = new Stack<char>();

        foreach (char c in s) 
        {
            // Push matching closing brackets onto the stack for opening ones
            if (c == '(') 
            {
                stack.Push(')');
            } 
            else if (c == '{') 
            {
                stack.Push('}');
            } 
            else if (c == '[') 
            {
                stack.Push(']');
            } 
            // If it's a closing bracket, check if the stack is empty or top doesn't match
            else if (stack.Count == 0 || stack.Pop() != c) 
            {
                return false;
            }
        }

        // If stack is empty, all opening brackets were properly closed
        return stack.Count == 0;
    }
}
