using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Seamless.Management;

namespace Management.Tests;

public sealed class ManagedTableCompoundAssignments{
    [Fact]
    public void NewTablesAndUpgradedGettersCompileWithAllCompoundAssignments(){
        var definition=Seamless.Management.DatabaseDesigner.EmptyManagedDatabase("compound","CompoundDb");
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Items","",[new("Id","int",true)],[]));
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Other","",[new("Id","int",true)],[]));
        var created=definition.Source.Files.Single();
        Assert.Contains("set=>this.Items.Assign(value);",created.Text);
        var legacy=created with{Text=created.Text
            .Replace("Items{get=>","Items=>",StringComparison.Ordinal)
            .Replace("set=>this.Items.Assign(value);}","",StringComparison.Ordinal)
            .Replace("Other{get=>","Other=>",StringComparison.Ordinal)
            .Replace("set=>this.Other.Assign(value);}"," // Keep this comment",StringComparison.Ordinal)};
        definition=definition with{Source=definition.Source with{Files=[legacy]}};
        var current=Seamless.Management.DatabaseDesigner.Read(definition,"Items");
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,current with{Name="Renamed"});
        var upgraded=definition.Source.Files.Single();
        Assert.Contains("set=>this.Renamed.Assign(value);",upgraded.Text);
        Assert.Contains("(row=>row.Key)",upgraded.Text);
        Assert.Contains("set=>this.Other.Assign(value);} // Keep this comment",upgraded.Text);
        var combined=upgraded with{Text=upgraded.Text+"""
            namespace CompoundDb{
                public static class Probe{
                    public static void Update(Database db,Tables.Items[] rows){
                        db.Renamed += rows;
                        db.Renamed -= rows;
                        db.Renamed &= rows;
                    }
                    public static void Update(Database.TypedTransaction tx,Tables.Items[] rows){
                        tx.Renamed += rows;
                        tx.Renamed -= rows;
                        tx.Renamed &= rows;
                    }
                }
            }
            """};
        using var result=new Seamless.Management.Server.RuntimeCompiler().Compile(definition.Source with{Files=[combined]});
        Assert.DoesNotContain(result.Result.Diagnostics,item=>item.Severity=="Error");
    }
}

