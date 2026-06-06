namespace Seamless.SourceGenerator.Linq.Fragments;

public static class GroupBySupport{
    public static string GroupingClass(string keyType,string elementType)=>$$"""
        private sealed class Grouping:global::System.Linq.IGrouping<{{keyType}},{{elementType}}>{
            private readonly global::System.Collections.Generic.List<{{elementType}}> values=new();
            public Grouping({{keyType}} key){
                this.Key=key;
            }
            public {{keyType}} Key{get;}
            public void Add({{elementType}} value)=>values.Add(value);
            public global::System.Collections.Generic.IEnumerator<{{elementType}}> GetEnumerator()=>values.GetEnumerator();
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
        }
        """;

    public static string LookupFields(string keyType,string lookupValueType,string groupListType,string lookupName,string groupName,string indexName)=>$$"""
        private global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>? {{lookupName}};
        private global::System.Collections.Generic.List<{{groupListType}}>? {{groupName}};
        private int {{indexName}}=-1;
        """;

    public static string BuildGroupingLookupBody(
        string enumeratorName,
        bool canProvideSpan,
        string keyType,
        string groupingType,
        string keyAssignCode,
        string groupAddCode,
        string lookupName,
        string groupName,
        string indexName,
        string itemName,
        string keyName)=>BuildLookupBody(
            enumeratorName,
            canProvideSpan,
            keyType,
            groupingType,
            groupingType,
            $"new {groupingType}({keyName})",
            keyAssignCode,
            groupAddCode,
            lookupName,
            groupName,
            indexName,
            itemName,
            keyName);

    public static string BuildListLookupBody(
        string enumeratorName,
        bool canProvideSpan,
        string keyType,
        string lookupValueType,
        string groupListType,
        string lookupValueCreateExpression,
        string keyAssignCode,
        string groupAddCode,
        string lookupName,
        string groupName,
        string indexName,
        string itemName,
        string keyName)=>BuildLookupBody(
            enumeratorName,
            canProvideSpan,
            keyType,
            lookupValueType,
            groupListType,
            lookupValueCreateExpression,
            keyAssignCode,
            groupAddCode,
            lookupName,
            groupName,
            indexName,
            itemName,
            keyName);

    public static string BuildLookupInitialization(
        string enumeratorName,
        bool canProvideSpan,
        string keyType,
        string lookupValueType,
        string groupListType,
        string lookupValueCreateExpression,
        string keyAssignCode,
        string groupAddCode,
        string groupListAddCode,
        string lookupName,
        string groupName,
        string itemName,
        string keyName)=>canProvideSpan ? $$"""
        if(this.{{lookupName}} is null){
            {{enumeratorName}}.TryGetSpan(out var __span);
            this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>(__span.Length);
            this.{{groupName}}=new global::System.Collections.Generic.List<{{groupListType}}>(__span.Length);
            foreach(var {{itemName}} in __span){
                {{keyType}} {{keyName}}=default!;
                {{IndentBlock(keyAssignCode,"                ")}}
                if(!this.{{lookupName}}.TryGetValue({{keyName}},out var __group)){
                    __group={{lookupValueCreateExpression}};
                    this.{{lookupName}}.Add({{keyName}},__group);
                    {{groupListAddCode}}
                }
                {{IndentBlock(groupAddCode,"                ")}}
            }
        }
        """ : $$"""
        if(this.{{lookupName}} is null){
            if({{enumeratorName}}.TryGetNonEnumeratedCount(out var __count)){
                this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>(__count);
                this.{{groupName}}=new global::System.Collections.Generic.List<{{groupListType}}>(__count);
            }else{
                this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>();
                this.{{groupName}}=new global::System.Collections.Generic.List<{{groupListType}}>();
            }
            while({{enumeratorName}}.MoveNext()){
                var {{itemName}}={{enumeratorName}}.Current;
                {{keyType}} {{keyName}}=default!;
                {{IndentBlock(keyAssignCode,"                ")}}
                if(!this.{{lookupName}}.TryGetValue({{keyName}},out var __group)){
                    __group={{lookupValueCreateExpression}};
                    this.{{lookupName}}.Add({{keyName}},__group);
                    {{groupListAddCode}}
                }
                {{IndentBlock(groupAddCode,"                ")}}
            }
        }
        """;

    private static string BuildLookupBody(
        string enumeratorName,
        bool canProvideSpan,
        string keyType,
        string lookupValueType,
        string groupListType,
        string lookupValueCreateExpression,
        string keyAssignCode,
        string groupAddCode,
        string lookupName,
        string groupName,
        string indexName,
        string itemName,
        string keyName){
        return canProvideSpan ? $$"""
            if(this.{{lookupName}} is null){
                {{enumeratorName}}.TryGetSpan(out var __span);
                this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>(__span.Length);
                this.{{groupName}}=new global::System.Collections.Generic.List<{{groupListType}}>(__span.Length);
                foreach(var {{itemName}} in __span){
                    {{keyType}} {{keyName}}=default!;
                    {{IndentBlock(keyAssignCode,"                    ")}}
                    if(!this.{{lookupName}}.TryGetValue({{keyName}},out var __group)){
                        __group={{lookupValueCreateExpression}};
                        this.{{lookupName}}.Add({{keyName}},__group);
                        this.{{groupName}}.Add(__group);
                    }
                    {{IndentBlock(groupAddCode,"                    ")}}
                }
            }
            this.{{indexName}}++;
            if(this.{{groupName}} is not null&&(uint)this.{{indexName}}<(uint)this.{{groupName}}.Count){
                current=this.Current=this.{{groupName}}[this.{{indexName}}];
                return true;
            }
            current=default!;
            return false;
            """ : $$"""
            if(this.{{lookupName}} is null){
                if({{enumeratorName}}.TryGetNonEnumeratedCount(out var __count)){
                    this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>(__count);
                    this.{{groupName}}=new global::System.Collections.Generic.List<{{groupListType}}>(__count);
                }else{
                    this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>();
                    this.{{groupName}}=new global::System.Collections.Generic.List<{{groupListType}}>();
                }
                while({{enumeratorName}}.MoveNext()){
                    var {{itemName}}={{enumeratorName}}.Current;
                    {{keyType}} {{keyName}}=default!;
                    {{IndentBlock(keyAssignCode,"                    ")}}
                    if(!this.{{lookupName}}.TryGetValue({{keyName}},out var __group)){
                        __group={{lookupValueCreateExpression}};
                        this.{{lookupName}}.Add({{keyName}},__group);
                        this.{{groupName}}.Add(__group);
                    }
                    {{IndentBlock(groupAddCode,"                    ")}}
                }
            }
            this.{{indexName}}++;
            if(this.{{groupName}} is not null&&(uint)this.{{indexName}}<(uint)this.{{groupName}}.Count){
                current=this.Current=this.{{groupName}}[this.{{indexName}}];
                return true;
            }
            current=default!;
            return false;
            """;
    }

    public static string IndentBlock(string text,string indent){
        return string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line=>line.Length==0 ? line : indent+line));
    }
}
