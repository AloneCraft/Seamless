namespace Seamless.FLinq.Generator;

// 単段パイプラインを Consume へ直接展開する経路。
internal sealed partial class PipelineEnumerable {
    public override string GetConsumePreparationStatement() {
        if(source is not RangeRoot range||stages.Count!=1) {
            return base.GetConsumePreparationStatement();
        }

        var stage = stages[0];
        if(stage.Kind is not (PipelineStageKind.Select or PipelineStageKind.Where or PipelineStageKind.SkipWhile)
            ||stage.Func is not { } function
            ||function.RequiresStatementInlining
            ||!function.CanInlineAssign) {
            return base.GetConsumePreparationStatement();
        }

        var functionDeclarations = string.Join("\n",new[] {
            this.HoistedLocalDeclarations(function),
            this.StaticDelegateLocalDeclarations(function),
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(functionDeclarations)
            ? range.RenderArgumentPreparation(range.PreparedStartName,range.PreparedCountName)
            : $$"""
                {{range.RenderArgumentPreparation(range.PreparedStartName,range.PreparedCountName)}}
                {{functionDeclarations}}
                """;
    }

    public override string GetConsumeExecutionStatement(string consumerName) =>
        this.RenderRangeParityConsume(consumerName)
        ??this.RenderRangeUnionConsume(consumerName)
        ??this.RenderSpanReverseConsume(consumerName)
        ??this.RenderConcatThenStatelessConsume(consumerName)
        ??this.RenderSingleTakeWhileDirectConsume(consumerName)
        ??this.RenderSingleSkipWhileDirectConsume(consumerName)
        ??this.RenderSingleConcatDirectConsume(consumerName)
        ??this.RenderSingleAppendOrPrependDirectConsume(consumerName)
        ??this.RenderSingleDefaultIfEmptyDirectConsume(consumerName)
        ??this.RenderSingleSelectDirectConsume(consumerName)
        ??this.RenderSingleWhereDirectConsume(consumerName)
        ??this.RenderPureOrderingDirectConsume(consumerName)
        ??this.RenderStatelessChainDirectConsume(consumerName)
        ??this.RenderFlattenedConsume(consumerName)
        ??base.GetConsumeExecutionStatement(consumerName);

    // 段をまとめた列挙子の構造体を作らずに Consume する。構造体のコンストラクタ引数とフィールドを局所変数、
    // TryGetNext の本体を局所関数にして差し替えメソッドの中に展開し、局所関数から根(と第2ソース)の列挙子を直接呼ぶ。
    // 構造体→根の構造体の入れ子の TryGetNext は、参照型の要素の共有ジェネリックコードで内側がインライン化されず遅い
    // (Work/exp/nestedconsume: 入れ子 14.8µs、1段 4.05µs)。Range は要素が int で入れ子の問題が起きないので対象にしない。
    // フィールドが単純な宣言だけで、構造体の型引数(TEnum など)を本体が使わないときだけ展開する。
    private string? RenderFlattenedConsume(string consumerName) {
        // 巻き上げた値・デリゲートのフィールドを持つ関数は、構造体のフィールドの作り方が別なので対象にしない。
        if(source is RangeRoot
            ||this.Funcs.Any(static function => function.HoistedLocals.Length!=0||function.CanInvokeAsDelegate)) {
            return null;
        }
        string sourceExpression;
        string fields;
        string constructor;
        string body;
        string dispose;
        string elementType;
        try {
            sourceExpression=source.GetEnumeratorExpression;
            fields=this.GetEnumeratorFields;
            constructor=this.GetEnumeratorConstructor;
            body=this.GetTryGetNextBody;
            dispose=this.GetEnumeratorDisposeBody;
            elementType=this.ElementType;
        } catch(NotSupportedException) {
            return null;
        }
        if(elementType.Contains("TResult")
            ||StructTypeParameterReference.IsMatch(fields+constructor+body+dispose)) {
            return null;
        }
        var parameters = EnumeratorDefinitionRenderModel.ParseCtorParams(this.GetEnumeratorPrimaryConstructorParameters).ToArray();
        var arguments = this.EnumeratorArguments(sourceExpression);
        if(parameters.Length!=arguments.Count) {
            return null;
        }
        // バッファを作る段などは「フィールドと同じ名前の局所変数を宣言して、最後に this.フィールド へ移す」形を使う。
        // this. を外すと同じ名前になるので、本体で同じ名前の局所変数を宣言するフィールドだけ、外側の局所変数の名前を付け替える。
        var allText = constructor+"\n"+body+"\n"+dispose;
        string Rename(string name) => global::System.Text.RegularExpressions.Regex.IsMatch(allText,$@"\bvar\s+{name}\b") ? name+"__field" : name;
        var renamedNames = new List<string>();
        var locals = new StringBuilder();
        for(var index = 0;index<parameters.Length;index++) {
            var (typeExpression,name) = parameters[index];
            var localName = Rename(name);
            if(localName!=name) {
                renamedNames.Add(name);
            }
            locals.AppendLine(StructTypeParameterReference.IsMatch(typeExpression)
                ? $"var {localName}={arguments[index]};"
                : $"{typeExpression} {localName}={arguments[index]};");
        }
        foreach(var line in fields.Replace("\r\n","\n").Split('\n').Select(static line => line.Trim()).Where(static line => line.Length!=0)) {
            var match = SimpleFieldDeclaration.Match(line);
            if(!match.Success) {
                return null;
            }
            var name = match.Groups["name"].Value;
            var localName = Rename(name);
            if(localName!=name) {
                renamedNames.Add(name);
            }
            var initializer = match.Groups["init"].Success ? match.Groups["init"].Value : "default!";
            locals.AppendLine($"{match.Groups["type"].Value} {localName}={initializer};");
        }
        // 付け替えたフィールドは、this. 付きの参照だけが外側の局所変数を指す(this. の無い参照は内側の同名の局所変数)。
        foreach(var name in renamedNames) {
            var qualified = new global::System.Text.RegularExpressions.Regex($@"(?<![@\w.])this\.{name}\b");
            constructor=qualified.Replace(constructor,name+"__field");
            body=qualified.Replace(body,name+"__field");
            dispose=qualified.Replace(dispose,name+"__field");
        }
        var nextName = $"__next{this.result}";
        var itemName = $"__flatItem{this.result}";
        var flatBody = StripThis(body);
        // 構造体の Current プロパティへの代入は局所変数で受ける(読まないなら宣言しない)。
        var currentDeclaration = flatBody.Contains("Current=") ? $"{elementType} Current=default!;" : string.Empty;
        return $$"""
            {
                {{IndentLines(locals.ToString().TrimEnd(),"    ").TrimStart()}}
                {{currentDeclaration}}
                {{IndentLines(StripThis(constructor),"    ").TrimStart()}}
                try{
                    while({{nextName}}(out var {{itemName}})){
                        {{consumerName}}.Consume({{itemName}});
                    }
                }finally{
                    {{IndentLines(StripThis(dispose),"        ").TrimStart()}}
                }
                [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                bool {{nextName}}(out {{elementType}} current){
                    {{IndentLines(flatBody,"        ").TrimStart()}}
                }
            }
            """;
    }

    private static readonly global::System.Text.RegularExpressions.Regex StructTypeParameterReference =
        new(@"\b(TEnum|TSecond\d*|TResult)\b",global::System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static readonly global::System.Text.RegularExpressions.Regex SimpleFieldDeclaration =
        new(@"^private\s+(?:readonly\s+)?(?<type>[^=;{}]+?)\s+(?<name>[A-Za-z_]\w*)(?:\s*=\s*(?<init>[^;{}]+))?;$",global::System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static readonly global::System.Text.RegularExpressions.Regex ThisMemberAccess =
        new(@"(?<![@\w.])this\.",global::System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string StripThis(string text) => ThisMemberAccess.Replace(text,string.Empty);

    // Range(昇順・降順)の先頭の Where が値か添字の偶奇だけを見るとき、述語を評価せずに2刻みで回し、続く Select/Where(添字なし)はその場に展開する。
    // FLinq の契約(.Anchor の無い述語は純粋)で、述語の評価を省いてよい。
    private string? RenderRangeParityConsume(string consumerName) {
        if(source is not (RangeRoot or DescendingRangeRoot)
            ||source.TreeContainsAnchor()
            ||stages.Count<1
            ||stages[0] is not { Kind: PipelineStageKind.Where,Func: { } predicate } first
            ||predicate.ContainsAnchor
            ||!RangePredicateOptimizer.TryExtractParity(predicate,first.UsesIndex,out var wantEven)
            ||stages.Skip(1).Any(static stage => stage.Kind is not (PipelineStageKind.Select or PipelineStageKind.Where)
                ||stage.UsesIndex
                ||stage.Func is not { } function
                ||function.RequiresStatementInlining
                ||function.CanInvokeAsDelegate
                ||!function.CanInlineAssign)) {
            return null;
        }
        var descending = source is DescendingRangeRoot;
        var (startExpression,countExpression) = source switch {
            RangeRoot ascendingRange => (ascendingRange.StartExpression,ascendingRange.CountExpression),
            DescendingRangeRoot descendingRange => (descendingRange.StartExpression,descendingRange.CountExpression),
            _ => throw new InvalidOperationException(),
        };
        var prepare = new RangeRoot(0,startExpression,countExpression);
        var start = $"__parityStart{this.result}";
        var count = $"__parityCount{this.result}";
        var end = $"__parityEnd{this.result}";
        var value = $"__parityValue{this.result}";
        var itemName = this.ItemName(0);
        var functions = stages.Skip(1).Select(static stage => stage.Func!).ToArray();
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(functions),
            this.StaticDelegateLocalDeclarations(functions),
        }.Where(static text => !string.IsNullOrWhiteSpace(text)));
        var body = new StringBuilder();
        var currentName = itemName;
        for(var index = 1;index<stages.Count;index++) {
            currentName=this.AppendStage(body,stages[index],currentName,index);
        }
        body.AppendLine($"    {consumerName}.Consume({currentName});");
        var loopBody = IndentLines(body.ToString().TrimEnd(),"    ");
        // 最初に通る値: 値の偶奇なら端の値の偶奇で、添字の偶奇なら端から数えた位置(端の値は添字 0)で決まる。
        var wanted = wantEven ? "0" : "1";
        string firstValue;
        string loop;
        if(!descending) {
            firstValue=first.UsesIndex
                ? $"(long){start}+{wanted}"
                : $"(long){start}+((((long){start}&1)=={wanted}) ? 0 : 1)";
            loop=$"for(var {value}={firstValue};{value}<{end};{value}+=2)";
        } else {
            firstValue=first.UsesIndex
                ? $"{end}-1-{wanted}"
                : $"{end}-1-((((({end}-1)&1)=={wanted})) ? 0 : 1)";
            loop=$"for(var {value}={firstValue};{value}>=(long){start};{value}-=2)";
        }
        return $$"""
            {
                {{localDeclarations}}
                {{IndentLines(prepare.RenderArgumentPreparation(start,count),"    ").TrimStart()}}
                var {{end}}=(long){{start}}+{{count}};
                {{loop}}{
                    var {{itemName}}=(int){{value}};
                {{loopBody}}
                }
            }
            """;
    }

    // 先頭が Concat で、後ろに添字の無い Where/Select だけが並ぶとき、段は要素ごとなので「第1ソースに段を適用して流し、
    // 続けて第2ソースに段を適用して流す」と同じ。それぞれの根(Range・span を出せる根・IEnumerable、第2ソースは Where を
    // 剥がした根)を差し替えメソッドの中で直接回して段をその場に展開する。Range の部分で段が偶奇の Where 1つなら2刻み。
    // 評価の順は従来と同じ(第1ソース・第2ソースの式、第1ソースの要素、第2ソースの要素)。偶奇の2刻みは述語を評価しないので .Anchor があれば使わない。
    private string? RenderConcatThenStatelessConsume(string consumerName) {
        if(stages.Count<2
            ||stages[0] is not { Kind: PipelineStageKind.Concat,SecondSource: { } secondSource } concat
            ||concat.ContainsAnchor
            ||source.TreeContainsAnchor()
            ||secondSource.TreeContainsAnchor()
            ||secondSource.ElementType!=source.ElementType
            ||stages.Skip(1).Any(static stage => stage.Kind is not (PipelineStageKind.Select or PipelineStageKind.Where)
                ||stage.UsesIndex
                ||stage.Func is not { } function
                ||function.ContainsAnchor
                ||function.RequiresStatementInlining
                ||function.CanInvokeAsDelegate
                ||!function.CanInlineAssign)) {
            return null;
        }
        Enumerable firstRoot;
        FuncFragment[] firstPredicates;
        if(source is RangeRoot) {
            firstRoot=source;
            firstPredicates=System.Array.Empty<FuncFragment>();
        } else if(!TryPeelWheres(source,out firstRoot,out firstPredicates)) {
            return null;
        }
        if(!TryPeelWheres(secondSource,out var secondRoot,out var secondPredicates)) {
            return null;
        }
        string firstExpression;
        string secondExpression;
        try {
            firstExpression=firstRoot is RangeRoot ? string.Empty : firstRoot.GetEnumeratorExpression;
            secondExpression=secondRoot.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        var firstName = $"__concatFirstRoot{this.result}";
        var secondName = $"__concatSecondRoot{this.result}";
        var firstItemName = $"__concatFirstItem{this.result}";
        var secondItemName = $"__concatSecondItem{this.result}";
        var functions = stages.Skip(1).Select(static stage => stage.Func!).Concat(firstPredicates).Concat(secondPredicates).ToArray();
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(functions),
            this.StaticDelegateLocalDeclarations(functions),
        }.Where(static text => !string.IsNullOrWhiteSpace(text)));
        string StagesBody(string itemName) {
            var body = new StringBuilder();
            body.AppendLine($"    var {this.ItemName(1)}={itemName};");
            var currentName = this.ItemName(1);
            for(var index = 1;index<stages.Count;index++) {
                currentName=this.AppendStage(body,stages[index],currentName,index);
            }
            body.AppendLine($"    {consumerName}.Consume({currentName});");
            return "{\n"+body.ToString().TrimEnd()+"\n}";
        }
        string firstLoop;
        if(firstRoot is RangeRoot range) {
            var start = $"__concatStart{this.result}";
            var count = $"__concatCount{this.result}";
            var end = $"__concatEnd{this.result}";
            var value = $"__concatValue{this.result}";
            var prepare = range.RenderArgumentPreparation(start,count);
            if(stages.Count==2&&stages[1].Kind==PipelineStageKind.Where&&RangePredicateOptimizer.TryExtractParity(stages[1].Func!,false,out var wantEven)) {
                var wanted = wantEven ? "0" : "1";
                firstLoop=$$"""
                    {{prepare}}
                    var {{end}}=(long){{start}}+{{count}};
                    for(var {{value}}=(long){{start}}+((((long){{start}}&1)=={{wanted}}) ? 0 : 1);{{value}}<{{end}};{{value}}+=2){
                        {{consumerName}}.Consume((int){{value}});
                    }
                    """;
            } else {
                firstLoop=$$"""
                    {{prepare}}
                    var {{end}}=(long){{start}}+{{count}};
                    for(var {{value}}=(long){{start}};{{value}}<{{end}};{{value}}++){
                        var {{firstItemName}}=(int){{value}};
                    {{IndentLines(StagesBody(firstItemName),"    ")}}
                    }
                    """;
            }
        } else {
            firstLoop=$"using var {firstName}={firstExpression};\n"+this.RenderRootLoop(firstRoot,firstPredicates,firstName,firstItemName,StagesBody(firstItemName));
        }
        // 第1ソースが Range のときも、第2ソースの式は第1ソースの引数の検査の後、要素を流す前に評価する(従来の構造体の作成と同じ順)。
        var secondDeclaration = $"using var {secondName}={secondExpression};";
        string body;
        if(firstRoot is RangeRoot) {
            var split = firstLoop.IndexOf("var __concatEnd",StringComparison.Ordinal);
            body=firstLoop.Substring(0,split)+secondDeclaration+"\n"+firstLoop.Substring(split);
        } else {
            var split = firstLoop.IndexOf('\n');
            body=firstLoop.Substring(0,split+1)+secondDeclaration+"\n"+firstLoop.Substring(split+1);
        }
        return $$"""
            {
                {{localDeclarations}}
                {{IndentLines(body,"    ").TrimStart()}}
                {{IndentLines(this.RenderRootLoop(secondRoot,secondPredicates,secondName,secondItemName,StagesBody(secondItemName)),"    ").TrimStart()}}
            }
            """;
    }

    // span を出せる根の後に添字の無い Where/Select と Reverse が1つだけ並ぶとき、Reverse は要素ごとの段と入れ替えられる
    // (Where/Select は順を保つ)ので、span を末尾から回して段をその場に展開する(要素を溜めない)。
    // 述語・選択子を評価する順が逆になるので、.Anchor があれば使わない。
    private string? RenderSpanReverseConsume(string consumerName) {
        if(stages.Count(static stage => stage.Kind==PipelineStageKind.Reverse)!=1
            ||source.TreeContainsAnchor()
            ||stages.Any(static stage => stage.Kind!=PipelineStageKind.Reverse
                &&(stage.Kind is not (PipelineStageKind.Select or PipelineStageKind.Where)
                    ||stage.UsesIndex
                    ||stage.Func is not { } function
                    ||function.ContainsAnchor
                    ||function.RequiresStatementInlining
                    ||function.CanInvokeAsDelegate
                    ||!function.CanInlineAssign))) {
            return null;
        }
        var sourceName = $"__reverseSource{this.result}";
        if(source.GetDirectSpanExpression(sourceName) is not { } spanExpression) {
            return null;
        }
        string sourceExpression;
        try {
            sourceExpression=source.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        var spanName = $"__reverseSpan{this.result}";
        var indexName = $"__reverseIndex{this.result}";
        var itemName = this.ItemName(0);
        var functions = stages.Where(static stage => stage.Func is not null).Select(static stage => stage.Func!).ToArray();
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(functions),
            this.StaticDelegateLocalDeclarations(functions),
        }.Where(static text => !string.IsNullOrWhiteSpace(text)));
        var body = new StringBuilder();
        var currentName = itemName;
        for(var index = 0;index<stages.Count;index++) {
            if(stages[index].Kind!=PipelineStageKind.Reverse) {
                currentName=this.AppendStage(body,stages[index],currentName,index);
            }
        }
        body.AppendLine($"    {consumerName}.Consume({currentName});");
        var loopBody = IndentLines(body.ToString().TrimEnd(),"    ");
        return $$"""
            {{localDeclarations}}
            using var {{sourceName}}={{sourceExpression}};
            var {{spanName}}={{spanExpression}};
            for(var {{indexName}}={{spanName}}.Length-1;{{indexName}}>=0;{{indexName}}--){
                var {{itemName}}={{spanName}}[{{indexName}}];
            {{loopBody}}
            }
            """;
    }

    // 昇順の Range の後に添字の無い Where だけが並び、最後が Union(既定の比較子)のとき、Range の部分の値は一意で、
    // 第2ソースの値 v が Range の部分に出たかは「v が区間に入り、同じ述語をすべて通るか」で決まる(述語は .Anchor が無く純粋)。
    // Range の部分は集合を使わずに流し(述語が1つで偶奇なら2刻み)、第2ソースの値だけを集合で重複除きする。
    // 第2ソースの式は従来どおり Range の引数の検査の後、Range を流す前に評価する。
    private string? RenderRangeUnionConsume(string consumerName) {
        if(source is not RangeRoot range
            ||source.TreeContainsAnchor()
            ||stages.Count<1
            ||stages[stages.Count-1] is not { Kind: PipelineStageKind.Union,SecondSource: { } secondSource } union
            ||union.ContainsAnchor
            ||union.Value!="null"
            ||source.ElementType!="int"
            ||secondSource.ElementType!="int"
            ||secondSource.TreeContainsAnchor()) {
            return null;
        }
        var wheres = stages.Take(stages.Count-1).ToArray();
        if(wheres.Any(static stage => stage.Kind!=PipelineStageKind.Where
            ||stage.UsesIndex
            ||stage.Func is not { } predicate
            ||predicate.ContainsAnchor
            ||predicate.RequiresStatementInlining
            ||predicate.CanInvokeAsDelegate
            ||!predicate.CanInlineAssign)) {
            return null;
        }
        // 第2ソースが根と Where の並びなら根を直接回す(Where の構造体→根の構造体を回さない)。そうでなければ第2ソースの列挙子を回す。
        if(!TryPeelWheres(secondSource,out var secondRoot,out var secondPredicates)) {
            secondRoot=secondSource;
            secondPredicates=System.Array.Empty<FuncFragment>();
        }
        string secondExpression;
        try {
            secondExpression=secondRoot.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        var start = $"__unionStart{this.result}";
        var count = $"__unionCount{this.result}";
        var end = $"__unionEnd{this.result}";
        var value = $"__unionValue{this.result}";
        var itemName = $"__unionItem{this.result}";
        var secondName = $"__unionSecond{this.result}";
        var secondItemName = $"__unionSecondItem{this.result}";
        var seenName = $"__unionSeen{this.result}";
        var functions = wheres.Select(static stage => stage.Func!).ToArray();
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(functions.Concat(secondPredicates).ToArray()),
            this.StaticDelegateLocalDeclarations(functions.Concat(secondPredicates).ToArray()),
        }.Where(static text => !string.IsNullOrWhiteSpace(text)));
        string PassesAll(string item) => functions.Length==0
            ? "true"
            : string.Join("&&",functions.Select(function => "("+function.RenderInlineExpression(item)+")"));
        var secondBody = $"if({secondItemName}>={start}&&{secondItemName}<{end}&&{PassesAll(secondItemName)}) continue;\nif({seenName}.Add({secondItemName})){{\n    {consumerName}.Consume({secondItemName});\n}}";
        string firstLoop;
        if(functions.Length==1&&RangePredicateOptimizer.TryExtractParity(functions[0],false,out var wantEven)) {
            var wanted = wantEven ? "0" : "1";
            firstLoop=$$"""
                for(var {{value}}=(long){{start}}+((((long){{start}}&1)=={{wanted}}) ? 0 : 1);{{value}}<{{end}};{{value}}+=2){
                    {{consumerName}}.Consume((int){{value}});
                }
                """;
        } else {
            firstLoop=$$"""
                for(var {{value}}=(long){{start}};{{value}}<{{end}};{{value}}++){
                    var {{itemName}}=(int){{value}};
                    if({{PassesAll(itemName)}}){
                        {{consumerName}}.Consume({{itemName}});
                    }
                }
                """;
        }
        return $$"""
            {
                {{localDeclarations}}
                {{IndentLines(range.RenderArgumentPreparation(start,count),"    ").TrimStart()}}
                var {{end}}=(long){{start}}+{{count}};
                using var {{secondName}}={{secondExpression}};
                {{IndentLines(firstLoop,"    ").TrimStart()}}
                var {{seenName}}=new global::System.Collections.Generic.HashSet<int>();
                {{IndentLines(this.RenderRootLoop(secondRoot,secondPredicates,secondName,secondItemName,secondBody),"    ").TrimStart()}}
            }
            """;
    }

