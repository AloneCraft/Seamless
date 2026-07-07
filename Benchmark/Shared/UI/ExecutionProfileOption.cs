using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed record ExecutionProfileOption(string DisplayName,BenchmarkExecutionProfile Profile);

public sealed record ScaleOption(string DisplayName,string RunValue);
