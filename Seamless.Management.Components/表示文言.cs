using System.Globalization;
using System.Resources;

namespace Seamless.Management.Components;

public static class 表示文言{
    private static readonly ResourceManager 資源=new("Seamless.Management.Components.表示文言",typeof(表示文言).Assembly);
    public static string 言語{
        get{
            if(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="en"){return "en";}
            return "ja";
        }
    }
    public static string 取得(string 原文)=>資源.GetString(原文,CultureInfo.CurrentUICulture)??原文;
    public static string 文脈(string 分類,string 原文)=>資源.GetString(分類+"."+原文,CultureInfo.CurrentUICulture)??取得(原文);
    public static string 取得(string 原文,params object?[]引数)=>string.Format(CultureInfo.CurrentCulture,取得(原文),引数);
}
