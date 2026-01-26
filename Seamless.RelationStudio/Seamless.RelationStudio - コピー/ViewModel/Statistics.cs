
using CommunityToolkit.Mvvm.ComponentModel;

using Seamless.Reasoning;

using System.Collections.ObjectModel;
using System.Globalization;
namespace Seamless.RelationStudio.ViewModel;
public partial class GridRowModel : ObservableObject
{
    [ObservableProperty]
    private string _列0; // 計測項目名など

    [ObservableProperty]
    private string _列1; // 計測結果の数値など
}
public partial class Statistics:ObservableObject{
    [ObservableProperty]
    public double _L1Size;
    //public ObservableCollection<GridRowModel> SfGrid { get; } = new();
    public ObservableCollection<Ram> Rams{ get; } = new();
    public ObservableCollection<コア> SfGrid { get; } = new();
    public async Task read(){
        var Rams=this.Rams;
        var SfGrid=this.SfGrid;
        //SfGrid.Add(new GridRowModel{列0 = "33",列1 = "444"});
        //SfGrid.Add(
        //    new コア(0){
        //        L1 =new(10,3,4){レイテンシsecond = 10},
        //        L2 = new(10,3,4){レイテンシsecond = 10}
        //    }
        //);
        var Benchmark=new Benchmark();
        //Benchmark.Run();
        Benchmark.GetRamsEnumerableAsync(Rams);
        //await foreach(var a in Benchmark.GetRamsEnumerable()){
        //    //    Rams.Add(a);
        //    //    b++;
        //    //    //if(b==2) break;
        //}
        //var b=0;
        //await foreach(var a in Benchmark.GetRamsEnumerable()){
        //    Rams.Add(a);
        //    b++;
        //    //if(b==2) break;
        //}
        //var EnumeratorAsync=Benchmark.GetコアEnumerableAsync();
        //await foreach(コア a in EnumeratorAsync){
        //    SfGrid.Add(
        //        a
        //    );
        //}
    }
}
