namespace Seamless.Database.Rdbms;

// [取込/ビューの訳] T-SQL 翻訳器の段階4(Document/refactoring-designs/20261115-T-SQL翻訳器.md)。
// 取り込むビューは訳して C# のビュー(読み取りの入口への拡張プロパティ)にする。訳せないビューは黙って写しにせず、
// 取り込まずに回避策つきで報告する。写しは利用者が明示して選んだビューだけ(Document/refactoring-designs/20261117-外部DBとファイルの取り込みと書き出し.md)。
public static partial class CSharpDatabaseGenerator{
    private sealed class ViewPlan(HashSet<string> snapshots,GeneratedSourceFile? source,IReadOnlyList<CompatibilityIssue> issues,IReadOnlyList<GeneratedViewBinding> views){
        public IReadOnlyCollection<string> Snapshots=>snapshots;
        public GeneratedSourceFile? Source=>source;
        public IReadOnlyList<CompatibilityIssue> Issues=>issues;
        public IReadOnlyList<GeneratedViewBinding> Views=>views;
        public bool IsSnapshot(string schema,string name)=>snapshots.Contains(Key(schema,name));
    }

    private static string Key(string schema,string name)=>schema+"."+name;

    private static ViewPlan TranslateViews(DatabaseCatalog catalog,IReadOnlyList<SchemaName> schemas,string typeNamespace,string containerType,ViewImportOptions options){
        var snapshots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var name in options.SnapshotViews){
            if(!schemas.Any(schema=>schema.Views.Any(view=>string.Equals(Key(schema.Source.Name,view.Source.Name),name,StringComparison.OrdinalIgnoreCase)))){
                throw new ArgumentException($"写しにするビュー '{name}' が取り込み元にありません。","options");
            }
            snapshots.Add(name);
        }
        var collation=catalog.DefaultCollation??throw new InvalidOperationException($"DB '{catalog.Name}' の既定の照合順序が分からないため、ビューを訳せません。");
        var schemaIdentifiers=schemas.ToDictionary(static schema=>schema.Source.Name,static schema=>FileName(schema.Name),StringComparer.OrdinalIgnoreCase);
        // FROM に書ける表とビュー。写しにするビューは表として読む。
        var relations=new List<ViewTranslationRelation>();
        foreach(var schema in schemas){
            foreach(var table in schema.Tables){
                relations.Add(Relation(schema,table.Source.Name,table.Name,$"global::{typeNamespace}.Tables.{FileName(schema.Name)}.{FileName(table.Name)}",table.Columns,false));
            }
            foreach(var view in schema.Views){
                var snapshot=snapshots.Contains(Key(schema.Source.Name,view.Source.Name));
                var kind=snapshot?"Tables":"Views";
                relations.Add(Relation(schema,view.Source.Name,view.Name,$"global::{typeNamespace}.{kind}.{FileName(schema.Name)}.{FileName(view.Name)}",view.Columns,!snapshot));
            }
        }
        var viewsByKey=schemas.SelectMany(static schema=>schema.Views.Select(view=>(Key:Key(schema.Source.Name,view.Source.Name),Schema:schema.Source.Name,View:view.Source)))
            .ToDictionary(static item=>item.Key,static item=>item,StringComparer.OrdinalIgnoreCase);
        var candidates=viewsByKey.Values.Where(item=>!snapshots.Contains(item.Key)).Select(static item=>new ViewTranslationTarget(item.Schema,item.View)).ToList();
        var issues=new List<CompatibilityIssue>();
        // 訳せないビューを使うビューは、訳せても C# としてコンパイルできないので取り込めない。
        // 訳し直すたびに使えるビューが減るので、変わらなくなるまで繰り返す。
        while(true){
            var result=options.Translator.Translate(new(typeNamespace,containerType,collation,schemaIdentifiers,relations,candidates));
            var translated=new HashSet<string>(result.Translated.Select(static target=>Key(target.Schema,target.View.Name)),StringComparer.OrdinalIgnoreCase);
            var blocked=new List<(ViewTranslationTarget Target,string Dependency)>();
            foreach(var target in result.Translated){
                foreach(var dependency in target.View.Dependencies??[]){
                    if(viewsByKey.ContainsKey(dependency)&&!translated.Contains(dependency)&&!snapshots.Contains(dependency)){
                        blocked.Add((target,dependency));
                        break;
                    }
                }
            }
            if(blocked.Count==0){
                issues.AddRange(result.Issues);
                foreach(var name in snapshots.Order(StringComparer.OrdinalIgnoreCase)){
                    var item=viewsByKey[name];
                    issues.Add(new(CompatibilityObjectKind.View,item.Schema,item.View.Name,null,"SNAPSHOT",
                        "取り込んだ時点の結果の写し(表)として残した。元の表を変えても、このビューの中身は変わらない。",[]));
                }
                // 訳したビューの置き場所。翻訳器は Queries.<スキーマ>Views に、ビューの名前の拡張プロパティとして出す。
                var bindings=result.Translated.Select(target=>{
                    var schema=schemas.Single(schema=>string.Equals(schema.Source.Name,target.Schema,StringComparison.Ordinal));
                    var view=schema.Views.Single(view=>ReferenceEquals(view.Source,target.View));
                    return new GeneratedViewBinding(target.Schema,target.View.Name,FileName(schema.Name),$"{typeNamespace}.Queries.{FileName(schema.Name)}Views",FileName(view.Name));
                }).ToArray();
                return new(snapshots,result.Source,issues,bindings);
            }
            foreach(var (target,dependency) in blocked){
                issues.Add(new(CompatibilityObjectKind.View,target.Schema,target.View.Name,null,"DEPENDENCY",
                    $"使っているビュー '{dependency}' を取り込めないため、このビューも取り込めない。",
                    [
                        new("FixDependency",$"'{dependency}' の回避策を選ぶと、このビューも訳せるようになる。"),
                        new("Snapshot","取り込んだ時点の結果を表(写し)として残す。元の表を変えても中身は変わらない。"),
                    ]));
                candidates.Remove(target);
            }
        }
    }

    private static ViewTranslationRelation Relation(SchemaName schema,string sourceName,string name,string rowType,IReadOnlyList<ColumnName> columns,bool isView)=>
        new(schema.Source.Name,sourceName,$"Database.{FileName(schema.Name)}.{FileName(name)}",rowType,
            columns.Where(static column=>!column.Source.IsSynthetic).Select(static column=>new ViewTranslationColumn(column.Source.Name,FileName(column.Name),column.Source.Type)).ToArray(),isView);

    // 中身を訳していない関数と手続きは、呼ぶと NotSupportedException になる。下書きは生成した呼び口そのもの。
    private static IEnumerable<CompatibilityIssue> RoutineIssues(IReadOnlyList<GeneratedRoutineBinding> routines){
        foreach(var routine in routines.Where(static routine=>routine.IsStub)){
            var kind=routine.Kind switch{
                RoutineKind.ScalarFunction=>CompatibilityObjectKind.ScalarFunction,
                RoutineKind.TableFunction=>CompatibilityObjectKind.TableFunction,
                _=>CompatibilityObjectKind.StoredProcedure,
            };
            yield return new(kind,routine.SourceSchemaName,routine.SourceName,null,"ROUTINE_BODY",
                "中身を C# に訳していないため、呼ぶと NotSupportedException になる。",
                [new("ManualCSharp",$"{routine.DeclaringTypeName}.{routine.MethodName} の中身を C# で書く。呼び口は生成してある。",routine.CSharpSource.Trim())]);
        }
    }
}
