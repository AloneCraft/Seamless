using BenchmarkDotNet.Attributes;

using System.Reflection;

namespace UI;

public sealed record ベンチマークプロジェクト(string 表示名,string アセンブリ名,string?型接頭辞=null);

public static class ベンチマークカタログ{
    public static IReadOnlyList<ベンチマークプロジェクト>スケーラブルプロジェクト一覧{get;}=[
        new("Scalable.Persisted.Set","Benchmark.Scalable.Persisted.Collections","Benchmark.Scalable.Persisted.Collections.Set."),
        new("Scalable.Tsavorite.Set","Benchmark.Scalable.Tsavorite.Collections","Benchmark.Scalable.Tsavorite.Collections.Set."),
        new("Scalable.Persisted.Dictionary","Benchmark.Scalable.Persisted.Collections","Benchmark.Scalable.Persisted.Collections.Dictionary."),
        new("Scalable.Tsavorite.Dictionary","Benchmark.Scalable.Tsavorite.Collections","Benchmark.Scalable.Tsavorite.Collections.Dictionary."),
        new("Scalable.Persisted.List","Benchmark.Scalable.Persisted.Collections","Benchmark.Scalable.Persisted.Collections.List."),
    ];

    public static IReadOnlyList<ベンチマークプロジェクト>標準プロジェクト一覧{get;}=ベンチマークソースタブ一覧.標準全て
        .SelectMany(static タブ=>new[]{
            new ベンチマークプロジェクト(タブ.左プロジェクト,タブ.左アセンブリ名,タブ.左型接頭辞),
            new ベンチマークプロジェクト(タブ.右プロジェクト,タブ.右アセンブリ名,タブ.右型接頭辞),
        })
        .Concat([
        new("Persisted コレクション","Benchmark.Persisted.Collections"),
        new("Tsavorite コレクション","Benchmark.Tsavorite.Collections"),
        ])
        .ToArray();

#if SCALABLE_BENCHMARK
    private static IReadOnlyList<ベンチマークプロジェクト>プロジェクト一覧=>スケーラブルプロジェクト一覧;
#else
    private static IReadOnlyList<ベンチマークプロジェクト>プロジェクト一覧=>標準プロジェクト一覧;
#endif

    public static IReadOnlyList<ベンチマークケース>読み込む(){
        return 読み込み本体(static プロジェクト=>Assembly.Load(new AssemblyName(プロジェクト.アセンブリ名)),利用不可アセンブリを飛ばすか:true);
    }

    internal static IReadOnlyList<ベンチマークケース>読み込む(IReadOnlyDictionary<string,Assembly>登録アセンブリ一覧){
        ArgumentNullException.ThrowIfNull(登録アセンブリ一覧);
        アセンブリ登録を検証(プロジェクト一覧,登録アセンブリ一覧.Keys);
        return 読み込み本体(プロジェクト=>登録アセンブリ一覧.TryGetValue(プロジェクト.アセンブリ名,out var アセンブリ)
            ?アセンブリ
            :throw new InvalidOperationException("ベンチマークアセンブリが登録されていません: "+プロジェクト.アセンブリ名),利用不可アセンブリを飛ばすか:false);
    }

