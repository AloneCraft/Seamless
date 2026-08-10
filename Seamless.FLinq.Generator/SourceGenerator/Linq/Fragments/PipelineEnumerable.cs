namespace Seamless.FLinq;

/*
PipelineEnumerable は Where/Select/Take/OrderBy など複数 stage を1つの enumerator 状態機械へ畳む。

入力例:
  source.Where(p).Select(s).Take(n)

生成例:
  while(source.TryGetNext(out var current0)) {
      if(!p(current0)) continue;
      var current1 = s(current0);
      if(takeCountReached) return false;
      current = current1;
      return true;
  }
  return false;

主な stage 分岐:
  - Where/Select/AsEnumerable/Cast は直列 loop 内の軽い変換として展開する。
  - SelectMany/GroupBy/Reverse/OrderBy/TakeLast などは stage 専用の buffer/state field を追加する。
  - Union/Except/Intersect 系 stage は二入力集合演算として BuildBinarySetLoop に切り替える。

設計意図:
  個別 Fragment を深くネストすると enumerator struct が段数分重なる。Pipeline は stage list を見て
  1つの Struct に state field と loop 分岐を集め、CountPreserving な場合は TryCopyTo もまとめて生成する。
*/
internal sealed partial class PipelineEnumerable(int 番号,Enumerable source,IReadOnlyList<PipelineStage> stages,string elementType):Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    private bool StagesCountPreserving => stages.All(static stage => stage.Kind is PipelineStageKind.Select or PipelineStageKind.AsEnumerable or PipelineStageKind.Cast);
    private bool SingleDefaultIfEmpty => stages.Count==1&&stages[0].Kind==PipelineStageKind.DefaultIfEmpty;
    protected override Enumerable PrimarySource => source;
    public Enumerable Source => source;
    public IReadOnlyList<PipelineStage> Stages => stages;
    public override string ElementType => elementType;
    public override string? NonEnumeratedCountExpression => this.StagesCountPreserving ? source.NonEnumeratedCountExpression : null;
    public override bool CountPreserving => source.CountPreserving&&this.StagesCountPreserving;
    public override bool CanTryProvideSpan => this.SingleDefaultIfEmpty&&source.CanTryProvideSpan;
    // SecondSources は stages(PipelineStage の一覧)の中に埋もれているため、Fragment 型フィールドを
    // 走査するリフレクションベースの伝播(基底実装)からは見えない。ここで明示的に伝播しないと、
    // 二入力集合演算(Union/Except 等)へ Where が融合されたとき、開いたジェネリック型引数(T)を
    // 参照する第2ソース側の struct が T/@this を解決できず生成コードが壊れる。
    public override void PropagateOuterTypeParameters(string[] outerTypeParameters) {
        base.PropagateOuterTypeParameters(outerTypeParameters);
        foreach(var second in this.SecondSources) {
            second.PropagateOuterTypeParameters(outerTypeParameters);
        }
    }
    public override void PropagateContainingTypeName(string? name,string[]? outerTypeParameters = null) {
        base.PropagateContainingTypeName(name,outerTypeParameters);
        foreach(var second in this.SecondSources) {
            second.PropagateContainingTypeName(name,outerTypeParameters);
        }
    }
    private void AppendCounterCall(StringBuilder builder,string indent,string methodName) {
        var call = this.CounterCall(methodName);
        if(!string.IsNullOrEmpty(call)) {
            builder.AppendLine($"{indent}{call}");
        }
    }
    public override string StructMethod {
        get {
            var result = string.Empty;
            foreach(var source in this.Funcs.Select(static f => f.SupportingMemberSources)) {
                result=JoinStaticMethods(result,source);
            }
            foreach(var source in this.SecondSources.Select(static source => source.StructMethod)) {
                result=JoinStaticMethods(result,source);
            }
            return result;
        }
    }
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.SecondSourcesStructs}}
        {{base.GetEnumeratorStruct}}
        """;
    protected override string StructTypeParameters {
        get {
            var secondSources = this.SecondSources.ToArray();
            if(secondSources.Length==0) {
                return base.StructTypeParameters;
            }
            var parameters = new List<string> { "TEnum" };
            for(var index = 0;index<secondSources.Length;index++) {
                parameters.Add(SecondTypeParameterName(index));
            }
            // OuterTypeParameters(含有型がジェネリックな場合の T 等)を先頭に付けないと、
            // ソースが開いたジェネリック型引数のときに struct 本体が参照する T が未解決になる。
            return this.StructTypeParametersWithOuter(parameters.ToArray());
        }
    }
    protected override string StructTypeConstraints {
        get {
            var constraints = new List<string> { $"where TEnum : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>" };
            foreach(var (second,index) in this.SecondSources.Select(static (second,index) => (second,index))) {
                constraints.Add($"where {SecondTypeParameterName(index)} : struct, global::Seamless.Linq.IValueEnumerator<{second.ElementType}>");
            }
            return string.Join("\n    ",constraints);
        }
    }
    public override string EnumeratorTypeName {
        get {
            var secondSources = this.SecondSources.ToArray();
            if(secondSources.Length==0) {
                return base.EnumeratorTypeName;
            }
            var arguments = new List<string> { source.EnumeratorTypeName };
            arguments.AddRange(secondSources.Select(static second => second.EnumeratorTypeName));
            // StructTypeParameters と対にして OuterTypeParameters を先頭に付ける(型引数の個数を揃える)。
            return this.EnumeratorTypeNameWithOuter(arguments.ToArray());
        }
    }
    public override string GetEnumeratorExpression {
        get {
            var args = new List<string> { source.GetEnumeratorExpression };
            foreach(var second in this.SecondSources) {
                args.Add(second.GetEnumeratorExpression);
            }
            foreach(var stage in this.CountStages()) {
                args.Add(stage.Value!);
            }
            foreach(var stage in this.RangeStages()) {
                args.Add(stage.Value!);
            }
            foreach(var stage in this.ComparerStages()) {
                args.Add(this.EqualityComparerExpression(stage));
            }
            foreach(var stage in this.OrderComparerStages()) {
                args.Add(this.OrderComparerExpression(stage));
            }
            args.AddRange(this.ClosureArgsWithoutPrefix());
            return RenderEnumeratorExpression(this.EnumeratorTypeName,args.ToArray());
        }
    }
}
