namespace Seamless.SourceGenerator.Linq.Fragments;

public abstract class ToLookupBase(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType){
    protected override Enumerable PrimarySource=>source;
    protected override string StructSources=>source.GetEnumeratorStruct;
    protected override string GetEnumeratorDisposeBody=>$"{e}.Dispose();";
    protected static string LookupImpl(string elementType,string keyType)=>$$"""
        private sealed class SeamlessGrouping:global::System.Linq.IGrouping<{{keyType}},{{elementType}}>{
            private readonly {{keyType}} key;
            private readonly global::System.Collections.Generic.List<{{elementType}}> elements;
            public SeamlessGrouping({{keyType}} key,global::System.Collections.Generic.List<{{elementType}}> elements){this.key=key;this.elements=elements;}
            public {{keyType}} Key=>this.key;
            public global::System.Collections.Generic.IEnumerator<{{elementType}}> GetEnumerator()=>this.elements.GetEnumerator();
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
        }
        private sealed class SeamlessLookup:global::System.Linq.ILookup<{{keyType}},{{elementType}}>{
            private readonly global::System.Collections.Generic.Dictionary<{{keyType}},global::System.Collections.Generic.List<{{elementType}}>> map;
            private readonly global::System.Collections.Generic.List<{{keyType}}> order;
            public SeamlessLookup(global::System.Collections.Generic.Dictionary<{{keyType}},global::System.Collections.Generic.List<{{elementType}}>> map,global::System.Collections.Generic.List<{{keyType}}> order){this.map=map;this.order=order;}
            public int Count=>this.map.Count;
            public global::System.Collections.Generic.IEnumerable<{{elementType}}> this[{{keyType}} key]=>this.map.TryGetValue(key,out var g)?g:global::System.Linq.Enumerable.Empty<{{elementType}}>();
            public bool Contains({{keyType}} key)=>this.map.ContainsKey(key);
            public global::System.Collections.Generic.IEnumerator<global::System.Linq.IGrouping<{{keyType}},{{elementType}}>> GetEnumerator(){
                foreach(var k in this.order) yield return new SeamlessGrouping(k,this.map[k]);
            }
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
        }
        """;
}
