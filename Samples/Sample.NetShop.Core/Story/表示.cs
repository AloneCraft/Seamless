using System.Globalization;

namespace Sample.NetShop.Core;

/// <summary>画面に出す数値の書式。</summary>
public static class 表示{
    public static string バイト(long バイト数){
        if(バイト数>=1L<<30){
            return (バイト数/(double)(1L<<30)).ToString("0.0",CultureInfo.InvariantCulture)+" GB";
        }
        if(バイト数>=1L<<20){
            return (バイト数/(double)(1L<<20)).ToString("0.0",CultureInfo.InvariantCulture)+" MB";
        }
        if(バイト数>=1L<<10){
            return (バイト数/(double)(1L<<10)).ToString("0.0",CultureInfo.InvariantCulture)+" KB";
        }
        return バイト数.ToString(CultureInfo.InvariantCulture)+" B";
    }

    public static string 金額(long 円)=>"¥"+円.ToString("N0",CultureInfo.InvariantCulture);

    public static string ミリ秒(TimeSpan 時間)=>時間.TotalMilliseconds.ToString("N1",CultureInfo.InvariantCulture)+" ms";
}
