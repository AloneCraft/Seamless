using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public readonly record struct SortValue(string? Text,double? Number,bool IsMissing){
    public static SortValue FromText(string text) => new(text,null,string.IsNullOrWhiteSpace(text));

    public static SortValue FromMetric(ProjectMetrics metrics,MetricKind kind){
        if(!metrics.HasMeasuredValue){
            return Missing;
        }

        return kind==MetricKind.Time
            ? FromNumber(metrics.TimeNanoseconds)
            : FromNumber(metrics.AllocatedBytes);
    }

    public static SortValue FromRatio(RatioValue ratio){
        return ratio.Kind switch{
            RatioValueKind.Numeric when ratio.NumericValue is { } value => FromNumber(value),
            RatioValueKind.BothZero => FromNumber(0),
            RatioValueKind.LeftOnly => FromFiniteOrInfinity(double.PositiveInfinity),
            RatioValueKind.RightOnly => FromNumber(0),
            _ => Missing,
        };
    }

    public static SortValue FromNumber(double? value){
        return value is { } number&&double.IsFinite(number)
            ? new(null,number,false)
            : Missing;
    }

    public static SortValue FromFiniteOrInfinity(double? value){
        return value is { } number&&!double.IsNaN(number)
            ? new(null,number,false)
            : Missing;
    }

    public static readonly SortValue Missing = new(null,null,true);

    public static int Compare(SortValue left,SortValue right,bool descending){
        if(left.IsMissing&&right.IsMissing){
            return 0;
        }

        if(left.IsMissing){
            return 1;
        }

        if(right.IsMissing){
            return -1;
        }

        var result = left.Number is { } leftNumber&&right.Number is { } rightNumber
            ? leftNumber.CompareTo(rightNumber)
            : string.Compare(left.Text,right.Text,StringComparison.OrdinalIgnoreCase);
        return descending ? -result : result;
    }
}
