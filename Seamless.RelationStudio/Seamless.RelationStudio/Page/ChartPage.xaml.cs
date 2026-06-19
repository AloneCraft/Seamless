using Syncfusion.Maui.Charts;
using System.Collections.Specialized;
using Seamless.Reasoning;

namespace Seamless.RelationStudio;

public partial class ChartPage:ContentPage{
    private Statistics _stats;

    public ChartPage(Statistics stats){
        InitializeComponent();
        this._stats=stats;
        this.BindingContext=stats;

        // Configure Bandwidth Axis to be on the right
        YBandwidth.CrossesAt=double.MaxValue;
        this._stats.SfGrid.CollectionChanged+=this.OnSfGridCollectionChanged;

        // Populate existing (if any)
        var a=0;
        foreach(var core in this._stats.SfGrid){
            this.AddSeriesForCore(core);
            //if(a==3) break;
            a++;
        }

        //stats.read();
    }

    private void OnSfGridCollectionChanged(object? sender,NotifyCollectionChangedEventArgs e){
        if(e.Action==NotifyCollectionChangedAction.Add&&e.NewItems!=null){
            foreach(�R�A core in e.NewItems) {
                MainThread.BeginInvokeOnMainThread(()=>{
                    this.AddSeriesForCore(core);
                });
            }
        }
        else if(e.Action==NotifyCollectionChangedAction.Reset){
            MainThread.BeginInvokeOnMainThread(()=>{
                Chart.Series.Clear();
            });
        }
    }

    private void AddSeriesForCore(�R�A core){
        var Latency=new LineSeries{
            ItemsSource=core.ArrayRam,
            XBindingPath="CacheSizeKiB",
            YBindingPath="LatencyNs",
            YAxisName="YLatency",
            Label=$"Latency Core {core.OS���ID}",
            ShowMarkers=true,
            EnableTooltip=true
        };
        Chart.Series.Add(Latency);

        //var Bandwidth = new LineSeries {
        //    ItemsSource = core.EnumerableRam,
        //    XBindingPath = "CacheSizeKiB",
        //    YBindingPath = "BandwidthGiB",
        //    YAxisName = "YBandwidth",
        //    Label = $"Bandwidth Core {core.OS���ID}",
        //    Opacity = 0.5,
        //    ShowMarkers = true,
        //    EnableTooltip = true
        //};
        //Chart.Series.Add(Bandwidth);
    }
}