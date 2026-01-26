using Seamless.RelationStudio.ViewModel;
using Syncfusion.Maui.Charts;
using System.Collections.Specialized;
using Seamless.Reasoning;

namespace Seamless.RelationStudio;

public partial class ChartPage : ContentPage
{
    private Statistics _stats;

    public ChartPage(Statistics stats) {
        InitializeComponent();
        _stats = stats;
        BindingContext = stats;
        
        // Configure Bandwidth Axis to be on the right
        YBandwidth.CrossesAt = double.MaxValue;
        
        _stats.SfGrid.CollectionChanged += OnSfGridCollectionChanged;
        
        // Populate existing (if any)
        foreach(var core in _stats.SfGrid) {
            AddSeriesForCore(core);
        }

        stats.read();
    }

    private void OnSfGridCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null) {
            foreach(コア core in e.NewItems) {
                MainThread.BeginInvokeOnMainThread(() => {
                    AddSeriesForCore(core);
                });
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset) {
            MainThread.BeginInvokeOnMainThread(() => {
                Chart.Series.Clear();
            });
        }
    }

    private void AddSeriesForCore(コア core) {
        // Latency Series (Line)
        var lineSeries = new LineSeries {
            ItemsSource = core.ListRam,
            XBindingPath = "CacheSizeKiB",
            YBindingPath = "LatencyNs",
            YAxisName = "YLatency",
            Label = $"Latency Core {core.OS上のID}",
            ShowMarkers = true,
            EnableTooltip = true
        };
        Chart.Series.Add(lineSeries);

        // Bandwidth Series (Area)
        var areaSeries = new AreaSeries {
            ItemsSource = core.ListRam,
            XBindingPath = "CacheSizeKiB",
            YBindingPath = "BandwidthGiB",
            YAxisName = "YBandwidth",
            Label = $"Bandwidth Core {core.OS上のID}",
            Opacity = 0.5,
            ShowMarkers = true,
            EnableTooltip = true
        };
        Chart.Series.Add(areaSeries);
    }
}