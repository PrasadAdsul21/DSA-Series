
using System;

class Program
{
    static void Main()
    {
        int[,] arr =
        {
            { 1,  2,  3,  4 },
            { 5,  6,  7,  8 },
            { 9, 10, 11, 12 },
            { 13, 14, 15, 16 }
        };

        int top = 0;
        int bottom = arr.GetLength(0) - 1;
        int left = 0;
        int right = arr.GetLength(1) - 1;

        while (top <= bottom && left <= right)
        {
            // 1. Traverse left to right
            for (int i = left; i <= right; i++)
                Console.Write(arr[top, i] + " ");

            top++;

            // 2. Traverse top to bottom
            for (int i = top; i <= bottom; i++)
                Console.Write(arr[i, right] + " ");

            right--;

            // 3. Traverse right to left
            if (top <= bottom)
            {
                for (int i = right; i >= left; i--)
                    Console.Write(arr[bottom, i] + " ");

                bottom--;
            }

            // 4. Traverse bottom to top
            if (left <= right)
            {
                for (int i = bottom; i >= top; i--)
                    Console.Write(arr[i, left] + " ");

                left++;
            }
        }
    }
}
