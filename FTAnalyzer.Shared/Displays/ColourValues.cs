namespace FTAnalyzer
{
    public static class ColourValues
    {
        [System.Flags]
        public enum BMDColours
        {
            EMPTY = 0, UNKNOWN_DATE = 1, OPEN_ENDED_DATE = 13, VERY_WIDE_DATE = 2, WIDE_DATE = 3, JUST_YEAR_DATE = 4, NARROW_DATE = 5, APPROX_DATE = 6,
            EXACT_DATE = 7, NO_SPOUSE = 8, NO_PARTNER = 9, NO_MARRIAGE = 10, ISLIVING = 11, OVER90 = 12, ALL_RECORDS = 99
        };
    }
}
