public class Solution
{
    public int MaxProfit(int[] prices)
    {
        // Edge case: Empty or single-day array cannot yield profit
        if (prices == null || prices.Length < 2)
        {
            return 0;
        }

        int maxProfit = 0;
        int minPrice = prices[0];

        for (int i = 1; i < prices.Length; i++)
        {
            if (prices[i] > minPrice)
            {
                maxProfit = Math.Max(maxProfit, prices[i] - minPrice);
            }
            else
            {
                // If prices[i] <= minPrice, updating minPrice directly avoids an extra Math.Min call
                minPrice = prices[i];
            }
        }

        return maxProfit;
    }
}