    // Select/Where だけが並ぶとき(1段で span・Range の根は既存の単段の経路が受ける)、span・Range・根の列挙子を
    // 差し替えメソッドの中で直接回し、段の式をその場に展開する。添字を使う段は差し替えメソッドの中の局所変数で数える。
    // 列挙子の構造体の TryGetNext を入れ子に呼ぶ形は、参照型の要素の共有ジェネリックコードで内側の呼び出しが
    // インライン化されず遅い(Work/exp/nestedconsume: 入れ子 14.8µs、直接 4.3µs)。
    private string? RenderStatelessChainDirectConsume(string consumerName) {
        // Range は要素が int で入れ子の問題が起きず、段の並びに Range 専用の最適化(偶奇の述語を刻みにするなど)があるので対象にしない。
        if(source is RangeRoot
            ||stages.Count<1
            ||stages.Any(static stage => stage.Kind is not (PipelineStageKind.Select or PipelineStageKind.Where)
                ||stage.Func is not { } function
                ||function.RequiresStatementInlining
                ||function.CanInvokeAsDelegate
                ||!function.CanInlineAssign)) {
            return null;
        }
        var functions = stages.Select(static stage => stage.Func!).ToArray();
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(functions),
            this.StaticDelegateLocalDeclarations(functions),
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));
        var itemName = this.ItemName(0);
        var body = new StringBuilder();
        var counters = new StringBuilder();
        var currentName = itemName;
        for(var index = 0;index<stages.Count;index++) {
            var stage = stages[index];
            if(!stage.UsesIndex) {
                currentName=this.AppendStage(body,stage,currentName,index);
                continue;
            }
            // 添字はその段に届いた要素の数え(System.Linq と同じく、述語・選択子の評価の前に進める)。
            var counter = $"__chainIndex{this.result}_{index}";
            counters.AppendLine($"var {counter}=-1;");
            body.AppendLine($"    {counter}++;");
            if(stage.Kind==PipelineStageKind.Where) {
                var (preCode,condition) = this.FuncInvokeCondition(stage.Func!,nameof(PipelineStageKind.Where),this.TempName(index),currentName,counter);
                if(!string.IsNullOrEmpty(preCode)) {
                    body.AppendLine(IndentBlock(preCode,"    "));
                }
                body.AppendLine($"    if(!{WrapConditionExpression(condition)}) continue;");
            } else {
                var nextName = this.ItemName(index+1);
                body.AppendLine($"    {stage.OutputType} {nextName}=default!;");
                body.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.Select),nextName,currentName,counter),"    "));
                currentName=nextName;
            }
        }
        body.AppendLine($"    {consumerName}.Consume({currentName});");
        var loopBody = IndentLines(body.ToString().TrimEnd(),"    ");
        localDeclarations=string.Join("\n",new[] { localDeclarations,counters.ToString().TrimEnd() }.Where(static value => !string.IsNullOrWhiteSpace(value)));
        var sourceName = $"__chainSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            // span の無い根は、根の列挙子だけを差し替えメソッドから回す(入れ子は1段で、内側はインライン化される)。
            string sourceEnumeratorExpression;
            try {
                sourceEnumeratorExpression=source.GetEnumeratorExpression;
            } catch(NotSupportedException) {
                return null;
            }
            return $$"""
                {{localDeclarations}}
                using var {{sourceName}}={{sourceEnumeratorExpression}};
                {{this.CounterCall("IncrementEnumerationLoopUsed")}}
                {{DrainLoopHead(source,sourceName,itemName)}}
                {{loopBody}}
                }
                """;
        }
        var spanName = $"__chainSpan{this.result}";
        var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();
        return $$"""
            {{localDeclarations}}
            {{directSpanSetup}}
            {{this.CounterCall("IncrementEnumerationLoopUsed")}}
            for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                var {{itemName}}={{spanName}}[__i{{this.result}}];
            {{loopBody}}
            }
            """;
    }

    private string? RenderSingleAppendOrPrependDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind is not (PipelineStageKind.Append or PipelineStageKind.Prepend)
            ||stage.Value is not { } value) {
            return null;
        }

        if(source is RangeRoot range) {
            var rangeStartName = $"__appendRangeStart{this.result}";
            var rangeCountName = $"__appendRangeCount{this.result}";
            var rangeItemName = $"__appendRangeItem{this.result}";
            var rangeAppendValue = $"{consumerName}.Consume({value});";
            var rangeBefore = stage.Kind==PipelineStageKind.Prepend ? rangeAppendValue+"\n" : string.Empty;
            var rangeAfter = stage.Kind==PipelineStageKind.Append ? "\n"+rangeAppendValue : string.Empty;
            return $$"""
                {{range.RenderArgumentPreparation(rangeStartName,rangeCountName)}}
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                {{rangeBefore}}for(var __i{{this.result}}=0;__i{{this.result}}<{{rangeCountName}};__i{{this.result}}++){
                    var {{rangeItemName}}={{rangeStartName}}+__i{{this.result}};
                    {{consumerName}}.Consume({{rangeItemName}});
                }{{rangeAfter}}
                """;
        }

        var sourceName = $"__appendSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        var itemName = $"__appendItem{this.result}";
        var appendValue = $"{consumerName}.Consume({value});";
        var before = stage.Kind==PipelineStageKind.Prepend ? appendValue+"\n" : string.Empty;
        var after = stage.Kind==PipelineStageKind.Append ? "\n"+appendValue : string.Empty;
        if(directSpanExpression is null) {
            // span の無い根は、根の列挙子を差し替えメソッドから回す(入れ子の構造体を回さない)。
            string rootExpression;
            try {
                rootExpression=source.GetEnumeratorExpression;
            } catch(NotSupportedException) {
                return null;
            }
            return $$"""
                using var {{sourceName}}={{rootExpression}};
                {{before}}{{DrainLoopHead(source,sourceName,itemName)}}
                    {{consumerName}}.Consume({{itemName}});
                }{{after}}
                """;
        }

        var spanName = $"__appendSpan{this.result}";
    var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();
        return $$"""
            {{directSpanSetup}}
            {{before}}for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                var {{itemName}}={{spanName}}[__i{{this.result}}];
                {{consumerName}}.Consume({{itemName}});
            }{{after}}
            """;
    }

    private string? RenderSingleDefaultIfEmptyDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind!=PipelineStageKind.DefaultIfEmpty||stage.Value is not { } defaultValue) {
            return null;
        }

        if(source is RangeRoot range) {
            var rangeStartName = $"__defaultRangeStart{this.result}";
            var rangeCountName = $"__defaultRangeCount{this.result}";
            var rangeItemName = $"__defaultRangeItem{this.result}";
            return $$"""
                {{range.RenderArgumentPreparation(rangeStartName,rangeCountName)}}
                if({{rangeCountName}}==0){
                    {{consumerName}}.Consume({{defaultValue}});
                }else{
                    global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                    for(var __i{{this.result}}=0;__i{{this.result}}<{{rangeCountName}};__i{{this.result}}++){
                        var {{rangeItemName}}={{rangeStartName}}+__i{{this.result}};
                        {{consumerName}}.Consume({{rangeItemName}});
                    }
                }
                """;
        }

        if(source is Root { DirectEnumerableExpression: { } directEnumerable }) {
            return $$"""
                {
                    var __defaultIfEmptySource={{directEnumerable}};
                    var __defaultIfEmptyValue={{defaultValue}};
                    global::System.ArgumentNullException.ThrowIfNull(__defaultIfEmptySource);
                    using(var __defaultIfEmptyEnumerator=__defaultIfEmptySource.GetEnumerator()){
                        if(__defaultIfEmptyEnumerator.MoveNext()){
                            do{
                                {{consumerName}}.Consume(__defaultIfEmptyEnumerator.Current);
                            }while(__defaultIfEmptyEnumerator.MoveNext());
                        }else{
                            {{consumerName}}.Consume(__defaultIfEmptyValue);
                        }
                    }
                }
                """;
        }
        var sourceName = $"__defaultSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__defaultSpan{this.result}";
    var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();
        var itemName = $"__defaultItem{this.result}";
        return $$"""
            {{directSpanSetup}}
            if({{spanName}}.Length==0){
                {{consumerName}}.Consume({{defaultValue}});
            }else{
                for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                    var {{itemName}}={{spanName}}[__i{{this.result}}];
                    {{consumerName}}.Consume({{itemName}});
                }
            }
            """;
    }

    private string? RenderSingleConcatDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind!=PipelineStageKind.Concat||stage.SecondSource is not { } secondSource) {
            return null;
        }

        var sourceName = $"__concatSource{this.result}";
        var secondName = $"__concatSecond{this.result}";
        var builder = new StringBuilder();
        this.AppendConcatEnumeratorDeclaration(builder,source,sourceName);
        this.AppendConcatEnumeratorDeclaration(builder,secondSource,secondName);
        this.AppendConcatConsumeSource(builder,source,sourceName,consumerName,$"__concatLeftSpan{this.result}",$"__concatLeftItem{this.result}",source.ElementType);
        this.AppendConcatConsumeSource(builder,secondSource,secondName,consumerName,$"__concatRightSpan{this.result}",$"__concatRightItem{this.result}",stage.OutputType);
        return builder.ToString().TrimEnd();
    }

    // RangeRoot は start+i で値を計算できるので enumerator の構築自体が要らない。
    private void AppendConcatEnumeratorDeclaration(StringBuilder builder,Enumerable sourceFragment,string enumeratorName) {
        if(sourceFragment is RangeRoot) {
            return;
        }
        builder.AppendLine($"using var {enumeratorName}={sourceFragment.GetEnumeratorExpression};");
    }

    private void AppendConcatConsumeSource(StringBuilder builder,Enumerable sourceFragment,string enumeratorName,string consumerName,string spanName,string itemName,string outputType) {
        if(sourceFragment is RangeRoot range) {
            var rangeStartName = $"{itemName}Start";
            var rangeCountName = $"{itemName}Count";
            builder.AppendLine(range.RenderArgumentPreparation(rangeStartName,rangeCountName));
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();");
            builder.AppendLine($"for(var __i{this.result}=0;__i{this.result}<{rangeCountName};__i{this.result}++) {{");
            builder.AppendLine($"    var {itemName}={rangeStartName}+__i{this.result};");
            builder.AppendLine($"    {consumerName}.Consume({this.CastConcatItem(sourceFragment.ElementType,outputType,itemName)});");
            builder.AppendLine("}");
            return;
        }

        var directSpanExpression = sourceFragment.GetDirectSpanExpression(enumeratorName);
        if(directSpanExpression is not null) {
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();");
            builder.AppendLine($"var {spanName}={directSpanExpression};");
            builder.AppendLine($"for(var __i{this.result}=0;__i{this.result}<{spanName}.Length;__i{this.result}++) {{");
            builder.AppendLine($"    var {itemName}={spanName}[__i{this.result}];");
            builder.AppendLine($"    {consumerName}.Consume({this.CastConcatItem(sourceFragment.ElementType,outputType,itemName)});");
            builder.AppendLine("}");
            return;
        }

        builder.AppendLine($"while({enumeratorName}.TryGetNext(out var {itemName})) {{");
        this.AppendCounterCall(builder,"    ","IncrementPhysicalMoveNext");
        builder.AppendLine($"    {consumerName}.Consume({this.CastConcatItem(sourceFragment.ElementType,outputType,itemName)});");
        builder.AppendLine("}");
    }

    private string CastConcatItem(string inputType,string outputType,string itemName) =>
        inputType==outputType
            ? itemName
            : $"({outputType})(object?){itemName}!";

    private string? RenderSingleWhereDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind!=PipelineStageKind.Where
            ||stage.Func is not { } predicate
            ||predicate.RequiresStatementInlining
            ||!predicate.CanInlineAssign) {
            return null;
        }

        if(source is RangeRoot range) {
            var rangeItemName = $"__whereRangeItem{this.result}";
            var rangeIndexName = $"__whereRangeIndex{this.result}";
            var rangeArguments = stage.UsesIndex
                ? new[] { rangeItemName,rangeIndexName }
                : new[] { rangeItemName };
            var rangeIndexDeclaration = stage.UsesIndex
                ? $"var {rangeIndexName}=-1;\n"
                : string.Empty;
            var rangeIndexIncrement = stage.UsesIndex
                ? $"{rangeIndexName}++;\n    "
                : string.Empty;
            var (rangePreCode,rangeConditionExpr)=this.FuncInvokeCondition(predicate,nameof(PipelineStageKind.Where),this.temp,rangeArguments);
            var rangePredicateCheck = string.IsNullOrEmpty(rangePreCode)
                ? $"if({WrapConditionExpression(rangeConditionExpr)})"
                : $"{rangePreCode}\n    if({WrapConditionExpression(rangeConditionExpr)})";

            return $$"""
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                {{rangeIndexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{range.PreparedCountName}};__i{{this.result}}++){
                    var {{rangeItemName}}={{range.PreparedStartName}}+__i{{this.result}};
                    {{rangeIndexIncrement}}{{rangePredicateCheck}} {
                        {{consumerName}}.Consume({{rangeItemName}});
                    }
                }
                """;
        }

        var sourceName = $"__whereSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__whereSpan{this.result}";
    var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();
        var itemName = $"__whereItem{this.result}";
        var indexName = $"__whereIndex{this.result}";
        var arguments = stage.UsesIndex
            ? new[] { itemName,indexName }
            : new[] { itemName };
        var indexDeclaration = stage.UsesIndex
            ? $"var {indexName}=-1;\n"
            : string.Empty;
        var indexIncrement = stage.UsesIndex
            ? $"{indexName}++;\n        "
            : string.Empty;
        var (preCode,conditionExpr)=this.FuncInvokeCondition(predicate,nameof(PipelineStageKind.Where),this.temp,arguments);
        var predicateCheck = string.IsNullOrEmpty(preCode)
            ? $"if({WrapConditionExpression(conditionExpr)})"
            : $"{preCode}\n        if({WrapConditionExpression(conditionExpr)})";
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(predicate),
            this.StaticDelegateLocalDeclarations(predicate),
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        return $$"""
            {{localDeclarations}}
            {{directSpanSetup}}
            {{indexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                var {{itemName}}={{spanName}}[__i{{this.result}}];
                {{indexIncrement}}{{predicateCheck}} {
                    {{consumerName}}.Consume({{itemName}});
                }
            }
            """;
    }

    private string? RenderSingleSelectDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind!=PipelineStageKind.Select
            ||stage.Func is not { } selector
            ||selector.RequiresStatementInlining
            ||!selector.CanInlineAssign) {
            return null;
        }

        if(source is RangeRoot range) {
            var rangeItemName = $"__selectRangeItem{this.result}";
            var rangeSelectedName = $"__selectRangeSelected{this.result}";
            var rangeIndexName = $"__selectRangeIndex{this.result}";
            var rangeArguments = stage.UsesIndex
                ? new[] { rangeItemName,rangeIndexName }
                : new[] { rangeItemName };
            var rangeIndexDeclaration = stage.UsesIndex
                ? $"var {rangeIndexName}=-1;\n"
                : string.Empty;
            var rangeIndexIncrement = stage.UsesIndex
                ? $"{rangeIndexName}++;\n    "
                : string.Empty;

            return $$"""
                {{this.CounterCall("IncrementEnumerationLoopUsed")}}
                {{rangeIndexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{range.PreparedCountName}};__i{{this.result}}++){
                    var {{rangeItemName}}={{range.PreparedStartName}}+__i{{this.result}};
                    {{rangeIndexIncrement}}{{stage.OutputType}} {{rangeSelectedName}}=default!;
                {{IndentLines(this.FuncAssign(selector,nameof(PipelineStageKind.Select),rangeSelectedName,rangeArguments),"    ")}}
                    {{consumerName}}.Consume({{rangeSelectedName}});
                }
                """;
        }

        var sourceName = $"__selectSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__selectSpan{this.result}";
    var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();
        var itemName = $"__selectItem{this.result}";
        var selectedName = $"__selectSelected{this.result}";
        var indexName = $"__selectIndex{this.result}";
        var arguments = stage.UsesIndex
            ? new[] { itemName,indexName }
            : new[] { itemName };
        var indexDeclaration = stage.UsesIndex
            ? $"var {indexName}=-1;\n"
            : string.Empty;
        var indexIncrement = stage.UsesIndex
            ? $"{indexName}++;\n        "
            : string.Empty;
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(selector),
            this.StaticDelegateLocalDeclarations(selector),
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        return $$"""
            {{localDeclarations}}
            {{directSpanSetup}}
            {{indexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                var {{itemName}}={{spanName}}[__i{{this.result}}];
                {{indexIncrement}}{{stage.OutputType}} {{selectedName}}=default!;
            {{IndentLines(this.FuncAssign(selector,nameof(PipelineStageKind.Select),selectedName,arguments),"    ")}}
                {{consumerName}}.Consume({{selectedName}});
            }
            """;
    }

    private string? RenderSingleTakeWhileDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind!=PipelineStageKind.TakeWhile
            ||stage.Func is not { } predicate
            ||predicate.RequiresStatementInlining
            ||!predicate.CanInlineAssign) {
            return null;
        }

        if(source is RangeRoot range) {
            var rangeStartName = $"__takeWhileRangeStart{this.result}";
            var rangeCountName = $"__takeWhileRangeCount{this.result}";
            var rangeItemName = $"__takeWhileRangeItem{this.result}";
            var rangeIndexName = $"__takeWhileRangeIndex{this.result}";
            var rangeArguments = stage.UsesIndex
                ? new[] { rangeItemName,rangeIndexName }
                : new[] { rangeItemName };
            var rangeIndexDeclaration = stage.UsesIndex
                ? $"var {rangeIndexName}=-1;\n"
                : string.Empty;
            var rangeIndexIncrement = stage.UsesIndex
                ? $"{rangeIndexName}++;\n    "
                : string.Empty;
            var rangeLocalDeclarations = string.Join("\n",new[]{
                this.HoistedLocalDeclarations(predicate),
                this.StaticDelegateLocalDeclarations(predicate),
            }.Where(static text => !string.IsNullOrEmpty(text)));
            var rangeLocalDeclarationBlock = string.IsNullOrEmpty(rangeLocalDeclarations)
                ? string.Empty
                : rangeLocalDeclarations+"\n";
            var (rangePreCode,rangeConditionExpr)=this.FuncInvokeConditionStatic(predicate,nameof(PipelineStageKind.TakeWhile),this.temp,rangeArguments);
            var rangePredicateCheck = string.IsNullOrEmpty(rangePreCode)
                ? $"if(!{WrapConditionExpression(rangeConditionExpr)})"
                : $"{rangePreCode}\n    if(!{WrapConditionExpression(rangeConditionExpr)})";

            return $$"""
                {{rangeLocalDeclarationBlock}}
                {{range.RenderArgumentPreparation(rangeStartName,rangeCountName)}}
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                {{rangeIndexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{rangeCountName}};__i{{this.result}}++){
                    var {{rangeItemName}}={{rangeStartName}}+__i{{this.result}};
                    {{rangeIndexIncrement}}{{rangePredicateCheck}} {
                        break;
                    }
                    {{consumerName}}.Consume({{rangeItemName}});
                }
                """;
        }

        var sourceName = $"__takeWhileSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        var spanName = $"__takeWhileSpan{this.result}";
        var itemName = $"__takeWhileItem{this.result}";
        var indexName = $"__takeWhileIndex{this.result}";
        var arguments = stage.UsesIndex
            ? new[] { itemName,indexName }
            : new[] { itemName };
        var indexDeclaration = stage.UsesIndex
            ? $"var {indexName}=-1;\n"
            : string.Empty;
        var indexIncrement = stage.UsesIndex
            ? $"{indexName}++;\n        "
            : string.Empty;
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(predicate),
            this.StaticDelegateLocalDeclarations(predicate),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var localDeclarationBlock = string.IsNullOrEmpty(localDeclarations)
            ? string.Empty
            : localDeclarations+"\n";
        var (preCode,conditionExpr)=this.FuncInvokeConditionStatic(predicate,nameof(PipelineStageKind.TakeWhile),this.temp,arguments);
        var predicateCheck = string.IsNullOrEmpty(preCode)
            ? $"if(!{WrapConditionExpression(conditionExpr)})"
            : $"{preCode}\n        if(!{WrapConditionExpression(conditionExpr)})";
        if(directSpanExpression is null) {
            // span の無い根は、根の列挙子を差し替えメソッドから回す(入れ子の構造体を回さない)。
            string rootExpression;
            try {
                rootExpression=source.GetEnumeratorExpression;
            } catch(NotSupportedException) {
                return null;
            }
            return $$"""
                {{localDeclarationBlock}}
                using var {{sourceName}}={{rootExpression}};
                {{indexDeclaration}}{{DrainLoopHead(source,sourceName,itemName)}}
                    {{indexIncrement}}{{predicateCheck}} {
                        break;
                    }
                    {{consumerName}}.Consume({{itemName}});
                }
                """;
        }
        var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();

        return $$"""
            {{localDeclarationBlock}}
            {{directSpanSetup}}
            {{indexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                var {{itemName}}={{spanName}}[__i{{this.result}}];
                {{indexIncrement}}{{predicateCheck}} {
                    break;
                }
                {{consumerName}}.Consume({{itemName}});
            }
            """;
    }

    private string? RenderSingleSkipWhileDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind!=PipelineStageKind.SkipWhile
            ||stage.Func is not { } predicate
            ||predicate.RequiresStatementInlining
            ||!predicate.CanInlineAssign) {
            return null;
        }

        if(source is RangeRoot range) {
            return this.RenderSkipWhileRangeDirectConsume(consumerName,range,stage,predicate);
        }

        var sourceName = $"__skipWhileSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        var spanName = $"__skipWhileSpan{this.result}";
        var itemName = $"__skipWhileItem{this.result}";
        var indexName = $"__skipWhileIndex{this.result}";
        var offsetName = $"__skipWhileOffset{this.result}";
        var arguments = stage.UsesIndex
            ? new[] { itemName,indexName }
            : new[] { itemName };
        var indexDeclaration = stage.UsesIndex
            ? $"var {indexName}=-1;\n"
            : string.Empty;
        var indexIncrement = stage.UsesIndex
            ? $"{indexName}++;\n        "
            : string.Empty;
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(predicate),
            this.StaticDelegateLocalDeclarations(predicate),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var localDeclarationBlock = string.IsNullOrEmpty(localDeclarations)
            ? string.Empty
            : localDeclarations+"\n";
        var (preCode,conditionExpr)=this.FuncInvokeConditionStatic(predicate,nameof(PipelineStageKind.SkipWhile),this.temp,arguments);
        var predicateCheck = string.IsNullOrEmpty(preCode)
            ? $"if(!{WrapConditionExpression(conditionExpr)})"
            : $"{preCode}\n            if(!{WrapConditionExpression(conditionExpr)})";
        if(directSpanExpression is null) {
            // span の無い根は、根の列挙子を差し替えメソッドから回す(入れ子の構造体を回さない)。
            string rootExpression;
            try {
                rootExpression=source.GetEnumeratorExpression;
            } catch(NotSupportedException) {
                return null;
            }
            var skippingLocal = $"__skipWhileSkipping{this.result}";
            return $$"""
                {{localDeclarationBlock}}
                using var {{sourceName}}={{rootExpression}};
                var {{skippingLocal}}=true;
                {{indexDeclaration}}{{DrainLoopHead(source,sourceName,itemName)}}
                    {{indexIncrement}}if({{skippingLocal}}){
                        {{predicateCheck}} {
                            {{skippingLocal}}=false;
                        }
                    }
                    if(!{{skippingLocal}}){
                        {{consumerName}}.Consume({{itemName}});
                    }
                }
                """;
        }
        var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();

        if(stage.UsesIndex) {
            var skippingName = $"__skipWhileSkipping{this.result}";
            return $$"""
                {{localDeclarationBlock}}
                using var {{sourceName}}={{source.GetEnumeratorExpression}};
                global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
                var {{spanName}}={{directSpanExpression}};
                var {{skippingName}}=true;
                {{indexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                    var {{itemName}}={{spanName}}[__i{{this.result}}];
                    {{indexIncrement}}if({{skippingName}}){
                        {{predicateCheck}} {
                            {{skippingName}}=false;
                        }
                    }
                    if(!{{skippingName}}){
                        {{consumerName}}.Consume({{itemName}});
                    }
                }
                """;
        }

        return $$"""
            {{localDeclarationBlock}}
            {{directSpanSetup}}
            var {{offsetName}}=0;
            {{indexDeclaration}}for(;{{offsetName}}<{{spanName}}.Length;{{offsetName}}++){
                var {{itemName}}={{spanName}}[{{offsetName}}];
                {{indexIncrement}}{{predicateCheck}} {
                    {{consumerName}}.Consume({{itemName}});
                    {{offsetName}}++;
                    break;
                }
            }
            for(;{{offsetName}}<{{spanName}}.Length;{{offsetName}}++){
                var {{itemName}}={{spanName}}[{{offsetName}}];
                {{consumerName}}.Consume({{itemName}});
            }
            """;
    }

    // RangeRoot は start+i で値を計算できるので、SkipWhile も状態機械を経由せず直列 for ループにできる。
    private string RenderSkipWhileRangeDirectConsume(string consumerName,RangeRoot range,PipelineStage stage,FuncFragment predicate) {
        var rangeItemName = $"__skipWhileRangeItem{this.result}";
        var rangeIndexName = $"__skipWhileRangeIndex{this.result}";
        var rangeOffsetName = $"__skipWhileRangeOffset{this.result}";
        var rangeArguments = stage.UsesIndex
            ? new[] { rangeItemName,rangeIndexName }
            : new[] { rangeItemName };
        var rangeIndexDeclaration = stage.UsesIndex
            ? $"var {rangeIndexName}=-1;\n"
            : string.Empty;
        var rangeIndexIncrement = stage.UsesIndex
            ? $"{rangeIndexName}++;\n        "
            : string.Empty;
        var (rangePreCode,rangeConditionExpr)=this.FuncInvokeConditionStatic(predicate,nameof(PipelineStageKind.SkipWhile),this.temp,rangeArguments);
        var rangePredicateCheck = string.IsNullOrEmpty(rangePreCode)
            ? $"if(!{WrapConditionExpression(rangeConditionExpr)})"
            : $"{rangePreCode}\n            if(!{WrapConditionExpression(rangeConditionExpr)})";

        if(stage.UsesIndex) {
            var rangeSkippingName = $"__skipWhileRangeSkipping{this.result}";
            return $$"""
                {{this.CounterCall("IncrementEnumerationLoopUsed")}}
                var {{rangeSkippingName}}=true;
                {{rangeIndexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{range.PreparedCountName}};__i{{this.result}}++){
                    var {{rangeItemName}}={{range.PreparedStartName}}+__i{{this.result}};
                    {{rangeIndexIncrement}}if({{rangeSkippingName}}){
                        {{rangePredicateCheck}} {
                            {{rangeSkippingName}}=false;
                        }
                    }
                    if(!{{rangeSkippingName}}){
                        {{consumerName}}.Consume({{rangeItemName}});
                    }
                }
                """;
        }

        return $$"""
            {{this.CounterCall("IncrementEnumerationLoopUsed")}}
            var {{rangeOffsetName}}=0;
            {{rangeIndexDeclaration}}for(;{{rangeOffsetName}}<{{range.PreparedCountName}};{{rangeOffsetName}}++){
                var {{rangeItemName}}={{range.PreparedStartName}}+{{rangeOffsetName}};
                {{rangeIndexIncrement}}{{rangePredicateCheck}} {
                    {{consumerName}}.Consume({{rangeItemName}});
                    {{rangeOffsetName}}++;
                    break;
                }
            }
            for(;{{rangeOffsetName}}<{{range.PreparedCountName}};{{rangeOffsetName}}++){
                var {{rangeItemName}}={{range.PreparedStartName}}+{{rangeOffsetName}};
                {{consumerName}}.Consume({{rangeItemName}});
            }
            """;
    }

    private string? RenderPureOrderingDirectConsume(string consumerName) {
        var directOrderStages = this.OrderStages(0).ToArray();
        // 並べ替えの段だけから成るときに限る。後ろに Where/Select などが続くと、この直接ループはそれらを適用できない
        // (2026-10-02 まで適用せずに全件を流していた)。後続の段は一般経路の昇順高速経路が正しく扱う。
        if(directOrderStages.Length>0&&directOrderStages.Length==stages.Count) {
            var primaryOrder = directOrderStages[0];
            if(this.CanUseSortedDefaultOrderDirectConsumeFastPath(0,primaryOrder,directOrderStages)) {
                var sourceName = $"__orderedSource{番号}";
                var fallbackName = $"__orderedFallback{番号}";
                var spanName = $"__orderedSpan{番号}";
                var sortedName = $"__orderedSorted{番号}";
                var handledName = $"__orderedHandled{番号}";
                var transferredName = $"__orderedTransferred{番号}";
                var directItemName = $"__orderedItem{番号}";
                var directConsumeLoop = primaryOrder.Descending
                    ? $$"""
                            var __groupEnd{{番号}}={{spanName}}.Length;
                            var __orderComparer{{番号}}=global::System.Collections.Generic.Comparer<{{primaryOrder.InputType}}>.Default;
                            while(__groupEnd{{番号}}>0){
                                var __groupStart{{番号}}=__groupEnd{{番号}}-1;
                                while(__groupStart{{番号}}>0&&__orderComparer{{番号}}.Compare({{spanName}}[__groupStart{{番号}}-1],{{spanName}}[__groupEnd{{番号}}-1])==0){
                                    __groupStart{{番号}}--;
                                }
                                for(var __i=__groupStart{{番号}};__i<__groupEnd{{番号}};__i++){
                                    var {{directItemName}}={{spanName}}[__i];
                                    {{consumerName}}.Consume({{directItemName}});
                                }
                                __groupEnd{{番号}}=__groupStart{{番号}};
                            }
                        """
                    : $$"""
                            for(var __i=0;__i<{{spanName}}.Length;__i++){
                                var {{directItemName}}={{spanName}}[__i];
                                {{consumerName}}.Consume({{directItemName}});
                            }
                        """;
                // 昇順でないときは、取得済みのソース列挙子を並べ替えの列挙子へ引き渡す(ソース式を2回評価しない)。
                // 引き渡した後の破棄は並べ替えの列挙子が受け持つ。
                return $$"""
                    var {{handledName}}=false;
                    var {{transferredName}}=false;
                    var {{sourceName}}={{source.GetEnumeratorExpression}};
                    try{
                        if({{sourceName}}.TryGetSpan(out var {{spanName}})){
                            var {{sortedName}}=true;
                            for(var __i=1;__i<{{spanName}}.Length;__i++){
                                if({{this.SortedOrderOutOfOrderExpression(primaryOrder,spanName,"__i")}}){
                                    {{sortedName}}=false;
                                    break;
                                }
                            }
                            if({{sortedName}}){
                    {{directConsumeLoop}}
                                {{handledName}}=true;
                            }
                        }
                        if(!{{handledName}}){
                            {{transferredName}}=true;
                            using var {{fallbackName}}={{this.GetEnumeratorExpressionWithSource(sourceName)}};
                            while({{fallbackName}}.TryGetNext(out var {{directItemName}})){
                                {{consumerName}}.Consume({{directItemName}});
                            }
                        }
                    }finally{
                        if(!{{transferredName}}){
                            {{sourceName}}.Dispose();
                        }
                    }
                    """;
            }
        }

        // 全段が Order 系のときに System.Linq.Enumerable.OrderBy(...) へ丸ごと委譲していた経路は撤去した。
        // 委譲は「展開が遅い」ことの隠蔽であって解決ではない。FLinq は同じアルゴリズムをインライン展開できる以上、
        // 理論下限は System.Linq と同速であり、負けているなら展開側の欠陥として原因を潰すべきである。
        // 実際、同じ OrderBy(keySelector) を Array/List タブは BuildOrderLoop の ArrayPool 経路で
        // 160B・対 ZLinq 0.87 で通しており、委譲していた Range だけが 49,469B・1.04 で負けていた。
        // 以降は base.GetConsumeExecutionStatement 経由で BuildOrderLoop の ArrayPool 経路へ落とす。
        return null;
    }
}
