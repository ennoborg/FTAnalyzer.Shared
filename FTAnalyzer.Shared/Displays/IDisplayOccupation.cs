using FTAnalyzer.Utilities;
using System;

namespace FTAnalyzer
{
    public interface IDisplayOccupation : IComparable<IDisplayOccupation>, IColumnComparer<IDisplayOccupation>
    {
        [ColumnDetail("Occupation", 400)]
        string Occupation { get; }
        [ColumnDetail("Count", 70)]
        int Count { get; }
    }
}