    internal static void アセンブリ登録を検証(
        IReadOnlyList<ベンチマークプロジェクト>プロジェクト一覧,
        IEnumerable<string>登録アセンブリ名一覧){
        var 期待一覧=プロジェクト一覧
            .Select(static プロジェクト=>プロジェクト.アセンブリ名)
            .Where(static 名前=>!string.IsNullOrWhiteSpace(名前))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var 登録一覧=登録アセンブリ名一覧
            .Where(static 名前=>!string.IsNullOrWhiteSpace(名前))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var 不足一覧=期待一覧.Except(登録一覧,StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var 余分一覧=登録一覧.Except(期待一覧,StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if(不足一覧.Length==0&&余分一覧.Length==0){
            return;
        }

        throw new InvalidOperationException(
            "ベンチマークアセンブリの登録が一致しません。不足: "
            +(不足一覧.Length==0?"（なし）":string.Join(", ",不足一覧))
            +"; 余分: "
            +(余分一覧.Length==0?"（なし）":string.Join(", ",余分一覧)));
    }

    private static IReadOnlyList<ベンチマークケース>読み込み本体(Func<ベンチマークプロジェクト,Assembly>アセンブリ解決器,bool 利用不可アセンブリを飛ばすか){
        var ケース一覧=new List<ベンチマークケース>();
        foreach(var プロジェクト in プロジェクト一覧.Where(static プロジェクト=>!string.IsNullOrWhiteSpace(プロジェクト.アセンブリ名))){
            Assembly アセンブリ;
            try{
                アセンブリ=アセンブリ解決器(プロジェクト);
            }catch(Exception 例外)when(利用不可アセンブリを飛ばすか&&例外 is FileNotFoundException or FileLoadException or BadImageFormatException){
                System.Diagnostics.Trace.WriteLine("ベンチマークカタログは利用できないアセンブリをスキップしました '"+プロジェクト.アセンブリ名+"': "+例外.Message);
                continue;
            }
            foreach(var 型 in アセンブリ.GetTypes().Where(型=>具象ベンチマーク型か(型,プロジェクト.型接頭辞)).OrderBy(static 型=>型.FullName,StringComparer.Ordinal)){
                foreach(var メソッド in 型.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).Where(ベンチマーク属性があるか).OrderBy(static メソッド=>メソッド.Name,StringComparer.Ordinal)){
                    var 引数組一覧=ベンチマーク引数を取得(メソッド).ToArray();
                    if(引数組一覧.Length==0){
                        ケース一覧.Add(new ベンチマークケース(プロジェクト.表示名,プロジェクト.アセンブリ名,型.FullName!,メソッド.Name,ベンチマークパスを作成(型)));
                        continue;
                    }

                    foreach(var 引数組 in 引数組一覧){
                        ケース一覧.Add(new ベンチマークケース(
                            プロジェクト.表示名,
                            プロジェクト.アセンブリ名,
                            型.FullName!,
                            メソッド.Name,
                            ベンチマーク引数フォーマッター.キーを作成(引数組),
                            引数組,
                            ベンチマークパスを作成(型)));
                    }
                }
            }
        }

        return ケース一覧;
    }

    private static bool 具象ベンチマーク型か(Type 型,string? 型接頭辞) =>
        型 is { IsAbstract: false, ContainsGenericParameters: false }
        &&(string.IsNullOrWhiteSpace(型接頭辞)||型.FullName?.StartsWith(型接頭辞,StringComparison.Ordinal)==true)
        &&型.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).Any(ベンチマーク属性があるか);

    private static bool ベンチマーク属性があるか(MethodInfo メソッド) =>
        メソッド.GetCustomAttribute<BenchmarkAttribute>() is not null;

    private static string ベンチマークパスを作成(Type 型) {
        var 入れ子名一覧 = new Stack<string>();
        var 基底 = 型;
        while(基底.DeclaringType is not null) {
            入れ子名一覧.Push(チェーン型名を正規化(基底.Name));
            基底=基底.DeclaringType;
        }
        var 論理基底 = 論理基底型を検索(基底);
        var 結果 = 論理型を整形(論理基底??基底);
        return 入れ子名一覧.Count==0 ? 結果 : 結果+"."+string.Join(".",入れ子名一覧);
    }

    private static Type? 論理基底型を検索(Type 型) {
        for(var 現在 = 型.BaseType;現在 is not null;現在=現在.BaseType) {
            if(現在.Namespace=="Seamless.Linq.Shared"
                &&(現在.Name.StartsWith("Array`",StringComparison.Ordinal)
                    ||現在.Name.StartsWith("IEnumerable`",StringComparison.Ordinal)
                    ||現在.Name.StartsWith("List`",StringComparison.Ordinal)
                    ||現在.Name.StartsWith("Persisted`",StringComparison.Ordinal)
                    ||現在.Name.StartsWith("Tsavorite`",StringComparison.Ordinal)
                    ||現在.Name=="Range_start_count")) {
                return 現在;
            }
            if(現在.Namespace=="Seamless.Linq.Optimization"
                &&(現在.Name.StartsWith("Array",StringComparison.Ordinal)
                    ||現在.Name.StartsWith("Enumerable",StringComparison.Ordinal)
                    ||現在.Name.StartsWith("List",StringComparison.Ordinal)
                    ||現在.Name.StartsWith("Range",StringComparison.Ordinal))) {
                return 現在;
            }
        }
        return null;
    }

