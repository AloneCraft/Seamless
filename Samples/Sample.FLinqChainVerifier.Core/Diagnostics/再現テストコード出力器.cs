using System.Text;

namespace Sample.FLinqChainVerifier.Core;

/// <summary>
/// シード値・組み合わせ・データパターンから、単体テスト(xUnit [Fact])としてそのまま使えるC#コードを生成・保存する。
/// </summary>
public static class 再現テストコード出力器{
    public static string テストコードを生成(
        チェーン検証項目 項目,
        string 原因診断,
        string 改善案){
        var 安全な名前=安全な識別子(項目.識別子);
        var チェーン連結=string.Join("_",項目.メソッド名一覧);
        var クラス名=$"Repro_{安全な名前}";
        var メソッド名=$"Chain_{チェーン連結}_Seed_{項目.シード値:X}";
        var ソース生成式=ソース変換式(項目.ソース種別,"raw");
        var 呼び出し引数型=ソース引数型(項目.ソース種別);
        var クエリ式=拡張メソッドカタログ.クエリ式を構築("source",項目.メソッド名一覧);

        var ビルダー=new StringBuilder();
        ビルダー.AppendLine("using Seamless.FLinq;");
        ビルダー.AppendLine("using Xunit;");
        ビルダー.AppendLine();
        ビルダー.AppendLine("namespace Test.System.Linq.Enumerable.FLinq.FailedQueries;");
        ビルダー.AppendLine();
        ビルダー.AppendLine("/// <summary>");
        ビルダー.AppendLine($"/// 自動生成された再現・検証テストコード");
        ビルダー.AppendLine($"/// 段階: {項目.段階} / ソース: {項目.ソース種別} / シード: 0x{項目.シード値:X} (試行: {項目.試行番号})");
        ビルダー.AppendLine($"/// チェーン: {string.Join(" -> ",項目.メソッド名一覧)}");
        foreach(var 行 in 原因診断.Split('\n',StringSplitOptions.RemoveEmptyEntries)){
            ビルダー.AppendLine($"/// 診断: {行.Trim()}");
        }
        foreach(var 行 in 改善案.Split('\n',StringSplitOptions.RemoveEmptyEntries)){
            ビルダー.AppendLine($"/// 改善案: {行.Trim()}");
        }
        ビルダー.AppendLine("/// </summary>");
        ビルダー.AppendLine($"public sealed class {クラス名}{{");
        ビルダー.AppendLine($"    [Fact,Trait(\"Category\",\"FuzzFailure\")]public void {メソッド名}(){{");
        ビルダー.AppendLine($"        var seed=0x{項目.シード値:X}UL;");
        ビルダー.AppendLine("        var second=new int[]{3,7,10,15,22,31};");
        ビルダー.AppendLine("        foreach(var count in new int[]{0,1,16,256}){");
        ビルダー.AppendLine("            var raw=CreateData(seed,count);");
        ビルダー.AppendLine($"            var input={ソース生成式};");
        ビルダー.AppendLine("            using var scope=LinqCounterScope.Start();");
        ビルダー.AppendLine("            var expected=ExecuteSafe(()=>RunSystem(input,second));");
        ビルダー.AppendLine("            var actual=ExecuteSafe(()=>RunFLinq(input,second));");
        ビルダー.AppendLine("            Assert.Equal(expected.ExceptionType,actual.ExceptionType);");
        ビルダー.AppendLine("            if(expected.ExceptionType is null){");
        ビルダー.AppendLine("                Assert.Equal(expected.Value,actual.Value);");
        ビルダー.AppendLine("            }");
        ビルダー.AppendLine("            Assert.Equal(0,scope.Counter.EnumerableFallback);");
        if(!string.IsNullOrWhiteSpace(項目.手書き理想コード)){
            ビルダー.AppendLine("            var ideal=ExecuteSafe(()=>RunIdeal(raw,second));");
            ビルダー.AppendLine("            Assert.Equal(expected.ExceptionType,ideal.ExceptionType);");
            ビルダー.AppendLine("            if(expected.ExceptionType is null){");
            ビルダー.AppendLine("                Assert.Equal(expected.Value,ideal.Value);");
            ビルダー.AppendLine("            }");
        }
        ビルダー.AppendLine("        }");
        ビルダー.AppendLine("    }");
        ビルダー.AppendLine();
        ビルダー.AppendLine("    [LinqIntercept(false)]");
        ビルダー.AppendLine($"    private static object? RunSystem({呼び出し引数型} source,int[]second)=>{クエリ式};");
        ビルダー.AppendLine();
        ビルダー.AppendLine("    [LinqIntercept(true)]");
        ビルダー.AppendLine($"    private static object? RunFLinq({呼び出し引数型} source,int[]second)=>{クエリ式};");
        if(!string.IsNullOrWhiteSpace(項目.手書き理想コード)){
            ビルダー.AppendLine();
            ビルダー.AppendLine("    [LinqIntercept(false)]");
            ビルダー.AppendLine("    private static object? RunIdeal(int[]raw,int[]second){");
            foreach(var 行 in 項目.手書き理想コード.Split('\n')){
                ビルダー.AppendLine("        "+行.TrimEnd('\r'));
            }
            ビルダー.AppendLine("    }");
        }
        ビルダー.AppendLine();
        ビルダー.AppendLine("    private static int[]CreateData(ulong seed,int count){");
        ビルダー.AppendLine("        var result=new int[count];");
        ビルダー.AppendLine("        var state=seed;");
        ビルダー.AppendLine("        if(state==0UL){");
        ビルダー.AppendLine("            state=0x9E3779B97F4A7C15UL;");
        ビルダー.AppendLine("        }");
        ビルダー.AppendLine("        for(var i=0;i<count;i++){");
        ビルダー.AppendLine("            state^=state>>12;");
        ビルダー.AppendLine("            state^=state<<25;");
        ビルダー.AppendLine("            state^=state>>27;");
        ビルダー.AppendLine("            var v=(int)((state*0x2545F4914F6CDD1DUL)>>32);");
        ビルダー.AppendLine("            result[i]=(v&255)-32;");
        ビルダー.AppendLine("        }");
        ビルダー.AppendLine("        return result;");
        ビルダー.AppendLine("    }");
        ビルダー.AppendLine();
        ビルダー.AppendLine("    private static (object? Value,Type? ExceptionType) ExecuteSafe(Func<object?>action){");
        ビルダー.AppendLine("        try{");
        ビルダー.AppendLine("            var value=action();");
        ビルダー.AppendLine("            if(value is global::System.Collections.IEnumerable seq&&value is not string){");
        ビルダー.AppendLine("                var list=new List<object?>();");
        ビルダー.AppendLine("                foreach(var item in seq){");
        ビルダー.AppendLine("                    list.Add(item);");
        ビルダー.AppendLine("                }");
        ビルダー.AppendLine("                return (list,null);");
        ビルダー.AppendLine("            }");
        ビルダー.AppendLine("            return (value,null);");
        ビルダー.AppendLine("        }catch(Exception ex){");
        ビルダー.AppendLine("            return (null,ex.GetType());");
        ビルダー.AppendLine("        }");
        ビルダー.AppendLine("    }");
        ビルダー.AppendLine("}");
        return ビルダー.ToString();
    }

