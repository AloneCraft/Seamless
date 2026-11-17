using Microsoft.Data.SqlClient;
using Seamless.Database.Rdbms;
using Seamless.Database.SqlTranslation;

namespace Seamless.Database.SqlServer;

/// <summary>
/// SQL Server のビューを、T-SQL 翻訳器(Seamless.Database.SqlTranslation)で C# のビューへ訳す。
/// 訳せないビューは、翻訳器の診断を「何が同じにならないか」と回避策の候補にして返す。
/// </summary>
public sealed class SqlServerViewTranslator:IViewTranslator{
    /// <inheritdoc/>
    // 訳の規則を変えたら上げる(生成物の指紋に入る)。
    public string Version=>"SqlViewTranslator-1";

    /// <inheritdoc/>
    public ViewTranslationResult Translate(ViewTranslationRequest request){
        ArgumentNullException.ThrowIfNull(request);
        var catalog=new TranslationCatalog(request.RootNamespace,request.ContainerType,request.DatabaseCollation,request.Relations.Select(Relation)){
            SchemaIdentifiers=request.SchemaIdentifiers,
        };
        var translator=new SqlViewTranslator(catalog);
        var translations=new List<ViewTranslation>();
        var translated=new List<ViewTranslationTarget>();
        var issues=new List<CompatibilityIssue>();
        foreach(var target in request.Views){
            var schemaReader=catalog.ReaderType(target.Schema);var schemaIdentifier=catalog.SchemaIdentifier(target.Schema);
            if(target.View.Definition is not{} definition){
                issues.Add(new(CompatibilityObjectKind.View,target.Schema,target.View.Name,null,"NO_DEFINITION",
                    "ビューの定義を読めない(暗号化されているか、権限が無い)ため、訳せない。",
                    [Snapshot(),new("ReadDefinition","定義を読める権限(VIEW DEFINITION)で接続し直すか、暗号化を外した定義を用意すると訳せる。")]));
                continue;
            }
            var translation=translator.Translate(target.Schema,target.View.Name,definition);
            if(translation.Succeeded){
                translations.Add(translation);
                translated.Add(target);
                // 訳せても、本物と同じにならない所(再現できない動きの模擬)は残った差として報告する。
                foreach(var warning in translation.Diagnostics.Where(static diagnostic=>diagnostic.Severity==TranslationSeverity.Warning)){
                    issues.Add(new(CompatibilityObjectKind.View,target.Schema,target.View.Name,null,warning.Code,warning.Message,[]));
                }
                continue;
            }
            var unsupported=translation.Diagnostics.First(static diagnostic=>diagnostic.Severity==TranslationSeverity.Unsupported);
            issues.Add(new(CompatibilityObjectKind.View,target.Schema,target.View.Name,null,unsupported.Code,
                $"{unsupported.Message}({unsupported.Line}行 {unsupported.Column}文字目)。このビューは訳せないため、取り込まない。",
                Workarounds(unsupported.Code,schemaReader,schemaIdentifier,target,definition)));
        }
        GeneratedSourceFile? source=null;
        if(translations.Count>0){
            source=new("Queries/Views.cs",translator.SourceFile(translations));
        }
        return new(translated,source,issues);
    }

    private static CatalogRelation Relation(ViewTranslationRelation relation)=>
        new(relation.Schema,relation.Name,relation.Access,relation.RowType,
            relation.Columns.Select(static column=>new CatalogColumn(column.Name,column.CSharpName,Type(column.Type),column.Type.IsNullable)).ToArray(),relation.IsView);

    // 取込の目録の型(SQL Server の型名は SqlTypeName、文字列の長さは文字数、max は長さ無し)を翻訳器の型にする。
    private static SqlTypeDescriptor Type(DatabaseType type){
        var name=type.SqlTypeName??throw new InvalidOperationException("SQL Server の型名の無い列は訳せない。");
        int? length=type.MaximumLength;
        if(length is null&&name is "varchar" or "nvarchar" or "varbinary" or "char" or "nchar" or "binary"){length=-1;}
        return SqlTypeDescriptor.Of(name,length,type.Precision,type.Scale,type.Collation);
    }

    private static CompatibilityWorkaround Snapshot()=>
        new("Snapshot","取り込んだ時点の結果を表(写し)として残す。元の表を変えても、写しの中身は変わらない。");

    // 訳せない構文ごとの書き直し方。どれも「同じ結果になる別の書き方」で、黙って当てはしない。
    private static IReadOnlyList<CompatibilityWorkaround> Workarounds(string code,string readerType,string schemaIdentifier,ViewTranslationTarget target,string definition){
        var list=new List<CompatibilityWorkaround>();
        var rewrite=code switch{
            "SQLT004"=>"共通テーブル式(WITH)を別のビューに分け、そのビューを FROM で使う形に書き直すと訳せる。",
            "SQLT030" or "SQLT033"=>"ビューの ORDER BY は SQL Server でも結果の順を保証しない。ORDER BY・TOP を外したビューにし、並べ替えと件数の制限は呼び出し側(OrderBy・Take)で書く。",
            "SQLT034"=>"FOR XML・FOR JSON を外したビューにし、XML や JSON は呼び出し側で組み立てる。",
            "SQLT037"=>"ROLLUP・CUBE は、集計の単位ごとのビューを UNION ALL でつなぐ形に書き直すと訳せる。",
            "SQLT050"=>"別の DB の表は訳せない。その表も同じ DB へ取り込んでから、同じ DB の表を使う形に書き直す。",
            "SQLT051"=>"使っている表かビューが取り込み元に無い。先にそれを取り込むか、定義を直す。",
            "SQLT063"=>"全文検索は再現しない。LIKE などの比較に書き直すと訳せるが、結果(順位や語の切り方)は変わる。",
            "SQLT069"=>"日付の足し引きを DATEADD・DATEDIFF に書き直すと訳せる。",
            "SQLT090"=>"ユーザー定義関数か XML のメソッドを呼んでいる。関数の中身を先に C# で書けば(関数の回避策)、それを使う形で手で書ける。",
            "SQLT091" or "SQLT092"=>"ウィンドウ関数・WITHIN GROUP はまだ訳せない。GroupBy と並べ替えを使う C# で手で書く。",
            "SQLT096" or "SQLT097" or "SQLT098" or "SQLT099"=>"この関数(またはこの使い方)の部品がまだ無い。Seamless.Database.SqlServer.Functions に部品を足すか、C# で手で書く。",
            "SQLT010" or "SQLT110" or "SQLT111"=>"この型(または型の変換)は扱えない。列を扱える型へ CAST したビューにすると訳せるが、値の範囲や精度が変わることがある。",
            _=>null,
        };
        if(rewrite is not null){list.Add(new("Rewrite",rewrite));}
        list.Add(new("ManualCSharp","C# のビューを手で書く。下書きの中身を、元の定義と同じ結果になるように書く。",
            $"// 元の定義(T-SQL)\n// {definition.Replace("\r\n","\n").Replace("\n","\n// ")}\npublic static partial class {schemaIdentifier}ManualViews{{\n    extension({readerType} q){{\n        public global::Seamless.Collections.ISet<行の型> {target.View.Name}=>throw new global::System.NotImplementedException();\n    }}\n}}"));
        list.Add(Snapshot());
        return list;
    }
}
