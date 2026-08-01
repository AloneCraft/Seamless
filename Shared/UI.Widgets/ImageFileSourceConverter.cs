using System.Globalization;

namespace UI;

public sealed class 画像ファイルソースコンバーター:IValueConverter {
    public object? Convert(object? 値,Type 対象型,object? パラメーター,CultureInfo カルチャー) {
        return 値 is string パス&&!string.IsNullOrWhiteSpace(パス)
            ? ImageSource.FromFile(パス)
            : null;
    }

    public object ConvertBack(object? 値,Type 対象型,object? パラメーター,CultureInfo カルチャー) {
        throw new NotSupportedException();
    }
}
