using CommunityToolkit.Mvvm.ComponentModel;

using Seamless.Reasoning;

using System.Collections.ObjectModel;
using System.Globalization;
namespace Seamless.RelationStudio;
public enum UnitConverterParameter{
    K,M,G,
    m,u,n,
    Ki,Mi,Gi,
}
public class UnitConverter:IValueConverter{
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
            }
        }
        return value;
    }
    public object ConvertBack(object? value,Type targetType,object? parameter,CultureInfo culture){
        throw new NotImplementedException();
    }
}
public class InverseBoolConverter:IValueConverter{
    public object Convert(object? value,Type targetType,object? parameter,CultureInfo culture){
        if(value is bool booleanValue)
            return!booleanValue;
        return value;
    }

    public object ConvertBack(object? value,Type targetType,object? parameter,CultureInfo culture){
        if(value is bool booleanValue)
            return!booleanValue;
        return value;
    }
}
