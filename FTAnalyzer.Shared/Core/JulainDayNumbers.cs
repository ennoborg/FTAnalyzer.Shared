using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace FTAnalyzer
{
    class JulianDayNumbers
    {

        static UInt16[] aCumulativeDays = new UInt16[]
        {
              0   // Month = 0  : -92 (Should not be accessed by algorithm)
          ,   0   // Month = 1  : -61 (Should not be accessed by algorithm)
          ,   0   // Month = 2  : -31 (Should not be accessed by algorithm)
          ,   0   // Month = 3  (March)
          ,  31   // Month = 4  (April)
          ,  61   // Month = 5  (May)
          ,  92   // Month = 6  (June)
          , 122   // Month = 7  (July)
          , 153   // Month = 8  (August)
          , 184   // Month = 9  (September)
          , 214   // Month = 10 (October)
          , 245   // Month = 11 (November)
          , 275   // Month = 12 (December)
          , 306   // Month = 13 (January, next year)
          , 337   // Month = 14 (February, next year)
        };

        // BUG: function has not been verified. This too hardly  matters given how it is used here.
        // BUG: this date to JD conversion assumes the Gregorian Calendar.
        // That defect hardly matters given the way the julian day is used here (compare to each other), but should be fixed when use of jd is folded into Individual
        static public Int32 DateToJD(Int16 year, UInt16 month, UInt16 day)
        {
            Int32 jd = 0;

            Int16 Y = year;
            UInt16 M = month;
            UInt16 D = day;
            Int16 B;

            // a few guards aginast the worst nonense
            if (Y < -4713) return 0;
            if (M > 12) return 0;
            if (D > 31) return 0;


            // calculation uses year starting in March
            if (2 < M)
            {
                Y--;
            }
            else
            {
                M += 12;
            };
            Debug.Assert(M < aCumulativeDays.Length);

            B = (Int16)(2 - (Y / 100) + (Y / 100) / 4);

            jd = (Y + 4716);
            jd += (jd * 365) + jd / 4;
            jd += aCumulativeDays[M];
            jd += D;
            jd += B;
            jd -= 1524;

            return jd;
        }

        static public Int32 DateToJD(DateTime dt)
        {
            Int32 jd = 0;

            Int16 Y = (Int16)dt.Year;
            UInt16 M = (UInt16)dt.Month;
            UInt16 D = (UInt16)dt.Day;
            Int16 B;

            // calculation uses year starting in March
            if (2 < M)
            {
                Y--;
            }
            else
            {
                M += 12;
            };
            Debug.Assert(M < aCumulativeDays.Length);

            B = (Int16)(2 - (Y / 100) + (Y / 100) / 4);

            jd = (Y + 4716);
            jd += (jd * 365) + jd / 4;
            jd += aCumulativeDays[M];
            jd += D;
            jd += B;
            jd -= 1524;

            return jd;
        }

    }
}
