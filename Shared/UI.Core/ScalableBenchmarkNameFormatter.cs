namespace UI;

internal readonly record struct スケーラブルベンチマークパラメーター(
    long Indexサイズ,
    long Pageサイズ,
    long Segmentサイズ,
    long Memoryサイズ,
    double 可変Fraction);

internal enum スケーラブルベンチマーク引数列 {
    Count,
    Index,
    Page,
    Segment,
    Memory,
    Fraction,
    Initial,
    Data,
}

internal static class ベンチマーク引数列一覧 {
    private static readonly スケーラブルベンチマーク引数列[] 永続化列一覧 = [
        スケーラブルベンチマーク引数列.Count,
        スケーラブルベンチマーク引数列.Initial,
        スケーラブルベンチマーク引数列.Data,
    ];
    private static readonly スケーラブルベンチマーク引数列[] Tsavorite列一覧 = [
        スケーラブルベンチマーク引数列.Count,
        スケーラブルベンチマーク引数列.Index,
        スケーラブルベンチマーク引数列.Page,
        スケーラブルベンチマーク引数列.Segment,
        スケーラブルベンチマーク引数列.Memory,
        スケーラブルベンチマーク引数列.Fraction,
    ];
    private static readonly スケーラブルベンチマーク引数列[] 空列一覧 = [];

    public static IReadOnlyList<スケーラブルベンチマーク引数列> 取得(ベンチマークソースタブ定義 ソースタブ) =>
        取得(ソースタブ.タブ);

    public static IReadOnlyList<スケーラブルベンチマーク引数列> 取得(ベンチマークソースタブ ソースタブ) =>
        ソースタブ switch {
            ベンチマークソースタブ.スケーラブル永続化リスト
                or ベンチマークソースタブ.スケーラブル永続化セット
                or ベンチマークソースタブ.スケーラブル永続化辞書=>永続化列一覧,
            ベンチマークソースタブ.スケーラブルTsavoriteセット
                or ベンチマークソースタブ.スケーラブルTsavorite辞書
                or ベンチマークソースタブ.スケーラブルセット
                or ベンチマークソースタブ.スケーラブル辞書=>Tsavorite列一覧,
            _ => 空列一覧,
        };
}

internal static class スケーラブルベンチマーク名フォーマッター {
    private const long 既定Indexサイズ = 64;
    private const long 既定Pageサイズ = 4096;
    private const long 既定Segmentサイズ = 8192;
    private const long 既定Memoryサイズ = 16384;
    private const double 既定可変Fraction = 0.5;

    private static readonly string[] 既知型トークン一覧 = [
        "UnmanagedStruct",
        "ManagedStruct",
        "ManagedClass",
        "Boolean",
        "Decimal",
        "Double",
        "Single",
        "String",
        "UInt64",
        "UInt32",
        "UInt16",
        "Int64",
        "Int32",
        "Int16",
        "Byte",
        "SByte",
        "Char",
    ];

    public static bool スケーラブルソースタブか(ベンチマークソースタブ ソースタブ) =>
        ソースタブ is ベンチマークソースタブ.スケーラブルセット
            or ベンチマークソースタブ.スケーラブル辞書
            or ベンチマークソースタブ.スケーラブル永続化リスト
            or ベンチマークソースタブ.スケーラブル永続化セット
            or ベンチマークソースタブ.スケーラブルTsavoriteセット
            or ベンチマークソースタブ.スケーラブル永続化辞書
            or ベンチマークソースタブ.スケーラブルTsavorite辞書;

    public static bool 型付きベンチマーク名を使うか(ベンチマークソースタブ ソースタブ) =>
        スケーラブルソースタブか(ソースタブ)
            ||ソースタブ is ベンチマークソースタブ.Seamless永続化
                or ベンチマークソースタブ.SeamlessTsavorite
                or ベンチマークソースタブ.System配列
                or ベンチマークソースタブ.SystemIEnumerable
                or ベンチマークソースタブ.System範囲
                or ベンチマークソースタブ.Systemリスト;

    public static bool 前回右列を使うか(ベンチマークソースタブ ソースタブ) =>
        ソースタブ is ベンチマークソースタブ.スケーラブル永続化セット
            or ベンチマークソースタブ.スケーラブルTsavoriteセット
            or ベンチマークソースタブ.スケーラブル永続化辞書
            or ベンチマークソースタブ.スケーラブルTsavorite辞書
            or ベンチマークソースタブ.スケーラブル永続化リスト;

    public static string ベンチマーク名を作成(
        ベンチマークソースタブ定義 ソースタブ,
        string 型完全名,
        string メソッド名,
        int 要素数 = 0) {
        if(!型付きベンチマーク名を使うか(ソースタブ.タブ)) {
            return メソッド名;
        }

        var ジェネリック表示 = ジェネリック型表示を作成(型完全名);
        if(string.IsNullOrWhiteSpace(ジェネリック表示)) {
            return メソッド名;
        }

        return "<"+ジェネリック表示+">."+メソッド名;
    }