    private static string 論理型を整形(Type 型) {
        var 名前 = 最適化型名を正規化(チェーン型名を正規化(型.Name.Split('`')[0]));
        if(!型.IsGenericType) {
            return 名前;
        }
        return 名前+"<"+string.Join(",",型.GetGenericArguments().Select(要素型を整形))+">";
    }

    private static string 要素型を整形(Type 型) => 型==typeof(int) ? "int" : 型.Name;
    private static string チェーン型名を正規化(string 名前) => 名前.EndsWith("_chain",StringComparison.Ordinal) ? 名前[..^6] : 名前;
    private static string 最適化型名を正規化(string 名前) {
        foreach(var 接尾辞 in new[] { "Rewrite","Pipeline","Pushdown" }) {
            if(名前.EndsWith(接尾辞,StringComparison.Ordinal)) {
                名前=名前[..^接尾辞.Length];
                break;
            }
        }
        return 名前 switch { "Enumerable" => "IEnumerable", "Range" => "Range_start_count", _ => 名前 };
    }

    private static IEnumerable<IReadOnlyList<ベンチマーク引数>> ベンチマーク引数を取得(MethodInfo メソッド) {
        var 引数名一覧 = メソッド.GetParameters().Select(static 引数 => 引数.Name??string.Empty).ToArray();
        foreach(var 属性 in メソッド.CustomAttributes.Where(static 属性 =>
            属性.AttributeType.Name=="BenchArgumentsAttribute"
            ||属性.AttributeType.Name==nameof(ArgumentsAttribute))) {
            var 値一覧 = 属性.ConstructorArguments
                .SelectMany(static 引数 => 引数.ArgumentType==typeof(object[])
                    ? 引数.Value is IEnumerable<CustomAttributeTypedArgument> 値一覧
                        ? 値一覧.Select(static 値 => 値.Value)
                        : []
                    : [引数.Value])
                .ToArray();
            var 名前一覧 = 引数名を解決(引数名一覧,値一覧.Length);
            yield return 名前一覧
                .Select((名前,インデックス) => new ベンチマーク引数(
                    引数名を正規化(名前),
                    インデックス<値一覧.Length ? 引数値を整形(値一覧[インデックス]) : string.Empty))
                .ToArray();
        }
    }

    private static string[] 引数名を解決(string[] 引数名一覧,int 値数) {
        if(引数名一覧.Length==値数&&!可変長Dataのみか(引数名一覧)) {
            return 引数名一覧;
        }

        return 値数 switch {
            3 => ["Count","Initial","Data"],
            6 => ["Count","Index","Page","Segment","Memory","Fraction"],
            _ => 引数名一覧,
        };
    }

    private static bool 可変長Dataのみか(string[] 引数名一覧) =>
        引数名一覧.Length==1
        &&string.Equals(引数名一覧[0],"data",StringComparison.OrdinalIgnoreCase);

    private static string 引数名を正規化(string 名前) =>
        名前 switch {
            "IndexSize" => "Index",
            "PageSize" => "Page",
            "SegmentSize" => "Segment",
            "MemorySize" => "Memory",
            "MutableFraction" => "Fraction",
            "InitialCapacity" => "Initial",
            "InitialDataCapacity" => "Data",
            _ => 名前,
        };

    private static string 引数値を整形(object? 値) =>
        値 is IFormattable 整形可能値
            ? 整形可能値.ToString(null,System.Globalization.CultureInfo.InvariantCulture)
            : 値?.ToString()??string.Empty;
}
