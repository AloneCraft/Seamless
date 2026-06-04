namespace Seamless.SourceGenerator.Linq.Fragments;

public class Chunk(int 番号, Enumerable source, string size) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {size})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, int size";
    public override string GetEnumeratorFields => source.CanProvideSpan
        ? $$"""
            private int {{a}};
            """
        : $$"""
            private {{source.ElementType}}[]? {{group}};
            """;
    public override string GetTryGetNextBody => source.CanProvideSpan
        ? $$"""
            if(size<=0) throw new global::System.ArgumentOutOfRangeException(nameof(size));
            {{e}}.TryGetSpan(out var __span);
            if((uint)this.{{a}}<(uint)__span.Length) {
                var len=global::System.Math.Min(size,__span.Length-this.{{a}});
                current=this.{{currentValue}}=__span.Slice(this.{{a}},len).ToArray();
                this.{{a}}+=len;
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            if(size<=0) throw new global::System.ArgumentOutOfRangeException(nameof(size));
            var __i=0;
            if(this.{{group}} is null) this.{{group}}=new {{source.ElementType}}[size];
            while({{e}}.MoveNext()) {
                this.{{group}}[__i++]= {{e}}.Current;
                if(__i==size) {
                    current=this.{{currentValue}}=this.{{group}};
                    this.{{group}}=new {{source.ElementType}}[size];
                    return true;
                }
            }
            if(__i>0) {
                var __last=new {{source.ElementType}}[__i];
                global::System.Array.Copy(this.{{group}},__last,__i);
                current=this.{{currentValue}}=__last;
                this.{{group}}=null;
                return true;
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => $"{source.ElementType}[]";
}