// 設計器の入力の検査：名前 Database は入口の Database と重なるので使えない(DB生成器の SDB002 と同じ)。
// ビュー・関数の本体は return 文1つ(式1つ)。新しく作るときの仮の本体(throw 文1つ)は認める。
// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の M5・M6。
public sealed partial class DatabaseDesigner{
    [Fact]
    public void ManagedDatabasesKeepTheirDeclarationsInOneNamedFile(){
        foreach(var name in new[]{"db1","db2"}){
            var definition=Seamless.Management.DatabaseDesigner.EmptyManagedDatabase(name,name);
            definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Items","Catalog",[new("Id","int",true)],[]));
            definition=Seamless.Management.DatabaseDesigner.CreateStarterView(definition,"ItemsView","",row=>"return global::Seamless.Linq.Set.Empty<"+row+">();");
            definition=Seamless.Management.DatabaseDesigner.ApplyQuery(definition,new("Find","",DatabaseQueryKind.ScalarFunction,[],[],"int","return 1;"));
            var file=Assert.Single(definition.Source.Files);
            Assert.Equal(name+".cs",file.Path);
            Assert.Equal(name+".Database",definition.EntryType);
            Assert.Contains("Catalog.Items",Seamless.Management.DatabaseDesigner.Tables(definition));
            using var compiled=new Seamless.Management.Server.RuntimeCompiler().Compile(definition.Source);
            Assert.DoesNotContain(compiled.Result.Diagnostics,diagnostic=>diagnostic.Severity=="Error");
            var renamed=Seamless.Management.DatabaseDesigner.PrepareManagedDatabase(definition,"Renamed");
            Assert.Equal("Renamed.cs",Assert.Single(renamed.Source.Files).Path);
            using var renamedBuild=new Seamless.Management.Server.RuntimeCompiler().Compile(renamed.Source);
            Assert.DoesNotContain(renamedBuild.Result.Diagnostics,diagnostic=>diagnostic.Severity=="Error");
            var recreated=Seamless.Management.DatabaseDesigner.RemoveQuery(definition,"ItemsView");
            recreated=Seamless.Management.DatabaseDesigner.CreateStarterView(recreated,"ItemsView","",row=>"return global::Seamless.Linq.Set.Empty<"+row+">();");
            Assert.Equal(name+".cs",Assert.Single(recreated.Source.Files).Path);
        }
        var legacy=Seamless.Management.DatabaseDesigner.Empty("legacy","222");
        legacy=Seamless.Management.DatabaseDesigner.Apply(legacy,new("Rows","",[new("Id","int",true)],[]));
        var converted=Seamless.Management.DatabaseDesigner.PrepareManagedDatabase(legacy,"Migrated");
        Assert.Equal("Migrated.cs",Assert.Single(converted.Source.Files).Path);
        Assert.Equal(new[]{"Rows"},Seamless.Management.DatabaseDesigner.Tables(converted));
        Assert.False(Seamless.Management.DatabaseDesigner.IsManagedDatabaseName("222"));
        Assert.False(Seamless.Management.DatabaseDesigner.IsManagedDatabaseName("CON"));
        Assert.False(Seamless.Management.DatabaseDesigner.IsManagedDatabaseName("class"));
    }
    [Fact]
    public void DatabaseSourceShowsTheWholeFileAndTableSourceKeepsItsDeclarationContexts(){
        var definition=Seamless.Management.DatabaseDesigner.EmptyManagedDatabase("preview","Preview");
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Rows","",[new("Id","int",true)],[]));
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Other","",[new("Id","int",true)],[]));
        definition=Seamless.Management.DatabaseDesigner.CreateStarterView(definition,"ListRows","",row=>"return global::Seamless.Linq.Set.Empty<"+row+">();");
        var file=Assert.Single(definition.Source.Files);
        var complete=Assert.Single(Seamless.Management.DatabaseDesigner.ReadStructureClassParts(definition,null)).Text;
        var databaseScope=Assert.Single(SyntaxFactory.ParseCompilationUnit(complete).Members.OfType<NamespaceDeclarationSyntax>(),scope=>scope.Name.ToString()=="Preview");
        var allTables=Assert.Single(databaseScope.Members.OfType<NamespaceDeclarationSyntax>(),scope=>scope.Name.ToString()=="Tables");
        Assert.Equal(new[]{"Rows","Other"},allTables.Members.OfType<StructDeclarationSyntax>().Select(row=>row.Identifier.ValueText));
        Assert.Contains("ListRows",complete);
        Assert.Equal(file.Text,Assert.Single(definition.Source.Files).Text);
        var target=Seamless.Management.DatabaseDesigner.Objects(definition).Single(item=>item.Kind=="Table"&&item.Id=="Rows");
        var design=Seamless.Management.DatabaseDesigner.ReadObjectDefinition(definition,target);
        var preview=Assert.Single(Seamless.Management.DatabaseDesigner.ReadTableSourceParts(definition.Source,design)).Text;
        var previewScope=Assert.IsType<NamespaceDeclarationSyntax>(Assert.Single(SyntaxFactory.ParseCompilationUnit(preview).Members));
        Assert.Equal("Preview",previewScope.Name.ToString());
        var databaseClass=Assert.Single(previewScope.Members.OfType<ClassDeclarationSyntax>());
        Assert.Equal("Database",databaseClass.Identifier.ValueText);
        Assert.Equal("Rows",Assert.Single(databaseClass.Members.OfType<PropertyDeclarationSyntax>()).Identifier.ValueText);
        var selectedTables=Assert.Single(previewScope.Members.OfType<NamespaceDeclarationSyntax>());
        Assert.Equal("Tables",selectedTables.Name.ToString());
        Assert.Equal("Rows",Assert.IsType<StructDeclarationSyntax>(Assert.Single(selectedTables.Members)).Identifier.ValueText);
        Assert.Contains("row.Key",preview);
        Assert.DoesNotContain("Other",preview);
        Assert.DoesNotContain("ListRows",preview);

        var property=Assert.Single(design.Parts,part=>part.Role=="Member");
        var generated=definition with{Source=definition.Source with{Files=[file with{Text=file.Text.Remove(property.Start,property.Length)}]}};
        var generatedDesign=Seamless.Management.DatabaseDesigner.ReadObjectDefinition(generated,target);
        var authored=Assert.Single(Seamless.Management.DatabaseDesigner.ReadTableSourceParts(generated.Source,generatedDesign)).Text;
        Assert.Contains("struct Rows",authored);
        Assert.DoesNotContain("throw null!",authored);
    }

