namespace Seamless.FLinq;

/*
「全件バッファしてから先頭から返す」実装（PipelineEnumerable の融合 Reverse/TakeLast/TakeRange、
および単独 Reverse フラグメントの非 span 経路）が共有する ArrayPool 版バッファの
構築・成長・返却の文字列テンプレート。OrderBy が既に確立している配管
（Rent → 所有権フラグ → try → 成功時にフィールドへ移譲してフラグ false → finally で未移譲時のみ返却）
と同じ形を、ソートを伴わない単純な用途向けに提供する。

返却点は「構築 finally の未移譲パス」と「Dispose」の2つだけに限定する（二重返却を防ぐため）。
配列の Length は成長後に論理件数より大きくなり得るため、論理件数は別フィールドで持つ。

呼び出し側の書式（StringBuilder 積み上げ / raw string 直埋め込み）に依存しないよう、
文字列を返すだけの純粋関数として提供する。suffix は呼び出し元の各段で一意な文字列。
*/
internal static class PooledBufferSupport {
    public static string BuildStart(string enumeratorName,string elementType,string fieldName,string countField,string returnLocal,string suffix) {
        var capacityLocal = $"__bufCapacity{suffix}";
        var sourceCountLocal = $"__bufSourceCount{suffix}";
        return $$"""
            var {{capacityLocal}}=16;
            if({{enumeratorName}}.TryGetNonEnumeratedCount(out var {{sourceCountLocal}})&&{{sourceCountLocal}}>16){
                {{capacityLocal}}={{sourceCountLocal}};
            }
            var {{fieldName}}=global::System.Buffers.ArrayPool<{{elementType}}>.Shared.Rent({{capacityLocal}});
            var {{returnLocal}}=true;
            var {{countField}}=0;
            try{
            """;
    }

    public static string Add(string elementType,string fieldName,string countField,string valueExpr,string suffix) {
        var nextCapacity = $"__bufNextCapacity{suffix}";
        var nextValues = $"__bufNext{suffix}";
        return $$"""
            if({{countField}}=={{fieldName}}.Length){
                var {{nextCapacity}}={{fieldName}}.Length*2;
                var {{nextValues}}=global::System.Buffers.ArrayPool<{{elementType}}>.Shared.Rent({{nextCapacity}});
                global::System.Array.Copy({{fieldName}},{{nextValues}},{{countField}});
                global::System.Buffers.ArrayPool<{{elementType}}>.Shared.Return({{fieldName}},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{{elementType}}>());
                {{fieldName}}={{nextValues}};
            }
            {{fieldName}}[{{countField}}]={{valueExpr}};
            {{countField}}++;
            """;
    }

    public static string Commit(string fieldName,string countField) => $$"""
        this.{{fieldName}}={{fieldName}};
        this.{{countField}}={{countField}};
        """;

    public static string Finally(string elementType,string fieldName,string returnLocal) => $$"""
        {{returnLocal}}=false;
        }finally{
            if({{returnLocal}}){
                global::System.Buffers.ArrayPool<{{elementType}}>.Shared.Return({{fieldName}},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{{elementType}}>());
            }
        }
        """;

    public static string Dispose(string elementType,string fieldName) => $$"""
        if(this.{{fieldName}} is not null&&this.{{fieldName}}.Length!=0){
            global::System.Buffers.ArrayPool<{{elementType}}>.Shared.Return(this.{{fieldName}},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{{elementType}}>());
            this.{{fieldName}}=null;
        }
        """;
}
