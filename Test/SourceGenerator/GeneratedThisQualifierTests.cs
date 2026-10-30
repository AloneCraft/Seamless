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

    [Fact]
    public void FieldUsedByLambdaIsCopiedToALocalAtTheStart() {
        var result = Qualify("int db; int limit; int Run(int item) { return Use(x => db+x)+limit; }");
        Assert.Contains("var db = this.db;",result);
        Assert.Contains("x => db+x",result);
        // ラムダの外で使う、ラムダに使われないフィールドには this. が付く。
        Assert.Contains("this.limit",result);
    }

    [Fact]
    public void EscapedFieldsUsedByNestedLambdasCompileAndPreserveValues(){
        const string source="""
            public sealed class CaptureOwner{public int Value=7;}
            public struct CaptureProbe{
                private readonly CaptureOwner @this;
                private readonly int @event;
                public CaptureProbe(CaptureOwner owner,int value){this.@this=owner;this.@event=value;}
                public int Read(){return Invoke(first=>Invoke(second=>@this.Value+@event+first+second));}
                private static int Invoke(System.Func<int,int> selector){return selector(1);}
            }
            public sealed class CaptureEntry{public static int Run(){return new CaptureProbe(new CaptureOwner(),3).Read();}}
            """;
        var qualified=GeneratedThisQualifier.Qualify(source);
        var compilation=GeneratorTestHelpers.CreateCompilation([("Capture.cs",qualified)]).WithAssemblyName("EscapedCaptureProbe");
        var actual=ExecutableGeneration.Run(compilation,"CaptureEntry",static type=>(int)type.GetMethod("Run")!.Invoke(null,null)!);
        Assert.Equal(12,actual);
    }

    [Fact]
    public void FieldThatIsMutatedIsNotCopied() {
        var result = Qualify("int state; int Run() { state++; return Use(x => state+x); }");
        Assert.DoesNotContain("var state = this.state;",result);
    }

    [Fact]
    public void OriginalLambdaNamesAreReusedOnlyWhenTheyDisappearFromGeneratedCode(){
        var helpers="struct S { void M() { var __it1_0=Read(); Use(__it1_0); } }";
        var body="";
        var preferred=new global::System.Collections.Generic.Dictionary<string,string>{{"__it1_0","row"}};
        var user=new global::System.Collections.Generic.HashSet<string>{"row"};
        GeneratedNameRenamer.Rename(ref helpers,ref body,user,preferred);
        Assert.Contains("var row=Read(); Use(row);",helpers);
        helpers="struct S { void M(int row) { var __it1_0=Read(); Use(row,__it1_0); } }";
        GeneratedNameRenamer.Rename(ref helpers,ref body,user,preferred);
        Assert.Contains("M(int row)",helpers);
        Assert.DoesNotContain("var row=Read()",helpers);
        Assert.Contains("var row2=Read()",helpers);
    }

    [Fact]
    public void LocalFunctionsAreReadableButExternalMethodsArePreserved(){
        var helpers="";
        var body="static bool __hasKey3(int key)=>key>0; var __it1=Read(); __Method_External(__hasKey3(__it1));";
        GeneratedNameRenamer.Rename(ref helpers,ref body,[]);
        Assert.Contains("static bool hasKey",body);
        Assert.Contains("__Method_External(hasKey(",body);
    }

    [Fact]
    public void GeneratedClassMembersAlsoGetThis(){
        var result=GeneratedThisQualifier.Qualify("class C { int count; int Run()=>count; }");
        Assert.Contains("=>this.count",result);
    }

    [Fact]
    public void GeneratedInitializationKeepsEvaluationOrderAndUserVariables(){
        var code="int __key2=default!; __key2=Next(); Use(__key2); int user=default; user=Next();";
        var result=GeneratedLocalInitializers.Simplify(code,methodBody:true);
        Assert.Contains("int __key2=Next();",result);
        Assert.Contains("int user=default; user=Next();",result);
        Assert.Equal(2,result.Split("Next()").Length-1);
        var self="int __key2=default; __key2=__key2+1;";
        Assert.Equal(self,GeneratedLocalInitializers.Simplify(self,methodBody:true));
        var intervening="int __key2=default; Observe(); __key2=Next();";
        Assert.Equal(intervening,GeneratedLocalInitializers.Simplify(intervening,methodBody:true));
    }

    [Fact]
    public void DiscardLambdaParameterDoesNotBecomeAnOutDiscard(){
        var helpers="struct S { int M() { source.TryGetNext(out var __it1_0); return __it1_0; } }";
        var body="";
        var preferred=new global::System.Collections.Generic.Dictionary<string,string>{{"__it1_0","_"}};
        GeneratedNameRenamer.Rename(ref helpers,ref body,[],preferred);
        Assert.DoesNotContain("out var _",helpers);
        Assert.Contains("out var item",helpers);
    }

    [Fact]
    public void ExistingUnderscoreVariablePreventsDiscardAssignments(){
        var names=new GeneratedNames(new global::System.Collections.Generic.HashSet<string>{"_"},[]);
        Assert.False(names.CanUseDiscard);
        Assert.True(new GeneratedNames([],[]).CanUseDiscard);
    }
}
