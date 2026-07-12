using System.Globalization;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;

public sealed class SortRule:ObservableObject{
    private GridSortColumn column;
    private bool descending;

    public SortRule(GridSortColumn column,bool descending = false){
        this.column=column;
        this.descending=descending;
    }

    public GridSortColumn Column{
        get => this.column;
        set{
            if(this.SetProperty(ref this.column,value)){
                this.OnPropertyChanged(nameof(this.ColumnText));
            }
        }
    }

    public bool Descending{
        get => this.descending;
        set{
            if(this.SetProperty(ref this.descending,value)){
                this.OnPropertyChanged(nameof(this.DirectionText));
            }
        }
    }

    public string ColumnText => GetColumnText(this.Column);

    public string DirectionText => this.Descending ? "降順" : "昇順";

    public static string GetColumnText(GridSortColumn column) =>
        column switch{
            GridSortColumn.BenchmarkType => "型",
            GridSortColumn.BenchmarkMethod => "ベンチマーク",
            GridSortColumn.BenchmarkName => "ベンチマーク",
            GridSortColumn.ElementCount => "要素数",
            GridSortColumn.ArgumentCount => "Count",
            GridSortColumn.ArgumentIndex => "Index",
            GridSortColumn.ArgumentPage => "Page",
            GridSortColumn.ArgumentSegment => "Segment",
            GridSortColumn.ArgumentMemory => "Memory",
            GridSortColumn.ArgumentFraction => "Fraction",
            GridSortColumn.ArgumentInitial => "Initial",
            GridSortColumn.ArgumentData => "Data",
            GridSortColumn.PairRatioTime => "時間比",
            GridSortColumn.PairRatioAllocated => "メモリシェア",
            GridSortColumn.LeftTime => "Left 時間",
            GridSortColumn.LeftAllocated => "Left メモリ",
            GridSortColumn.RightTime => "Right 時間",
            GridSortColumn.RightAllocated => "Right メモリ",
            _ => column.ToString(),
        };
}
