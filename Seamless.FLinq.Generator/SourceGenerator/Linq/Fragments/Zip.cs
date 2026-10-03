namespace Seamless.FLinq.Generator;

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
internal class Zip(int 番号,Enumerable source,Enumerable second):Enumerable(番号) {
    // 2つの根を同時に進めるループの頭。first を先に進め、尽きたら second は進めない(System.Linq と同じ)。
    private protected string ZipDrainHead(Enumerable first,Enumerable secondSource,string firstName,string secondName,string firstItem,string secondItem) =>
        ZipDrainSupport.Head(first,secondSource,firstName,secondName,firstItem,secondItem);
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
        if(second is { } __visitorChild_second) {
            yield return __visitorChild_second;
        }
    }

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
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,second.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second";
    public override string GetConsumeExecutionStatement(string consumerName) {
        return $$"""
            using var __zipSource={{source.GetEnumeratorExpression}};
            using var __zipSecond={{second.GetEnumeratorExpression}};
            if(__zipSource.TryGetSpan(out var __zipSourceSpan)&&__zipSecond.TryGetSpan(out var __zipSecondSpan)){
                global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
                var __zipLength=global::System.Math.Min(__zipSourceSpan.Length,__zipSecondSpan.Length);
                for(var __zipIndex=0;__zipIndex<__zipLength;__zipIndex++){
                    {{consumerName}}.Consume((__zipSourceSpan[__zipIndex],__zipSecondSpan[__zipIndex]));
                }
            }else{
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                {{this.ZipDrainHead(source,second,"__zipSource","__zipSecond","__zipFirst","__zipSecondValue")}}
                    {{consumerName}}.Consume((__zipFirst,__zipSecondValue));
                }
            }
            """;
    }    public override string GetTryGetNextBody => $$"""
        if(source.TryGetNext(out var __mn94)&&second.TryGetNext(out var __mn95)){
            current=this.{{this.currentValue}}=(__mn94,__mn95);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => $"({source.ElementType} First, {second.ElementType} Second)";
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
}

internal class Zip3(int 番号,Enumerable source,Enumerable second,Enumerable third):Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
        if(second is { } __visitorChild_second) {
            yield return __visitorChild_second;
        }
        if(third is { } __visitorChild_third) {
            yield return __visitorChild_third;
        }
    }

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
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,second.GetEnumeratorExpression,third.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second, TEnum3 third";
    public override string GetTryGetNextBody => $$"""
        if(source.TryGetNext(out var __mn96)&&second.TryGetNext(out var __mn97)&&third.TryGetNext(out var __mn98)){
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
internal class Zip_resultSelector(int 番号,Enumerable source,Enumerable second,FuncFragment resultSelector):Enumerable(番号) {
    private string ZipDrainHead(Enumerable first,Enumerable secondSource,string firstName,string secondName,string firstItem,string secondItem) =>
        ZipDrainSupport.Head(first,secondSource,firstName,secondName,firstItem,secondItem);
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
        if(second is { } __visitorChild_second) {
            yield return __visitorChild_second;
        }
    }
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(resultSelector is { } __visitorChild_resultSelector) {
            yield return __visitorChild_resultSelector;
        }
    }

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
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { resultSelector },source.GetEnumeratorExpression,second.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum1 source, TEnum2 second{this.ClosurePrimaryCtorSuffix(resultSelector)}";
    // Consume は Zip の列挙子の構造体を作らず、差し替えメソッドで両方の根を直接回す
    // (Zip→根の構造体の入れ子の呼び出しは、参照型の共有ジェネリックコードでインライン化されない)。
    // 両方が span を出せれば短い方の長さまで添字で回す。そうでなければ System.Linq と同じく先に first を進め、尽きたら second は進めない。
    public override string GetConsumeExecutionStatement(string consumerName) {
        string sourceExpression;
        string secondExpression;
        try {
            sourceExpression=source.GetEnumeratorExpression;
            secondExpression=second.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(resultSelector),
            this.StaticDelegateLocalDeclarations(resultSelector),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var sourceName = $"__zipSource{番号}";
        var secondName = $"__zipSecond{番号}";
        var sourceSpanName = $"__zipSourceSpan{番号}";
        var secondSpanName = $"__zipSecondSpan{番号}";
        var lengthName = $"__zipLength{番号}";
        var indexName = $"__zipIndex{番号}";
        var firstName = $"__zipFirst{番号}";
        var secondValueName = $"__zipSecondValue{番号}";
        var resultName = $"__zipResult{番号}";
        var resultType = FuncReturnTypeName(resultSelector);
        var assign = $"{resultType} {resultName}=default!;\n{this.FuncAssignStatic(resultSelector,nameof(resultSelector),resultName,firstName,secondValueName)}\n{consumerName}.Consume({resultName});";
        return $$"""
            {{localDeclarations}}
            using var {{sourceName}}={{sourceExpression}};
            using var {{secondName}}={{secondExpression}};
            if({{sourceName}}.TryGetSpan(out var {{sourceSpanName}})&&{{secondName}}.TryGetSpan(out var {{secondSpanName}})){
                global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
                var {{lengthName}}=global::System.Math.Min({{sourceSpanName}}.Length,{{secondSpanName}}.Length);
                for(var {{indexName}}=0;{{indexName}}<{{lengthName}};{{indexName}}++){
                    var {{firstName}}={{sourceSpanName}}[{{indexName}}];
                    var {{secondValueName}}={{secondSpanName}}[{{indexName}}];
                    {{IndentGeneratedStatement(assign,"            ")}}
                }
            }else{
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                {{this.ZipDrainHead(source,second,sourceName,secondName,firstName,secondValueName)}}
                    {{IndentGeneratedStatement(assign,"            ")}}
                }
            }
            """;
    }
    public override string GetTryGetNextBody => $$"""
        if(source.TryGetNext(out var __mn99)&&second.TryGetNext(out var __mn100)){
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

internal static class ZipDrainSupport {
    public static string Head(Enumerable first,Enumerable second,string firstName,string secondName,string firstItem,string secondItem) {
        var (firstPreamble,firstCondition,firstDeclaration) = Fragment.DrainParts(first,firstName,firstItem);
        var (secondPreamble,secondCondition,secondDeclaration) = Fragment.DrainParts(second,secondName,secondItem);
        return $"{firstPreamble}{secondPreamble}while({firstCondition}&&{secondCondition}){{{firstDeclaration}{secondDeclaration}";
    }
}
