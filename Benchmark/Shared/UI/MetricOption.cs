using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed record MetricOption(string DisplayName,MetricKind Kind);

public sealed record ChartModeOption(string DisplayName,ChartMode Mode);