    public static string ファイルへ保存(
        string 出力先フォルダ,
        チェーン検証項目 項目,
        string テストコード){
        Directory.CreateDirectory(出力先フォルダ);
        var チェーン連結=string.Join("_",項目.メソッド名一覧);
        var ファイル名=$"Fail_D{項目.メソッド名一覧.Count}_{項目.ソース種別}_{安全な識別子(チェーン連結)}_Seed{項目.シード値:X}.cs";
        var フルパス=Path.Combine(出力先フォルダ,ファイル名);
        File.WriteAllText(フルパス,テストコード,Encoding.UTF8);
        return フルパス;
    }

    private static string ソース引数型(入力ソース種別 ソース種別){
        switch(ソース種別){
            case 入力ソース種別.Array:
                return "int[]";
            case 入力ソース種別.List:
                return "List<int>";
            default:
                return "IEnumerable<int>";
        }
    }

    private static string ソース変換式(入力ソース種別 ソース種別,string 元配列名){
        switch(ソース種別){
            case 入力ソース種別.Array:
                return 元配列名;
            case 入力ソース種別.List:
                return $"new List<int>({元配列名})";
            case 入力ソース種別.Range:
                return $"global::System.Linq.Enumerable.Range(1,{元配列名}.Length)";
            default:
                return $"({元配列名} as IEnumerable<int>)";
        }
    }

    private static string 安全な識別子(string 文字列){
        var ビルダー=new StringBuilder(文字列.Length);
        foreach(var 文字 in 文字列){
            if(char.IsLetterOrDigit(文字)||文字=='_'){
                ビルダー.Append(文字);
            }else{
                ビルダー.Append('_');
            }
        }
        return ビルダー.ToString();
    }
}
