namespace DOTS.Terrain.WFC
{
    public static class WFCCellHelpers
    {
        public static bool IsPatternPossible(ref WFCCell cell, int patternIndex)
        {
            return (cell.possiblePatternsMask & (1u << patternIndex)) != 0;
        }
        public static void SetPatternPossible(ref WFCCell cell, int patternIndex, bool possible)
        {
            if (possible)
                cell.possiblePatternsMask |= (1u << patternIndex);
            else
                cell.possiblePatternsMask &= ~(1u << patternIndex);
        }
        public static int CountPossiblePatterns(uint mask)
        {
            int count = 0;
            for (int i = 0; i < 32; i++)
                if ((mask & (1u << i)) != 0) count++;
            return count;
        }
        public static int GetFirstPossiblePattern(uint mask)
        {
            for (int i = 0; i < 32; i++)
                if ((mask & (1u << i)) != 0) return i;
            return -1;
        }
    }
}
