using Sample.NetShop.Core;

namespace Sample.NetShop.Controls;

/// <summary>画面全体で使う色。</summary>
internal static class 配色{
    public static readonly Color 背景=Color.FromArgb("#F4F6FA");
    public static readonly Color 面=Colors.White;
    public static readonly Color 枠=Color.FromArgb("#D5DBE5");
    public static readonly Color 文字=Color.FromArgb("#1F2937");
    public static readonly Color 薄い文字=Color.FromArgb("#64748B");
    public static readonly Color アクセント=Color.FromArgb("#2563EB");
    public static readonly Color 成功=Color.FromArgb("#15803D");
    public static readonly Color 失敗=Color.FromArgb("#B91C1C");
    public static readonly Color コード背景=Color.FromArgb("#1E293B");
    public static readonly Color コード文字=Color.FromArgb("#E2E8F0");
    public static readonly Color コード強調=Color.FromArgb("#3B4F7A");
    public static readonly Color 実行中の章=Color.FromArgb("#DBEAFE");

    public static Color 棒(棒の色 色){
        switch(色){
            case 棒の色.強調:
                return Color.FromArgb("#2563EB");
            case 棒の色.比較:
                return Color.FromArgb("#F59E0B");
            case 棒の色.警告:
                return Color.FromArgb("#DC2626");
            default:
                return Color.FromArgb("#94A3B8");
        }
    }

    public static Color ログ(ログ種別 種別){
        switch(種別){
            case ログ種別.成功:
                return 成功;
            case ログ種別.失敗:
                return 失敗;
            default:
                return 文字;
        }
    }
}
