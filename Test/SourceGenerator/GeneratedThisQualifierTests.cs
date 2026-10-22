using Seamless.FLinq.Generator;

namespace Test.SourceGenerator;

/// <summary>
/// 生成した構造体の中のメンバーの使用に <c>this.</c> を付ける後処理(<see cref="GeneratedThisQualifier"/>)と、
/// 機械的な名前の付け替え(<see cref="GeneratedNameRenamer"/>)の単体テスト。意味が変わる所には付けない・触らないことを確かめる。
/// </summary>
public sealed class GeneratedThisQualifierTests {
    private static string Qualify(string members) =>
        GeneratedThisQualifier.Qualify($"struct S {{ {members} }}");

    [Fact]
    public void FieldsPropertiesAndMethodsGetThis() {
        var result = Qualify("int count; int Current { get; set; } int Next() => 1; int Run() { Current = count + Next(); return Current; }");
        Assert.Contains("this.Current = this.count + this.Next();",result);
        Assert.Contains("return this.Current;",result);
    }

    [Fact]
    public void AlreadyQualifiedIsNotDoubled() {
        var result = Qualify("int count; int Run() => this.count;");
        Assert.DoesNotContain("this.this",result);
        Assert.Contains("=> this.count;",result);
    }

    [Fact]
    public void ParameterWithTheSameNameKeepsConstructorShape() {
        var result = Qualify("int count; S(int count) { this.count = count; }");
        Assert.Contains("this.count = count;",result);
        Assert.DoesNotContain("this.count = this.count",result);
    }

    [Fact]
    public void LocalWithTheSameNameIsLeftAlone() {
        var result = Qualify("int count; int Run() { var count = 1; return count; }");
        Assert.Contains("return count;",result);
    }

    [Fact]
    public void MemberNamesOfOtherObjectsAndNamedArgumentsAreLeftAlone() {
        var result = Qualify("int count; int Run(Other other) => other.count + F(count: 1);");
        Assert.Contains("other.count",result);
        Assert.Contains("F(count: 1)",result);
        Assert.DoesNotContain("this.count: 1",result);
    }

    [Fact]
    public void StaticMembersAndLambdasAreLeftAlone() {
        var result = Qualify("int count; static int Make(int x) => count; int Run() => Use(x => count);");
        Assert.DoesNotContain("this.count",result);
    }

    [Fact]
    public void ObjectInitializerLeftSideIsLeftAlone() {
        var result = Qualify("int count; Other Run() => new Other { count = count };");
        Assert.Contains("new Other { count = this.count }",result);
    }

    [Fact]
    public void RenamerKeepsContractAndMethodNamesAndAvoidsUserIdentifiers() {
        var helpers = "struct S { int __e2; void M() { var __it2_0 = __e2; __Method_X(__it2_0); } }";
        var body = "var item = 1; var __it3_0 = item; M(__it3_0);";
        var user = new global::System.Collections.Generic.HashSet<string> { "item" };
        GeneratedNameRenamer.Rename(ref helpers,ref body,user);
        // 付け替えた名前は役割名になり、利用者の item とぶつからない。外部のメソッド名(__Method_X)は残る。
        Assert.Contains("__Method_X",helpers);
        Assert.DoesNotContain("__it2_0",helpers);
        Assert.DoesNotContain("__it3_0",body);
        Assert.DoesNotContain("__e2",helpers);
        Assert.Contains("var item = 1;",body);
        Assert.DoesNotContain("var item = item",body);
    }
}
