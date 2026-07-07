using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed class SourceTabOption:TabHeaderOptionBase{
    private static readonly Color RunningIndicatorColor=Color.FromArgb("#00FF00");
    private static readonly Color DefaultProgressOutlineColor=Color.FromArgb("#64748B");
    private BenchmarkTabProgress progress;
    private bool isRunning;

    public SourceTabOption(BenchmarkSourceTabDefinition definition){
        this.Definition=definition;
    }

    public BenchmarkSourceTabDefinition Definition { get; }
    public BenchmarkSourceTab Tab => this.Definition.Tab;
    public string Id => this.Definition.Id;
    public string DisplayName => this.Definition.DisplayName;
    public BenchmarkTabProgress Progress{
        get => this.progress;
        private set{
            if(this.SetProperty(ref this.progress,value)){
                this.OnPropertyChanged(nameof(this.HeaderText));
                this.OnPropertyChanged(nameof(this.HasProgress));
                this.OnPropertyChanged(nameof(this.ProgressFraction));
                this.OnPropertyChanged(nameof(this.ProgressText));
                this.OnPropertyChanged(nameof(this.ProgressTooltipText));
            }
        }
    }
    public override string HeaderText => this.DisplayName;
    public bool HasProgress => this.Progress.Total>0;
    public double ProgressFraction => this.Progress.Total<=0 ? 0 : this.Progress.Fraction;
    public string ProgressText => this.Progress.Total<=0 ? "0/0" : this.Progress.FractionText;
    public string ProgressTooltipText => "確宁E"+this.ProgressText;
    public Color ProgressOutlineColor => this.IsRunning ? RunningIndicatorColor : DefaultProgressOutlineColor;
    public bool IsRunning{
        get => this.isRunning;
        private set{
            if(this.SetProperty(ref this.isRunning,value)){
                this.OnPropertyChanged(nameof(this.ProgressOutlineColor));
            }
        }
    }

    public void SetProgress(BenchmarkTabProgress value){
        this.Progress=value;
    }

    public void SetRunning(bool value){
        this.IsRunning=value;
    }
}