    [Fact]
    public void DatabaseSourcePreviewFormatsOldWhitespaceAndPreservesTheStoredText(){
        const string original="""
            namespace Aa{
            // Keep this comment
            public sealed partial class Database{
            public string Sample=>"  a  b  ";

            public int t0=>0;


            public int t1=>1;
            }
            }
            """;
        var definition=Seamless.Management.DatabaseDesigner.EmptyManagedDatabase("format","Aa");
        definition=definition with{Source=definition.Source with{Files=[new("Aa.cs",original)]}};
        var preview=Assert.Single(Seamless.Management.DatabaseDesigner.ReadStructureClassParts(definition,null)).Text;
        Assert.Contains("\n    public sealed partial class Database{",preview);
        Assert.Contains("\n        public int t0=>0;\n        public int t1=>1;",preview);
        Assert.Contains("""public string Sample=>"  a  b  ";""",preview);
        Assert.Contains("// Keep this comment",preview);
        Assert.DoesNotContain("\n\n",preview);
        Assert.Equal(original,Assert.Single(definition.Source.Files).Text);

        var directive="#if KEEP\n"+original+"\n#endif\n";
        var conditional=definition with{Source=definition.Source with{Files=[new("Aa.cs",directive)],Defines=["KEEP"]}};
        Assert.Equal(directive,Assert.Single(Seamless.Management.DatabaseDesigner.ReadStructureClassParts(conditional,null)).Text);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ManagerAppendsGeneratedMembersWithLocalIndentationAndLineEndings(string newline){
        var definition=Seamless.Management.DatabaseDesigner.EmptyManagedDatabase("append","Aa");
        var file=Assert.Single(definition.Source.Files);
        var suffix="        // Keep the spaces  \n        private const string Example=\"  a  b  \";\n        #if KEEP\n        private const int Conditional=1;\n        #endif\n";
        var text=file.Text.Replace("    }",suffix+"\n    }",StringComparison.Ordinal).ReplaceLineEndings(newline);
        definition=definition with{Source=definition.Source with{Files=[file with{Text=text}],Defines=["KEEP"]}};
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("t0","",[new("Id","int",true)],[]));
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("t1","",[new("Id","int",true)],[]));
        definition=Seamless.Management.DatabaseDesigner.AddSchema(definition,"Catalog");
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Items","Catalog",[new("Id","int",true)],[]));

        var stored=Assert.Single(definition.Source.Files).Text;
        var normalized=stored.ReplaceLineEndings("\n");
        Assert.Contains(suffix,normalized);
        Assert.Contains("#endif\n        public global::",normalized);
        Assert.Contains(" t0{get=>this.Table<",normalized);
        Assert.Contains(" t1{get=>this.Table<",normalized);
        Assert.Contains("set=>this.t0.Assign(value);}\n        public global::Seamless.Database.Table<global::Aa.Keys.t1",normalized);
        Assert.Contains("        public global::Aa.Schema_Catalog Catalog=>new(this);\n    }",normalized);
        Assert.Contains("\n        public global::Seamless.Database.Table<global::Aa.Keys.Catalog_Items",normalized);
        Assert.Equal(normalized.ReplaceLineEndings(newline),stored);
    }

    [Fact]public void TheNameDatabaseIsRejectedForTablesSchemasViewsAndFunctions(){
        var definition=Seamless.Management.DatabaseDesigner.Empty("names","Names");
        Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.Apply(definition,new("Database","",[new("Id","int",true)],[]))).Code);
        Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.Apply(definition,new("Items","Database",[new("Id","int",true)],[]))).Code);
        Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.AddSchema(definition,"Database")).Code);
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Items","",[new("Id","int",true)],[]));
        Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.ApplyQuery(definition,
            new("Database","",DatabaseQueryKind.ScalarFunction,[],[],"int","return 1;"))).Code);
    }

    [Fact]public void ViewAndFunctionBodiesMustBeASingleReturn(){
        var definition=Seamless.Management.DatabaseDesigner.Apply(Seamless.Management.DatabaseDesigner.Empty("bodies","Bodies"),new("Items","",[new("Id","int",true)],[]));
        var twoStatements=new QueryDesign("Count","",DatabaseQueryKind.ScalarFunction,[],[],"int","var items=q.Database.Items;\nreturn items.Count;");
        Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.ApplyQuery(definition,twoStatements)).Code);
        // return 文1つは、ルートからたどる書き方でも短い書き方でも受け付ける。
        Seamless.Management.DatabaseDesigner.ApplyQuery(definition,twoStatements with{Body="return q.Database.Items.Count;"});
        Seamless.Management.DatabaseDesigner.ApplyQuery(definition,twoStatements with{Name="Count2",Body="return q.Items.Count;"});
        // 新しく作るときの仮の本体。
        Seamless.Management.DatabaseDesigner.ApplyQuery(definition,twoStatements with{Name="Draft",Body="throw new global::System.NotSupportedException();"});
    }
}