    public static string ベンチマーク型表示を作成(string 型完全名) =>
        ジェネリック型表示を作成(型完全名);

    public static string 従来メソッド1比較型表示を作成(string 型完全名) {
        var 表示 = ベンチマーク型表示を作成(型完全名);
        if(!string.IsNullOrWhiteSpace(表示)) {
            return 表示;
        }

        var 型名 = 型短名(型完全名);
        if(型名 is "FLinq" or "Linq" or "ZLinq") {
            return "int";
        }
        return string.Empty;
    }

    public static string ベンチマーク名を作成(
        string ジェネリック表示,
        string メソッド名,
        スケーラブルベンチマークパラメーター パラメーター) =>
        "<"+ジェネリック表示+">."+メソッド名;

    public static string ベンチマーク名を作成(string 型完全名,string メソッド名) {
        var ジェネリック表示 = ジェネリック型表示を作成(型完全名);
        return string.IsNullOrWhiteSpace(ジェネリック表示)
            ? メソッド名
            : "<"+ジェネリック表示+">."+メソッド名;
    }

    public static string 引数値を取得(IReadOnlyList<ベンチマーク引数> 引数一覧,スケーラブルベンチマーク引数列 列) =>
        ベンチマーク引数フォーマッター.値を取得(引数一覧,列.ToString());

