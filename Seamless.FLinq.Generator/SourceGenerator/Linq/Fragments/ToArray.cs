namespace Seamless.FLinq.Generator;

/*
ToArray は source.ToArray() を、件数/Span/InlineArray/ArrayPool の順で最短コピーする Compute に変換する。

入力例:
  source.ToArray()

生成例:
  if(source.TryGetNonEnumeratedCount(out var count)) {
      result = AllocateUninitializedArray<T>(count);
      if(source.TryCopyTo(result, 0)) return result;
      while(source.TryGetNext(out var v)) result[i++] = v;
  } else if(source.TryGetSpan(out var span)) {
      result = copy(span);
  } else {
      small buffer は InlineArray、溢れたら ArrayPool へ spill;
  }

バリエーション:
  - RangeRoot<int> は count サイズの配列を確保して start+i を直接代入する。
  - array/list/ICollection 由来の DirectEnumerableExpression は専用 hybrid inline を生成する。
*/
internal class ToArray(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    // スタック上で受ける InlineArray の要素数。これ以内なら ArrayPool に一切触れない。
    private const int InlineCapacity = 16;
    protected override Enumerable PrimarySource => source;
    // InlineArray バッファ型を補助 struct として出力する。
    // {Struct}_Buf 命名は補助クラス退避の正規表現（Struct\d+_接尾辞）にマッチしリネーム衝突を防ぐ。
    public override string StructMethod => $$"""
        [global::System.Runtime.CompilerServices.InlineArray({{InlineCapacity}})]
        private struct {{this.Struct}}_Buf{{this.StructTypeParametersWithOuter()}} {
            private {{source.ElementType}} _element0;
        }
        """;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeToArrayInline(destVar,destType)
        ??this.RenderDirectSpanToArrayInline(destVar,destType)
        ??this.RenderDirectEnumerableToArrayHybridInline(source,destVar,destType)
        ??this.RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanToArrayInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            if({{spanName}}.Length==0){
                {{destVar}}=global::System.Array.Empty<{{source.ElementType}}>();
            }else{
                {{destVar}}=global::System.GC.AllocateUninitializedArray<{{source.ElementType}}>({{spanName}}.Length);
                global::Seamless.Linq.LinqCounterScope.IncrementFastCopyUsed();
                {{spanName}}.CopyTo(global::System.MemoryExtensions.AsSpan({{destVar}}));
            }
            """);
    }
    private string? RenderRangeToArrayInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }

        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var index = $"__i{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                {{destVar}}=new int[{{count}}];
                for(var {{index}}=0;{{index}}<{{count}};{{index}}++){
                    {{destVar}}[{{index}}]={{start}} + {{index}};
                }
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source){
            global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanCalled();
            source.TryGetSpan(out var __sourceSpan);
            global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanSucceeded();
            if(__sourceSpan.Length==0){
                return global::System.Array.Empty<{{source.ElementType}}>();
            }
            var __result=global::System.GC.AllocateUninitializedArray<{{source.ElementType}}>(__sourceSpan.Length);
            global::Seamless.Linq.LinqCounterScope.IncrementFastCopyUsed();
            __sourceSpan.CopyTo(global::System.MemoryExtensions.AsSpan(__result));
            return __result;
        }
        """
        : $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source){
            using var enumerator = source;
            if(enumerator.TryGetNonEnumeratedCount(out var __count)){
                if(__count==0){
                    return global::System.Array.Empty<{{source.ElementType}}>();
                }
                var __result=global::System.GC.AllocateUninitializedArray<{{source.ElementType}}>(__count);
                if(enumerator.TryCopyTo(global::System.MemoryExtensions.AsSpan(__result),0)){
                    global::Seamless.Linq.LinqCounterScope.IncrementFastCopyUsed();
                    return __result;
                }
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                var __i=0;
                while(enumerator.TryGetNext(out var __mn80)){
                    __result[__i++]=__mn80;
                }
                return __result;
            }
            global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanCalled();
            if(enumerator.TryGetSpan(out var __sourceSpan)){
                global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanSucceeded();
                if(__sourceSpan.Length==0){
                    return global::System.Array.Empty<{{source.ElementType}}>();
                }
                var __result=global::System.GC.AllocateUninitializedArray<{{source.ElementType}}>(__sourceSpan.Length);
                global::Seamless.Linq.LinqCounterScope.IncrementFastCopyUsed();
                __sourceSpan.CopyTo(global::System.MemoryExtensions.AsSpan(__result));
                return __result;
            }
            global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanFailed();
            global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
            // 要素数が InlineCapacity 以内ならスタック上の InlineArray だけで受け、ArrayPool/List の
            // ヒープ確保もスレッドセーフな Rent/Return も一切回避する。溢れたときだけ ArrayPool へスピルし、
            // 最後に正確なサイズの配列へ1回だけコピーする。MoveNext/Current のみで TEnum 制約に依存しない。
            {{this.Struct}}_Buf{{this.StructTypeParametersWithOuter()}} __inline = default;
            global::System.Span<{{source.ElementType}}> __stack = __inline;
            {{source.ElementType}}[]? __heap = null;
            var n = 0;
            try {
                while(enumerator.TryGetNext(out var __mn81)){
                    if(__heap is null){
                        if(n < {{InlineCapacity}}){
                            __stack[n++] = __mn81;
                            continue;
                        }
                        // スタックバッファが満杯になったので ArrayPool へ移行する。
                        __heap = global::System.Buffers.ArrayPool<{{source.ElementType}}>.Shared.Rent({{InlineCapacity}} * 2);
                        __stack.CopyTo(__heap);
                    } else if(n == __heap.Length){
                        var __next = global::System.Buffers.ArrayPool<{{source.ElementType}}>.Shared.Rent(__heap.Length * 2);
                        global::System.Array.Copy(__heap, __next, n);
                        global::System.Buffers.ArrayPool<{{source.ElementType}}>.Shared.Return(__heap, clearArray: true);
                        __heap = __next;
                    }
                    __heap[n++] = __mn81;
                }
                if(n == 0){
                    return global::System.Array.Empty<{{source.ElementType}}>();
                }
                var result = new {{source.ElementType}}[n];
                if(__heap is null){
                    __stack.Slice(0, n).CopyTo(result);
                }else{
                    global::System.Array.Copy(__heap, result, n);
                }
                return result;
            } finally {
                if(__heap is not null){
                    global::System.Buffers.ArrayPool<{{source.ElementType}}>.Shared.Return(__heap, clearArray: true);
                }
            }
        }
        """;
    protected override string GetEnumeratorDisposeBody => string.Empty;
}
