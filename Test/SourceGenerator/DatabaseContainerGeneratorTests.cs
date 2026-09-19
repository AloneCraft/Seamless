using Microsoft.CodeAnalysis;

namespace Test.SourceGenerator;

public sealed class DatabaseContainerGeneratorTests {
    [Fact,Trait("Category","Unit")]
    public void MarkerControlsGeneration() {
        {
            //マーカーと同じ名前空間にあるDatabaseContainerを生成対象にする
            const string source = """
                namespace Sample;
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                    public global::Seamless.Database.Table<int> Items=>this.Table<int>();
                }
                """;
            var result=Run(source);
            Assert.Contains(result.GeneratedSources,static generated=>generated.HintName.Contains("Sample.Catalog.Container.g.cs",StringComparison.Ordinal));
        }
        {
            //マーカーがない名前空間には何も生成しない
            const string source = """
                namespace Sample;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                    public global::Seamless.Database.Table<int> Items=>this.Table<int>();
                }
                """;
            var result=Run(source);
            Assert.Empty(result.GeneratedSources);
            Assert.Empty(result.Diagnostics);
        }
        {
            //マーカーの子名前空間にあるDatabaseContainerは生成対象にしない
            const string source = """
                namespace Sample {
                    internal static class DatabaseGenerator;
                }
                namespace Sample.Child {
                    public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                        public global::Seamless.Database.Table<int> Items=>this.Table<int>();
                    }
                }
                """;
            var result=Run(source);
            Assert.DoesNotContain(result.GeneratedSources,static generated=>generated.HintName.Contains("Catalog.Container.g.cs",StringComparison.Ordinal));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void DatabaseObjectsGeneratedAsSystemTable() {
        const string source = """
            namespace Sample;
            internal static partial class DatabaseGenerator;
            public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                public global::Seamless.Database.Table<int> Items=>this.Table<int>();
                public global::Seamless.Database.View<string> Names()=>this.View(static()=>[]);
                public global::Seamless.Database.TableFunction<int> Find(int minimum)=>this.TableFunction(()=>[]);
                public global::Seamless.Database.ScalarFunction<int> Total()=>this.ScalarFunction(static()=>0);
                public global::Seamless.Database.StoredProcedure<int> Rebuild()=>this.StoredProcedure(static()=>0);
                public int Count()=>0;
            }
            """;
        var result=Run(source);
        var generated=Assert.Single(result.GeneratedSources,static generated=>generated.HintName.Contains("Catalog.Container.g.cs",StringComparison.Ordinal)).SourceText.ToString();
        Assert.Contains("SystemTable",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.Table",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.View",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.TableFunction",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.ScalarFunction",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.StoredProcedure",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.SystemTable",generated,StringComparison.Ordinal);
        Assert.Contains("typeof(int)",generated,StringComparison.Ordinal);
        Assert.DoesNotContain("""new(new("Count")""",generated,StringComparison.Ordinal);
        Assert.DoesNotContain("SqlName",generated,StringComparison.Ordinal);
    }

    [Fact,Trait("Category","Unit")]
    public void DuplicateObjectNameReportsSdb001() {
        const string source = """
            namespace Sample;
            internal static class DatabaseGenerator;
            public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                public global::Seamless.Database.TableFunction<int> Find(int minimum)=>this.TableFunction(()=>[]);
                public global::Seamless.Database.TableFunction<int> Find(string minimum)=>this.TableFunction(()=>[]);
            }
            """;
        var result=Run(source);
        Assert.Contains(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB001"&&diagnostic.Severity==DiagnosticSeverity.Error);
    }

    [Fact,Trait("Category","Unit")]
    public void ReservedDatabaseObjectsNameReportsSdb002() {
        const string source = """
            namespace Sample;
            internal static class DatabaseGenerator;
            public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                public new global::Seamless.Database.Table<int> DatabaseObjects=>this.Table<int>();
            }
            """;
        var result=Run(source);
        Assert.Contains(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB002"&&diagnostic.Severity==DiagnosticSeverity.Error);
    }

    [Fact,Trait("Category","Unit")]
    public void SchemaGraphUsesQualifiedObjectNames() {
        const string source = """
            namespace Sample {
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                    public Schemas.sales sales=>new(this);
                    public Schemas.inventory inventory=>new(this);
                }
            }
            namespace Sample.Schemas {
                public sealed class sales(global::Seamless.Database.DatabaseContainer database):global::Seamless.Database.DatabaseSchema(database,"sales") {
                    public global::Seamless.Database.Table<int> Product=>this.Table<int>();
                }
                public sealed class inventory(global::Seamless.Database.DatabaseContainer database):global::Seamless.Database.DatabaseSchema(database,"inventory") {
                    public global::Seamless.Database.Table<int> Product=>this.Table<int>();
                }
            }
            """;

        var result=Run(source);
        var generated=Assert.Single(result.GeneratedSources,static generated=>generated.HintName.Contains("Catalog.Container.g.cs",StringComparison.Ordinal)).SourceText.ToString();
        Assert.Contains("""new("sales.Product")""",generated,StringComparison.Ordinal);
        Assert.Contains("""new("inventory.Product")""",generated,StringComparison.Ordinal);
        Assert.Contains("this.sales.Product.Add(row)",generated,StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB001");
    }

    [Fact,Trait("Category","Unit")]
    public void SchemaRecordUsesArrayMemoryPackApis() {
        const string source = """
            namespace Sample {
                internal static class DatabaseGenerator;
            }
            namespace Sample.Records {
                public partial struct ImageRow {
                    public partial byte[]? Data { get; }
                }
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",source)]);
        GeneratorDriver driver=GeneratorTestHelpers.CreateDatabaseDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var outputCompilation,out var diagnostics);
        var result=driver.GetRunResult().Results.Single(static candidate=>
            candidate.GeneratedSources.Any(static generated=>generated.HintName=="Seamless.Database.Schema.g.cs"));
        var generated=Assert.Single(result.GeneratedSources).SourceText.ToString();

        Assert.Contains("writer.WriteArray(value._value0)",generated,StringComparison.Ordinal);
        Assert.Contains("reader.ReadArray(ref value._value0)",generated,StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics,static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error);
        Assert.DoesNotContain(outputCompilation.GetDiagnostics(),static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error);
    }

    private static GeneratorRunResult Run(string source) {
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",source)]);
        var driver=GeneratorTestHelpers.CreateDatabaseDriver().RunGenerators(compilation);
        var results=driver.GetRunResult().Results;
        Assert.Equal(2,results.Length);
        return results[0];
    }
}