    public static string 表示引数値を取得(
        ベンチマークソースタブ定義 ソースタブ,
        IReadOnlyList<ベンチマーク引数> 引数一覧,
        スケーラブルベンチマーク引数列 列) {
        var 値 = 引数値を取得(引数一覧,列);
        if(!string.IsNullOrWhiteSpace(値)||!スケーラブルソースタブか(ソースタブ.タブ)) {
            return 値;
        }

        if(!Tsavoriteストレージ引数を使うか(ソースタブ.タブ)) {
            return 値;
        }

        var Count = ベンチマーク引数フォーマッター.整数を取得(引数一覧,スケーラブルベンチマーク引数列.Count.ToString());
        var パラメーター = パラメーターを作成(ソースタブ,Count);
        return 列 switch {
            スケーラブルベンチマーク引数列.Index => パラメーター.Indexサイズ.ToString(System.Globalization.CultureInfo.InvariantCulture),
            スケーラブルベンチマーク引数列.Page => パラメーター.Pageサイズ.ToString(System.Globalization.CultureInfo.InvariantCulture),
            スケーラブルベンチマーク引数列.Segment => パラメーター.Segmentサイズ.ToString(System.Globalization.CultureInfo.InvariantCulture),
            スケーラブルベンチマーク引数列.Memory => パラメーター.Memoryサイズ.ToString(System.Globalization.CultureInfo.InvariantCulture),
            スケーラブルベンチマーク引数列.Fraction => パラメーター.可変Fraction.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture),
            _ => 値,
        };
    }

    private static bool Tsavoriteストレージ引数を使うか(ベンチマークソースタブ ソースタブ) =>
        ソースタブ is ベンチマークソースタブ.スケーラブルTsavoriteセット
            or ベンチマークソースタブ.スケーラブルTsavorite辞書
            or ベンチマークソースタブ.スケーラブルセット
            or ベンチマークソースタブ.スケーラブル辞書;

    public static string パラメーター接尾辞を作成(スケーラブルベンチマークパラメーター パラメーター) =>
        固定幅で整形(パラメーター.Indexサイズ,6)
        +"_"+固定幅で整形(パラメーター.Pageサイズ,7)
        +"_"+固定幅で整形(パラメーター.Segmentサイズ,7)
        +"_"+固定幅で整形(パラメーター.Memoryサイズ,7)
        +"_"+Fractionを整形(パラメーター.可変Fraction);

    public static string ジェネリック型表示を作成(string 型完全名) {
        var 型名 = 型短名(型完全名);
        if(型名.StartsWith("Array",StringComparison.Ordinal)) {
            return 型トークンを整形(型接尾辞を正規化(型名["Array".Length..]));
        }
        if(型名.StartsWith("IEnumerable",StringComparison.Ordinal)) {
            return 型トークンを整形(型接尾辞を正規化(型名["IEnumerable".Length..]));
        }
        if(型名=="Range_start_count") {
            return "int";
        }
        if(型名.StartsWith("Persisted",StringComparison.Ordinal)) {
            return 型トークンを整形(型接尾辞を正規化(型名["Persisted".Length..]));
        }
        if(型名.StartsWith("Tsavorite",StringComparison.Ordinal)) {
            return 型トークンを整形(型接尾辞を正規化(型名["Tsavorite".Length..]));
        }
        if(型名.StartsWith("Dictionary",StringComparison.Ordinal)) {
            return string.Join(",",型トークンを分割(型接尾辞を正規化(型名["Dictionary".Length..])).Select(型トークンを整形));
        }
        if(型名.StartsWith("List",StringComparison.Ordinal)) {
            return 型トークンを整形(型接尾辞を正規化(型名["List".Length..]));
        }
        if(型名.StartsWith("Set",StringComparison.Ordinal)) {
            return 型トークンを整形(型接尾辞を正規化(型名["Set".Length..]));
        }
        if(型名.StartsWith("HashSet",StringComparison.Ordinal)) {
            return 型トークンを整形(型接尾辞を正規化(型名["HashSet".Length..]));
        }
        if(型名.StartsWith("BlittableHashSet",StringComparison.Ordinal)) {
            return 型トークンを整形(型接尾辞を正規化(型名["BlittableHashSet".Length..]));
        }
        if(型名.StartsWith("FLinq",StringComparison.Ordinal)) {
            return 型トークンを整形(型接尾辞を正規化(型名["FLinq".Length..]));
        }
        if(型名.StartsWith("Linq",StringComparison.Ordinal)) {
            return 型トークンを整形(型接尾辞を正規化(型名["Linq".Length..]));
        }
        if(型名.StartsWith("ZLinq",StringComparison.Ordinal)) {
            return 型トークンを整形(型接尾辞を正規化(型名["ZLinq".Length..]));
        }

        return string.Empty;
    }

    public static string 型短名(string 型完全名) {
        if(string.IsNullOrWhiteSpace(型完全名)) {
            return string.Empty;
        }

        var インデックス = 型完全名.LastIndexOf('.');
        return インデックス<0||インデックス==型完全名.Length-1
            ? 型完全名
            : 型完全名[(インデックス+1)..];
    }

    private static スケーラブルベンチマークパラメーター パラメーターを作成(ベンチマークソースタブ定義 ソースタブ,int 要素数) {
        var Memoryサイズ = 要素数>0
            ? スケーラブルMemoryサイズを取得(要素数)
            : 既定Memoryサイズ;
        return ソースタブ.タブ switch {
            ベンチマークソースタブ.スケーラブルTsavoriteセット
                or ベンチマークソースタブ.スケーラブルTsavorite辞書
                or ベンチマークソースタブ.スケーラブルセット
                or ベンチマークソースタブ.スケーラブル辞書=>new(
                    131072,
                    4096,
                    4096,
                    Memoryサイズ,
                    1.0),
            _ => new(
                既定Indexサイズ,
                既定Pageサイズ,
                既定Segmentサイズ,
                Memoryサイズ,
                既定可変Fraction),
        };
    }

    private static long スケーラブルMemoryサイズを取得(int Count) {
        var 必要サイズ = Math.Max(8192L,Count*128L);
        var 結果 = 1L;
        while(結果<必要サイズ) {
            結果<<=1;
        }
        return 結果;
    }

    private static string 固定幅で整形(long 値,int 幅) =>
        値.ToString("D"+幅.ToString(System.Globalization.CultureInfo.InvariantCulture),System.Globalization.CultureInfo.InvariantCulture);

    private static string Fractionを整形(double 値) =>
        ((int)Math.Round(値*10,MidpointRounding.AwayFromZero)).ToString("D2",System.Globalization.CultureInfo.InvariantCulture);

    private static IEnumerable<string> 型トークンを分割(string 文字列) {
        for(var インデックス = 0;インデックス<文字列.Length;) {
            var トークン = 既知型トークン一覧.FirstOrDefault(候補 => 文字列[インデックス..].StartsWith(候補,StringComparison.Ordinal));
            if(トークン is not null) {
                yield return トークン;
                インデックス+=トークン.Length;
                continue;
            }

            var 次インデックス = インデックス+1;
            yield return 文字列[インデックス..次インデックス];
            インデックス=次インデックス;
        }
    }

    private static string 型接尾辞を正規化(string 文字列) {
        var 値 = 文字列.TrimStart('_');
        if(値.StartsWith("MemoryPack",StringComparison.Ordinal)) {
            値=値["MemoryPack".Length..].TrimStart('_');
        }
        if(値.StartsWith("Blittable",StringComparison.Ordinal)) {
            値=値["Blittable".Length..].TrimStart('_');
        }
        return 値;
    }

    private static string 型トークンを整形(string トークン) =>
        トークン switch {
            "String" => "string",
            "Int" => "int",
            "Int32" => "int",
            "Decimal" => "decimal",
            "Boolean" => "System.Boolean",
            "Double" => "System.Double",
            "Single" => "System.Single",
            "UInt64" => "System.UInt64",
            "UInt32" => "System.UInt32",
            "UInt16" => "System.UInt16",
            "Int64" => "System.Int64",
            "Int16" => "System.Int16",
            "Byte" => "System.Byte",
            "SByte" => "System.SByte",
            "Char" => "System.Char",
            "ManagedClass" => "ManagedClass",
            "ManagedStruct" => "ManagedStruct",
            "UnmanagedStruct" => "UnmanagedStruct",
            _ => トークン,
        };
}
