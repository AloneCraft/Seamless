using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed record ResultElementCountOption(int Value,string DisplayName);

public sealed record ThresholdOption(string DisplayName,int Milliseconds);
