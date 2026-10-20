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
    protected override string ComputeExpression => $"Compute(ref {this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeToArrayInline(destVar,destType)
        ??this.RenderDirectSpanToArrayInline(destVar,destType)
        ??this.RenderZipToArrayInline(destVar,destType)
        ??this.RenderConsumeBasedToArrayInline(destVar,destType)
        ??this.RenderDirectEnumerableToArrayHybridInline(source,destVar,destType)
        ??this.RenderComputeInline(destVar,destType);
    private string? RenderConsumeBasedToArrayInline(string destVar,string destType){
        if(source is not (Join or GroupJoin or SelectManyBase)){
            return null;
        }
        var placeholder=$"__FLINQ_TOARRAY_{番号}__";
        string consumeStatement;
        try{
            consumeStatement=source.GetConsumeExecutionStatement(placeholder);
        }catch(NotSupportedException){
            return null;
        }
        if(string.IsNullOrWhiteSpace(consumeStatement)
            ||consumeStatement.Contains("TryGetNext")
            ||consumeStatement.Contains("GetRemainingEnumerator")
            ||!consumeStatement.Contains($"{placeholder}.Consume(")){
            return null;
        }
        var poolBufferVar=$"__consumePoolBuffer{番号}";
        var poolCountVar=$"__consumePoolCount{番号}";
        var poolNextVar=$"__consumePoolNext{番号}";
        var escaped=global::System.Text.RegularExpressions.Regex.Escape(placeholder);
        var pattern=$@"(?<indent>[ \t]*){escaped}\.Consume\((?<arg>.+?)\);";
        var replaced=global::System.Text.RegularExpressions.Regex.Replace(consumeStatement,pattern,match => {
            var indent=match.Groups["indent"].Value;
            var arg=match.Groups["arg"].Value;
            var sb = new StringBuilder();
            sb.AppendLine($"{indent}if({poolCountVar}=={poolBufferVar}.Length) {{");
            sb.AppendLine($"{indent}    var {poolNextVar}=global::System.Buffers.ArrayPool<{source.ElementType}>.Shared.Rent({poolBufferVar}.Length*2);");
            sb.AppendLine($"{indent}    global::System.Array.Copy({poolBufferVar},{poolNextVar},{poolCountVar});");
            sb.AppendLine($"{indent}    global::System.Buffers.ArrayPool<{source.ElementType}>.Shared.Return({poolBufferVar},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{source.ElementType}>());");
            sb.AppendLine($"{indent}    {poolBufferVar}={poolNextVar};");
            sb.AppendLine($"{indent}}}");
            sb.Append($"{indent}{poolBufferVar}[{poolCountVar}++]={arg};");
            return sb.ToString();
        });
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{poolBufferVar}}=global::System.Buffers.ArrayPool<{{source.ElementType}}>.Shared.Rent(16);
                var {{poolCountVar}}=0;
                try {
                    {{replaced}}
                    if({{poolCountVar}}==0) {
                        {{destVar}}=global::System.Array.Empty<{{source.ElementType}}>();
                    } else {
                        {{destVar}}=global::System.GC.AllocateUninitializedArray<{{source.ElementType}}>({{poolCountVar}});
                        global::System.Array.Copy({{poolBufferVar}},{{destVar}},{{poolCountVar}});
                    }
                } finally {
                    global::System.Buffers.ArrayPool<{{source.ElementType}}>.Shared.Return({{poolBufferVar}},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{{source.ElementType}}>());
                }
            }
            """;
    }
    private string? RenderZipToArrayInline(string destVar,string destType){
        if(source is Zip zip){
            return zip.RenderDirectToArrayInline(destVar,destType);
        }
        if(source is Zip_resultSelector zipSel){
            return zipSel.RenderDirectToArrayInline(destVar,destType);
        }
        if(source is Zip3 zip3){
            return zip3.RenderDirectToArrayInline(destVar,destType);
        }
        return null;
    }
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
                {{range.RenderArgumentPreparation(start,count)}}
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
        private static {{resultType}} Compute(ref TEnum source){
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
        private static {{resultType}} Compute(ref TEnum source){
            // Compute は列挙子を破棄しない。Count など他の終端と同じく、参照で渡された本体を持ち主
            // (呼び出し元の finally、または終端 enumerator の Dispose)が 1 回だけ破棄する。
            // ここで複製を作って破棄すると、コンストラクタでプール配列を借りる enumerator（OrderBy 等）が
            // 呼び出し元の Dispose と二重 Return になり、後続の無関係なクエリのデータが壊れる。
            ref var enumerator = ref source;
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
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
