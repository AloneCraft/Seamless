$path = "Tests\Database.Compilation.Tests\Migration\SourceGenerator\DatabaseContainerGeneratorTests.cs"
$content = Get-Content $path -Raw

$insertCode = @"

    [Fact,Trait("Category","Unit")]
    public void IncrementalSyntaxAddition_GeneratesContainerPropertiesWithoutDiskSave(){
        const string initialSource="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",initialSource)]);
        var driver=GeneratorTestHelpers.CreateDatabaseDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var initialOutput,out _);

        // 未ビルド・未保存の状態でエディタ入力された構文木をメモリ上で追加
        const string ordersSource="""
            namespace Sample.Tables{
                public partial struct Orders{
                    [global::Seamless.Database.Key]
                    public partial int Id{get;init;}
                    public partial decimal Amount{get;init;}
                }
            }
            """;
        var ordersTree=Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(ordersSource,GeneratorTestHelpers.ParseOptions,path:"Orders.cs");
        var updatedCompilation=compilation.AddSyntaxTrees(ordersTree);

        driver.RunGeneratorsAndUpdateCompilation(updatedCompilation,out var finalOutput,out var diagnostics);
        Assert.DoesNotContain(diagnostics,static d=>d.Severity==Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.DoesNotContain(finalOutput.GetDiagnostics(),static d=>d.Severity==Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

        var catalogType=finalOutput.GetTypeByMetadataName("Sample.Catalog")!;
        Assert.NotNull(catalogType);
        var ordersProperty=catalogType.GetMembers("Orders").OfType<Microsoft.CodeAnalysis.IPropertySymbol>().FirstOrDefault();
        Assert.NotNull(ordersProperty);
        Assert.Equal("Orders",ordersProperty.Name);
        Assert.Contains("Table",ordersProperty.Type.ToDisplayString());
    }

    [Fact,Trait("Category","Unit")]
    public void TableAndViewModifications_IncrementallyUpdateSymbols(){
        const string baseSource="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
            }
            namespace Sample.Tables{
                public partial struct Items{
                    [global::Seamless.Database.Key]
                    public partial int Id{get;init;}
                }
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",baseSource)]);
        var driver=GeneratorTestHelpers.CreateDatabaseDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var baseOutput,out _);

        var initialItemType=baseOutput.GetTypeByMetadataName("Sample.Tables.Items")!;
        Assert.Null(initialItemType.GetMembers("Price").FirstOrDefault());

        // 列を追加した構文木に差し替え
        const string modifiedSource="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
            }
            namespace Sample.Tables{
                public partial struct Items{
                    [global::Seamless.Database.Key]
                    public partial int Id{get;init;}
                    public partial decimal Price{get;init;}
                }
            }
            """;
        var modifiedTree=Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(modifiedSource,GeneratorTestHelpers.ParseOptions,path:"Database.cs");
        var updatedCompilation=compilation.ReplaceSyntaxTree(compilation.SyntaxTrees[0],modifiedTree);

        driver.RunGeneratorsAndUpdateCompilation(updatedCompilation,out var modifiedOutput,out var diagnostics);
        Assert.DoesNotContain(diagnostics,static d=>d.Severity==Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.DoesNotContain(modifiedOutput.GetDiagnostics(),static d=>d.Severity==Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

        var updatedItemType=modifiedOutput.GetTypeByMetadataName("Sample.Tables.Items")!;
        var priceProp=updatedItemType.GetMembers("Price").OfType<Microsoft.CodeAnalysis.IPropertySymbol>().FirstOrDefault();
        Assert.NotNull(priceProp);
        Assert.Equal("decimal",priceProp.Type.ToDisplayString());
    }

    [Fact,Trait("Category","Unit")]
    public void IncompleteSyntaxDuringTyping_MaintainsExistingSymbolsWithoutCrash(){
        const string validSource="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
            }
            namespace Sample.Tables{
                public partial struct Items{
                    [global::Seamless.Database.Key]
                    public partial int Id{get;init;}
                }
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",validSource)]);
        var driver=GeneratorTestHelpers.CreateDatabaseDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out _,out _);

        // 入力途中の不完全な構文（波括弧が閉じられていない等の文法エラー構文木）を追加
        const string typingIncompleteSource="""
            namespace Sample.Tables{
                public partial struct DraftTyping{
                    [global::Seamless.Database.Key]
                    public int Incomplete
            """;
        var typingTree=Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(typingIncompleteSource,GeneratorTestHelpers.ParseOptions,path:"DraftTyping.cs");
        var updatedCompilation=compilation.AddSyntaxTrees(typingTree);

        // 生成器自体が例外スロー（Crash）しないことを検証
        driver.RunGeneratorsAndUpdateCompilation(updatedCompilation,out var typingOutput,out var diagnostics);
        Assert.DoesNotContain(diagnostics,static d=>d.Severity==Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

        // 既存の正常なテーブルのシンボルが維持されていることを検証
        var catalogType=typingOutput.GetTypeByMetadataName("Sample.Catalog")!;
        Assert.NotNull(catalogType);
        var itemsProperty=catalogType.GetMembers("Items").OfType<Microsoft.CodeAnalysis.IPropertySymbol>().FirstOrDefault();
        Assert.NotNull(itemsProperty);
    }
"@

$target = "    private static GeneratorRunResult Run(string source)"
if ($content.Contains($target)) {
    $newContent = $content.Replace($target, $insertCode + "`r`n`r`n" + $target)
    Set-Content -Path $path -Value $newContent -NoNewline
    Write-Output "Successfully inserted acceptance tests."
} else {
    Write-Error "Target not found"
}
