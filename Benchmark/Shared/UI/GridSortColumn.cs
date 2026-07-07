using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public enum GridSortColumn{
    BenchmarkType,
    BenchmarkMethod,
    BenchmarkName,
    ElementCount,
    ArgumentCount,
    ArgumentIndex,
    ArgumentPage,
    ArgumentSegment,
    ArgumentMemory,
    ArgumentFraction,
    ArgumentInitial,
    ArgumentData,
    PairRatioTime,
    PairRatioAllocated,
    LeftTime,
    LeftAllocated,
    RightTime,
    RightAllocated,
}
