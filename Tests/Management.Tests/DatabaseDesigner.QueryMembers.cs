using Seamless.Management;
using Seamless.Management.Server;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

/// <summary>
/// 設計器が扱うビュー・関数の形: 生成される入口(IReader・I&lt;スキーマ&gt;Reader)への拡張メンバー。
/// ビューは ISet&lt;行&gt; を返すプロパティ、テーブル関数は ISet&lt;行&gt; を返すメソッド、スカラー関数は値を返すメソッド。
/// </summary>
public sealed class DatabaseDesignerQueryMembers{
    private static Definition WithItems(string schema){
        var definition=Designer.Empty("members","Members");
        if(schema.Length>0){definition=Designer.AddSchema(definition,schema);}
        return Designer.Apply(definition,new("Items",schema,[new("Id","int",true),new("Value","decimal",false)],[]));
    }

    [Theory][InlineData("")][InlineData("dbo0")]
    public void CreatedViewsAndFunctionsAreExtensionMembersOfTheGeneratedReader(string schema){
        var definition=WithItems(schema);
        definition=Designer.ApplyQuery(definition,new("Active",schema,DatabaseQueryKind.View,[new("Id","int",false)],[],"","return q.Items.Where(row=>row.Value>0).Select(row=>new Active(row.Id));"));
        definition=Designer.ApplyQuery(definition,new("Find",schema,DatabaseQueryKind.TableFunction,[new("Id","int",false)],[new("minimum","decimal")],"ISet<FindRow>","return q.Items.Where(row=>row.Value>=minimum).Select(row=>new FindRow(row.Id));"));
        definition=Designer.ApplyQuery(definition,new("Amount",schema,DatabaseQueryKind.ScalarFunction,[],[new("id","int")],"decimal","return q.Items.Where(row=>row.Id==id).Sum(row=>row.Value);"));
        var reader=schema.Length==0?"IReader":"I"+schema+"Reader";
        var view=definition.Source.Files.Single(file=>file.Path==(schema.Length==0?"Queries/Active.cs":"Queries/"+schema+"/Active.cs")).Text;
        Assert.Contains("extension(global::UserCode.ManagedDb."+reader+" q)",view);
        Assert.Contains("ISet<global::UserCode.Views"+(schema.Length==0?"":"."+schema)+".@Active> @Active\n",view);
        Assert.Contains("\n                get\n                {\n                    return ",view);
        // 結果の行型は Views・Functions に、キーを持たない行として置く。
        Assert.Contains(definition.Source.Files,file=>file.Path==(schema.Length==0?"Views/Active.cs":"Views/"+schema+"/Active.cs")&&file.Text.Contains("record struct @Active(int @Id)"));
        Assert.Contains(definition.Source.Files,file=>file.Path==(schema.Length==0?"Functions/FindRow.cs":"Functions/"+schema+"/FindRow.cs"));
        var prefix=schema.Length==0?"":schema+".";
        var objects=Designer.Objects(definition);
        Assert.Equal("View",Assert.Single(objects,item=>item.Id==prefix+"Active").Kind);
        Assert.Equal("TableFunction",Assert.Single(objects,item=>item.Id==prefix+"Find").Kind);
        Assert.Equal("ScalarFunction",Assert.Single(objects,item=>item.Id==prefix+"Amount").Kind);
        {
            //読み直すと、作ったときの本体・列・引数・戻り型が戻る
            var read=Designer.ReadQuery(definition,prefix+"Find");
            Assert.Equal(DatabaseQueryKind.TableFunction,read.Kind);
            Assert.Contains("q.Items.Where(row=>row.Value>=minimum)",read.Body);
            Assert.Equal([new("Id","int",false,"Id")],read.Columns);
            Assert.Equal([new ParameterDesign("minimum","decimal")],read.Parameters);
            Assert.StartsWith("global::Seamless.Collections.ISet<",read.ReturnType);
            Assert.Equal("decimal",Designer.ReadQuery(definition,prefix+"Amount").ReturnType);
        }
        {
            //作ったソースはコンパイルでき、名前で呼ぶ入口が目録と同じ名前で並ぶ
            using var compiled=new RuntimeCompiler().Compile(definition.Source);
            Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        }
    }

    [Fact]
    public void TheStarterViewShowsBothWaysToReadATableInASchema(){
        var definition=WithItems("dbo0");
        var starter=Seamless.Management.Components.管理ワークスペース.新規ビュー本体("Example");
        definition=Designer.ApplyQuery(definition,new("Example","",DatabaseQueryKind.View,[new("Id","int",false)],[],"",starter));
        var generated=definition.Source.Files.Single(file=>file.Path=="Queries/Example.cs").Text;
        Assert.Contains("\n                    // return q.スキーマ名.テーブル名.Select",generated);
        Assert.Contains("\n                    // return from a in q.スキーマ名.テーブル名",generated);
        Assert.Contains("\n                    return global::Seamless.Linq.Set.Empty",generated);
        definition=Designer.ApplyQuery(definition,new("MethodExample","",DatabaseQueryKind.View,[new("Id","int",false)],[],"","return q.dbo0.Items.Select(row => new MethodExample(row.Id));"));
        definition=Designer.ApplyQuery(definition,new("QueryExample","",DatabaseQueryKind.View,[new("Id","int",false)],[],"","return from a in q.dbo0.Items select new QueryExample(a.Id);"));
        using var compiled=new RuntimeCompiler().Compile(definition.Source);
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
    }

