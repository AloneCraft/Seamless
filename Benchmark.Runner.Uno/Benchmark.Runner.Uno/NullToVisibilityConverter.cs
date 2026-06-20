using global::System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace Benchmark.Runner.Uno;

public sealed class NullToVisibilityConverter:IValueConverter{
    public object Convert(object value,Type targetType,object parameter,string language){
        return value is null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value,Type targetType,object parameter,string language){
        throw new NotSupportedException();
    }
}
