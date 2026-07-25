namespace Seamless.FLinq.Linq.Fragments;

/*
Zip は複数 source を同時に1件ずつ進め、どれかが尽きた時点で終了する enumerator を生成する。

入力例:
  first.Zip(second)
  first.Zip(second, (a, b) => ...)

生成例:
  if(first.TryGetNext(out var v) && second.TryGetNext(out var v)) {
      current = (v, v);
      return true;
  }
  return false;

バリエーション:
  - Zip3 は3入力 tuple を返す。
  - resultSelector 付きは tuple ではなく resultSelector(v, v) を Current に入れる。
*/
public class Zip(int 番号,Enumerable source,Enumerable second):Enumerable(番号) {
    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => this.StructTypeParametersWithOuter("TEnum1","TEnum2");
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::Seamless.Linq.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => this.EnumeratorTypeNameWithOuter(source.EnumeratorTypeName,second.EnumeratorTypeName);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second";
    public override string GetTryGetNextBody => $$"""
        if(source.TryGetNext(out var __mn94)&&second.TryGetNext(out var __mn95)) {
            current=this.{{this.currentValue}}=(__mn94,__mn95);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => $"({source.ElementType} First, {second.ElementType} Second)";
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
}

public class Zip3(int 番号,Enumerable source,Enumerable second,Enumerable third):Enumerable(番号) {
    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => this.StructTypeParametersWithOuter("TEnum1","TEnum2","TEnum3");
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::Seamless.Linq.IValueEnumerator<{second.ElementType}>\n    where TEnum3 : struct, global::Seamless.Linq.IValueEnumerator<{third.ElementType}>";
    public override string EnumeratorTypeName => this.EnumeratorTypeNameWithOuter(source.EnumeratorTypeName,second.EnumeratorTypeName,third.EnumeratorTypeName);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{third.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}, {third.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second, TEnum3 third";
    public override string GetTryGetNextBody => $$"""
        if(source.TryGetNext(out var __mn96)&&second.TryGetNext(out var __mn97)&&third.TryGetNext(out var __mn98)) {
            current=this.{{this.currentValue}}=(__mn96,__mn97,__mn98);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => $"({source.ElementType} First, {second.ElementType} Second, {third.ElementType} Third)";
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();\nthird.Dispose();";
}

/*
Zip_resultSelector は Zip(second, resultSelector) 用。2つの Current を selector へ渡して任意の結果型にする。

生成例:
  if(first.TryGetNext(out var v) && second.TryGetNext(out var v)) {
      current = resultSelector(v, v);
      return true;
  }

selector は通常の FuncFragment と同じく、inline/delegate/closure 付きの各経路を取る。
*/
public class Zip_resultSelector(int 番号,Enumerable source,Enumerable second,FuncFragment resultSelector):Enumerable(番号) {
    private string ResultSelectorFuncType => $"global::System.Func<{source.ElementType}, {second.ElementType}, {FuncReturnTypeName(resultSelector)}>";
    public override string StructMethod => this.FuncSupport(resultSelector,nameof(resultSelector));
    protected override string StructTypeParameters => this.StructTypeParametersWithOuter("TEnum1","TEnum2");
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::Seamless.Linq.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => this.EnumeratorTypeNameWithOuter(source.EnumeratorTypeName,second.EnumeratorTypeName);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}{this.ClosureArgsSuffix(resultSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum1 source, TEnum2 second{this.ClosurePrimaryCtorSuffix(resultSelector)}";
    public override string GetTryGetNextBody => $$"""
        if(source.TryGetNext(out var __mn99)&&second.TryGetNext(out var __mn100)) {
            var {{this.item}}=__mn99;
            var {{this.temp}}=__mn100;
            {{this.FuncAssignCurrentIndented("    ",resultSelector,nameof(resultSelector),this.currentValue,this.item,this.temp)}}
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => FuncReturnTypeName(resultSelector);
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
}
