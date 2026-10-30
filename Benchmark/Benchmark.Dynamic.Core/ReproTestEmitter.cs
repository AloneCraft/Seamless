using System.Text;

namespace Benchmark.Dynamic.Core;

public sealed record ReproTestOptions{
    /// <summary>再現テストを置く名前空間。<c>Test.System.Linq.Enumerable.FLinq</c> のテストプロジェクトに置く前提。</summary>
    public string Namespace{get;init;}="Test.System.Linq.Enumerable.FLinq.FailedQueries";

    /// <summary>失敗の状況(状態や食い違いの説明)。コメントとして先頭に書く。</summary>
    public string Note{get;init;}=string.Empty;

    public string TraitCategory{get;init;}="FuzzFailure";
}

/// <summary>
/// 失敗したクエリを、単独で動く xUnit の再現テストにする。
/// 生成クラス(<see cref="QuerySourceEmitter"/> の出力から ZLinq を除いたもの)をそのまま埋め込み、
/// System.Linq と FLinq(最適化なし・あり)の結果を <see cref="DynamicQueryComparer"/> で比べる。
/// 置き先のテストプロジェクトは Benchmark.Dynamic.Core を参照し、FLinq の生成器を有効にしておく。
/// 対応する Source は配列・List・IEnumerable・Range。永続系は単独では作れないので対象外。
/// </summary>
public static class ReproTestEmitter{
    public static string Emit(
        QueryIrDocument document,
        IReadOnlyList<int> primary,
        IReadOnlyList<int> secondary,
        ReproTestOptions? options=null){
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(secondary);
        options??=new();
        var emitted=new QuerySourceEmitter().Emit(document,new QuerySourceEmissionOptions{
            GeneratedNamespace=options.Namespace,
            ClassName="Probe",
            IncludeZLinq=false,
        });
        var newline=Environment.NewLine;
        var className="Repro_"+Sanitize(document.QueryId);
        var expression=QueryExpressionFormatter.Format(document).Replace("\r"," ").Replace("\n"," ");
        var builder=new StringBuilder(emitted.SourceText.Length+2_048);
        builder.Append(emitted.SourceText.Replace("using System.Linq;","using System.Linq;"+newline+"using Xunit;",StringComparison.Ordinal));
        builder.AppendLine();
        builder.Append("// ").AppendLine(expression);
        builder.Append("// source=").Append(document.SourceTarget).Append(" seed=").Append(document.Seed.ToCanonicalString()).AppendLine();
        foreach(var line in options.Note.Split('\n',StringSplitOptions.RemoveEmptyEntries)){
            builder.Append("// ").AppendLine(line.TrimEnd('\r'));
        }
        builder.Append("public sealed class ").Append(className).AppendLine("{");
        builder.Append("    [Fact,Trait(\"Category\",\"").Append(options.TraitCategory).AppendLine("\")]");
        builder.AppendLine("    public void FLinqはSystemLinqと同じ結果を返す(){");
        builder.Append("        var primary=").Append(EmitFixture(document.SourceTarget,primary)).AppendLine(";");
        builder.Append("        var secondary=").Append(EmitFixture(document.SourceTarget,secondary)).AppendLine(";");
        builder.AppendLine("        var expected=Probe.RunSystem(primary,secondary);");
        builder.AppendLine("        var unoptimized=global::Benchmark.Dynamic.Core.DynamicQueryComparer.Compare(expected,Probe.RunFLinqUnoptimized(primary,secondary));");
        builder.AppendLine("        var optimized=global::Benchmark.Dynamic.Core.DynamicQueryComparer.Compare(expected,Probe.RunFLinq(primary,secondary));");
        builder.AppendLine("        Assert.True(unoptimized.IsMatch,\"FLinq(最適化なし): \"+unoptimized.Difference);");
        builder.AppendLine("        Assert.True(optimized.IsMatch,\"FLinq(最適化あり): \"+optimized.Difference);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string EmitFixture(SourceTarget target,IReadOnlyList<int> values){
        switch(target.GetSourceKind()){
            case SourceKind.Array or SourceKind.IEnumerable:
                return $"new int[]{{{string.Join(",",values)}}}";
            case SourceKind.List:
                return $"new global::System.Collections.Generic.List<int>{{{string.Join(",",values)}}}";
            case SourceKind.Range:{
                if(values.Count==0){
                    return "new global::Benchmark.Dynamic.Core.RangeSourceFixture(0,0)";
                }
                for(var index=1;index<values.Count;index++){
                    if(values[index]!=values[0]+index){
                        throw new ArgumentException("A Range source requires contiguous values.",nameof(values));
                    }
                }
                return $"new global::Benchmark.Dynamic.Core.RangeSourceFixture({values[0]},{values.Count})";
            }
            default:
                throw new NotSupportedException($"A standalone reproduction is not available for {target}.");
        }
    }

    private static string Sanitize(string value){
        var builder=new StringBuilder(value.Length);
        foreach(var character in value){
            builder.Append(char.IsLetterOrDigit(character)||character=='_'?character:'_');
        }
        if(builder.Length==0||char.IsDigit(builder[0])){
            builder.Insert(0,'_');
        }
        return builder.ToString();
    }
}
