
using CommunityToolkit.Mvvm.ComponentModel;

using Seamless.Reasoning;

using System.Collections.ObjectModel;
using System.Globalization;
namespace Seamless.RelationStudio.PageModel;
public partial class Chart:ObservableObject{
    [ObservableProperty]
    public double _L1Size;
    //public ObservableCollection<GridRowModel> SfGrid { get; } = new();
    public ObservableCollection<Ram> Rams{ get; } = new();
    public ObservableCollection<コア> Enumerableコア{ get; } = new();
    public ObservableCollection<コア> SfGrid { get; } = new();
    private bool IsLoaded;
    private readonly Benchmark Benchmark;
    public Chart(Benchmark Benchmark){
        this.Benchmark=Benchmark;
        Benchmark.Run();
    }
    public async Task read(){
        //for(var a=0;a<1000;a++){
        //    await Task.Run(()=>Thread.Sleep(100));
        //}
        if(this.IsLoaded) return;
        this.IsLoaded = true;

        //var Rams = this.Rams;
        var SfGrid = this.SfGrid;
        ThreadPool.QueueUserWorkItem(async st=>{
            var Benchmark=this.Benchmark;
            //GC.Collect(2,GCCollectionMode.Forced,true);
            //GC.WaitForPendingFinalizers();
            var ProcessorCount=Environment.ProcessorCount;
            for(var a=0;a<ProcessorCount;a++){
                var OS上のID=new nint(1L<<a);
                実行環境.SetThreadAffinityMask(実行環境.GetCurrentThread(),OS上のID);
                Thread.Sleep(0);
                var コア=new コア(OS上のID);
                //コア.
                await foreach(var b in Benchmark.GetRamsEnumerable()){
                    コア.ArrayRam.Add(new(b.Bytes,b.レイテンシsecond,b.BytesSecond));
                }
                SfGrid.Add(コア);
            }
        });
        //await foreach(var a in Benchmark.GetコアEnumerableAsync()) {
        //    SfGrid.Add(a);
        //}
    }
}