    [Fact]
    public void RenamingQueriesUpdatesOnlyBoundReferencesAndTheirOwnResultType(){
        var definition=WithItems("dbo0");
        definition=Designer.ApplyQuery(definition,new("Recent","dbo0",DatabaseQueryKind.View,[new("Id","int",false)],[],"","return q.Items.Select(row => new Recent(row.Id));"));
        definition=Designer.ApplyQuery(definition,new("Total","dbo0",DatabaseQueryKind.ScalarFunction,[],[new("id","int")],"int","return id;"));
        definition=Designer.ApplyQuery(definition,new("Following","",DatabaseQueryKind.ScalarFunction,[],[],"int","return q.dbo0.Recent.Count + q.dbo0.Total(1);"));
        var compiler=new RuntimeCompiler();
        definition=Designer.RenameQuery(definition,"dbo0.Recent","Latest",compiler.CompileSchemaForEditing(definition.Source));
        definition=Designer.RenameQuery(definition,"dbo0.Total","Quantity",compiler.CompileSchemaForEditing(definition.Source));
        Assert.Contains("q.dbo0.@Latest",Designer.ReadQuery(definition,"Following").Body);
        Assert.Contains("q.dbo0.@Quantity(1)",Designer.ReadQuery(definition,"Following").Body);
        Assert.Contains("new @Latest(",Designer.ReadQuery(definition,"dbo0.Latest").Body);
        Assert.Contains(definition.Source.Files,file=>file.Path=="Views/dbo0/Latest.cs");
        using var compiled=compiler.Compile(definition.Source);
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
    }

    [Fact]
    public void BodyEditsReplaceOnlyTheBodyAndKeepTheDeclarationAroundIt(){
        var definition=WithItems("");
        definition=Designer.ApplyQuery(definition,new("Amount","",DatabaseQueryKind.ScalarFunction,[],[new("id","int")],"decimal","return q.Items.Where(row=>row.Id==id).Sum(row=>row.Value);"));
        var path=definition.Source.Files.Single(file=>file.Path=="Queries/Amount.cs").Path;
        // 利用者が書き足した属性・コメント・ほかのメンバーは残す。
        definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>file.Path!=path?file:file with{Text=file.Text.Replace("public decimal @Amount(","/* keep comment */[global::System.Diagnostics.DebuggerStepThrough] public decimal @Amount(",StringComparison.Ordinal).Replace("extension(","public static int Helper()=>1;extension(",StringComparison.Ordinal)}).ToArray()}};
        var read=Designer.ReadQuery(definition,"Amount");
        var updated=Designer.ApplyQuery(definition,read with{Body="/* new body */return id;"});
        var text=updated.Source.Files.Single(file=>file.Path==path).Text;
        Assert.Contains("/* keep comment */[global::System.Diagnostics.DebuggerStepThrough] public decimal @Amount(int @id)",text);
        Assert.Contains("public static int Helper()=>1;",text);
        Assert.Contains("/* new body */return id;",text);
        Assert.DoesNotContain("Sum(row=>row.Value)",text);
        Assert.Contains("/* new body */return id;",Designer.ReadQuery(updated,"Amount").Body);
    }

    [Fact]
    public void StructureEditsChangeTheRowColumnsParametersAndScalarReturnType(){
        var definition=WithItems("");
        definition=Designer.ApplyQuery(definition,new("Find","",DatabaseQueryKind.TableFunction,[new("Id","int",false)],[new("minimum","decimal")],"ISet<FindRow>","return q.Items.Where(row=>row.Value>=minimum).Select(row=>new FindRow(row.Id));"));
        definition=Designer.ApplyQuery(definition,new("Amount","",DatabaseQueryKind.ScalarFunction,[],[new("id","int")],"decimal","return q.Items.Where(row=>row.Id==id).Sum(row=>row.Value);"));
        {
            //テーブル関数の結果の行型に列を足し、引数の型を変える(本体はそのまま)
            var find=Designer.ReadQuery(definition,"Find");
            var updated=Designer.ApplyQuery(definition,find with{Columns=[..find.Columns,new("Value","decimal",false)],Parameters=[new("minimum","double")]});
            Assert.Contains("record struct FindRow(int Id,decimal Value)",updated.Source.Files.Single(file=>file.Path=="Functions/FindRow.cs").Text.Replace("@","",StringComparison.Ordinal));
            Assert.Contains("Find(double minimum)",updated.Source.Files.Single(file=>file.Path=="Queries/Find.cs").Text.Replace("@","",StringComparison.Ordinal));
        }
        {
            //スカラー関数の戻り型を変える
            var amount=Designer.ReadQuery(definition,"Amount");
            var updated=Designer.ApplyQuery(definition,amount with{ReturnType="double"});
            Assert.Contains("public double @Amount(",updated.Source.Files.Single(file=>file.Path=="Queries/Amount.cs").Text);
            Assert.Equal("double",Designer.ReadQuery(updated,"Amount").ReturnType);
        }
    }

    [Fact]
    public void ViewsAndFunctionsCannotChangeTheirKindOrTakeTheReceiverName(){
        var definition=WithItems("");
        definition=Designer.ApplyQuery(definition,new("Amount","",DatabaseQueryKind.ScalarFunction,[],[],"decimal","return 0m;"));
        var amount=Designer.ReadQuery(definition,"Amount");
        Assert.Equal("Unsupported",Assert.Throws<ManagementException>(()=>Designer.ApplyQuery(definition,amount with{Kind=DatabaseQueryKind.TableFunction,Columns=[new("Id","int",false)],ReturnType="ISet<AmountRow>"})).Code);
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>Designer.ApplyQuery(definition,amount with{Kind=DatabaseQueryKind.View,Columns=[new("Id","int",false)],ReturnType=""})).Code);
        // 本体は受け手を q で読むので、引数に q は使えない。
        Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>Designer.ApplyQuery(definition,new("Other","",DatabaseQueryKind.ScalarFunction,[],[new("q","int")],"int","return q;"))).Code);
    }
}
