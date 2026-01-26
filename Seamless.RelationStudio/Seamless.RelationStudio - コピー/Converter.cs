
using CommunityToolkit.Mvvm.ComponentModel;

using Seamless.Reasoning;

using System.Collections.ObjectModel;
using System.Globalization;
namespace Seamless.RelationStudio;
public enum UnitConverterParameter {
    K,M,G,
    m,u,n,
    Ki,Mi,Gi,
}
public class UnitConverter:IValueConverter {
    public object Convert(object? value,Type targetType,object? parameter,CultureInfo culture){
        var UnitConverterParameter=(UnitConverterParameter)parameter!;
        if(value is double @double){
            switch(UnitConverterParameter){
                case UnitConverterParameter.K:return @double/Const.K;
                case UnitConverterParameter.M:return @double/Const.M;
                case UnitConverterParameter.G:return @double/Const.G;
                case UnitConverterParameter.m:return @double/Const.m;
                case UnitConverterParameter.u:return @double/Const.u;
                case UnitConverterParameter.n:return @double/Const.n;
                case UnitConverterParameter.Ki:return @double/Const.Ki;
                case UnitConverterParameter.Mi:return @double/Const.Mi;
                case UnitConverterParameter.Gi:return @double/Const.Gi;
                default:throw new ArgumentOutOfRangeException();
            }
        } else if(value is long @long){
            switch(UnitConverterParameter){
                case UnitConverterParameter.K:return@long/Const.K;
                case UnitConverterParameter.M:return@long/Const.M;
                case UnitConverterParameter.G:return@long/Const.G;
                case UnitConverterParameter.m:return@long/Const.m;
                case UnitConverterParameter.u:return@long/Const.u;
                case UnitConverterParameter.n:return@long/Const.n;
                case UnitConverterParameter.Ki:return@long/Const.Ki;
                case UnitConverterParameter.Mi:return@long/Const.Mi;
                case UnitConverterParameter.Gi:return@long/Const.Gi;
                default:throw new ArgumentOutOfRangeException();
            }
        }
        throw new ArgumentOutOfRangeException();
        //if(value is double d) {

        //    // レイテンシ(秒)をナノ秒(ns)に変換
        //    if(parameter as string == "Latency") {
        //        double ns = d * 1_000_000_000;
        //        return $"{ns:N2} ns";
        //    }

        //    // Byte/s を GB/s に変換
        //} else {
        //    var v = (long)value;
        //    if(parameter as string == "Bandwidth") {
        //        var gbps = v / (1024 * 1024 * 1024);
        //        return $"{gbps:N2} GB/s";
        //    }
        //}
        //return value?.ToString() ?? "";
    }

    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)
        => throw new NotImplementedException();
}
