
using CommunityToolkit.Mvvm.ComponentModel;

using Seamless.Reasoning;

using System.Collections.ObjectModel;
using System.Globalization;
namespace Seamless.RelationStudio.ViewModel;
public partial class GridRowModel:ObservableObject{
    [ObservableProperty]
    private string _列0; // 計測項目名など

    [ObservableProperty]
    private string _列1; // 計測結果の数値など
}
public partial class Statistics:ObservableObject{
    [ObservableProperty]
    public double _L1Size;
    //public ObservableCollection<GridRowModel> SfGrid { get; } = new();
    public ObservableCollection<Ram> Rams{get;}=new();
    public ObservableCollection<コア> SfGrid{get;}=new();
    private bool IsLoaded;
    public async Task read(){
        if(IsLoaded) return;
        IsLoaded=true;

        var Rams=this.Rams;
        var SfGrid=this.SfGrid;

        using var Benchmark=new Benchmark();

        await foreach(var a in Benchmark.GetRamsEnumerable()){
            Rams.Add(a);
        }
        await foreach(var a in Benchmark.GetコアEnumerableAsync()){
            SfGrid.Add(a);
        }
    }
}
