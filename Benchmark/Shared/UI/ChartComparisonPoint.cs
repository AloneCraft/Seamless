using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed class ChartComparisonPoint{
    public ChartComparisonPoint(
        string benchmark,
        string displayName,
        double leftValue,
        double rightValue,
        string leftText,
        string rightText,
        string tooltipText,
        double? ratio)
        : this(benchmark,displayName,string.Empty,displayName,false,false,leftValue,rightValue,leftText,rightText,tooltipText,ratio,false,false,false,false){
    }

    public ChartComparisonPoint(
        string benchmark,
        string displayName,
        string groupDisplayName,
        string rowDisplayName,
        bool isGroupFirst,
        bool isGroupLast,
        double leftValue,
        double rightValue,
        string leftText,
        string rightText,
        string tooltipText,
        double? ratio)
        : this(benchmark,displayName,groupDisplayName,rowDisplayName,isGroupFirst,isGroupLast,leftValue,rightValue,leftText,rightText,tooltipText,ratio,false,false,false,false){
    }

    private ChartComparisonPoint(
        string benchmark,
        string displayName,
        string groupDisplayName,
        string rowDisplayName,
        bool isGroupFirst,
        bool isGroupLast,
        double leftValue,
        double rightValue,
        string leftText,
        string rightText,
        string tooltipText,
        double? ratio,
        bool isRunning,
        bool isLeftUiSelected,
        bool isRightUiSelected,
        bool isRowUiSelected){
        this.Benchmark=benchmark;
        this.BaseDisplayName=displayName;
        this.GroupDisplayName=groupDisplayName;
        this.RowDisplayName=rowDisplayName;
        this.IsGroupFirst=isGroupFirst;
        this.IsGroupLast=isGroupLast;
        this.LeftValue=leftValue;
        this.RightValue=rightValue;
        this.LeftText=leftText;
        this.RightText=rightText;
        this.TooltipText=tooltipText;
        this.Ratio=ratio;
        this.IsRunning=isRunning;
        this.IsLeftUiSelected=isLeftUiSelected;
        this.IsRightUiSelected=isRightUiSelected;
        this.IsRowUiSelected=isRowUiSelected;
    }

    public string Benchmark { get; }
    public string BaseDisplayName { get; }
    public string DisplayName => this.IsRunning
        ? "◁E"+this.BaseDisplayName
        : this.BaseDisplayName;
    public string GroupDisplayName { get; }
    public string RowDisplayName { get; }
    public bool IsGroupFirst { get; }
    public bool IsGroupLast { get; }
    public double LeftValue { get; }
    public double RightValue { get; }
    public string LeftText { get; }
    public string RightText { get; }
    public string TooltipText { get; }
    public double? Ratio { get; }
    public bool IsRunning { get; }
    public bool IsLeftUiSelected { get; }
    public bool IsRightUiSelected { get; }
    public bool IsRowUiSelected { get; }

    public ChartComparisonPoint WithRunningState(bool isRunning) =>
        new(
            this.Benchmark,
            this.BaseDisplayName,
            this.GroupDisplayName,
            this.RowDisplayName,
            this.IsGroupFirst,
            this.IsGroupLast,
            this.LeftValue,
            this.RightValue,
            this.LeftText,
            this.RightText,
            this.TooltipText,
            this.Ratio,
            isRunning,
            this.IsLeftUiSelected,
            this.IsRightUiSelected,
            this.IsRowUiSelected);

    public ChartComparisonPoint WithUiSelection(bool isLeftUiSelected,bool isRightUiSelected,bool isRowUiSelected) =>
        new(
            this.Benchmark,
            this.BaseDisplayName,
            this.GroupDisplayName,
            this.RowDisplayName,
            this.IsGroupFirst,
            this.IsGroupLast,
            this.LeftValue,
            this.RightValue,
            this.LeftText,
            this.RightText,
            this.TooltipText,
            this.Ratio,
            this.IsRunning,
            isLeftUiSelected,
            isRightUiSelected,
            isRowUiSelected);

    public ChartComparisonPoint WithGroupDisplay(string groupDisplayName,string rowDisplayName,bool isGroupFirst,bool isGroupLast) =>
        new(
            this.Benchmark,
            rowDisplayName,
            groupDisplayName,
            rowDisplayName,
            isGroupFirst,
            isGroupLast,
            this.LeftValue,
            this.RightValue,
            this.LeftText,
            this.RightText,
            this.TooltipText,
            this.Ratio,
            this.IsRunning,
            this.IsLeftUiSelected,
            this.IsRightUiSelected,
            this.IsRowUiSelected);
}
