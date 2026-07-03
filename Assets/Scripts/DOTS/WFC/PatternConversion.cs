namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Conversion helper for DungeonPattern to WFCPattern
    /// </summary>
    public static class PatternConversion
    {
        public static WFCPattern ToWFCPattern(DungeonPattern pattern)
        {
            return new WFCPattern
            {
                patternId = pattern.id,
                weight = pattern.weight,
                domain = PatternDomain.Dungeon,
                type = (int)pattern.type,
                north = pattern.north,
                east = pattern.east,
                south = pattern.south,
                west = pattern.west
            };
        }
    }
}
