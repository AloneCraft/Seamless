namespace Seamless.FLinq.Generator;
/*
Fragment は「LINQ 呼び出しをどの C# コードへ置き換えるか」を表す最小単位。

入力例:
  source.Where(x => x > 0).Select(x => x * 2).ToArray()

生成の読み方:
  - 実際の出力では Struct{番号}, __e{番号}, __it{番号} のような番号付き名になる。
  - このフォルダ内のコメントでは、理解しやすいように sourceEnumerator / item / result などの擬似名で書く。
  - Enumerable 系 Fragment は GetEnumeratorStruct + GetTryGetNextBody を合成して struct enumerator を出す。
  - Scalar 系 Fragment は Compute(...) または単一要素 enumerator を出し、終端結果だけを返す。

代表的な生成骨格:
  private struct GeneratedEnumerator<TSourceEnumerator> : IValueEnumerator<T> {
      private TSourceEnumerator sourceEnumerator;
      public bool TryGetNext(out T current) {
          // 各 Fragment の GetTryGetNextBody がここへ入る。
      }
  }

設計意図:
  interceptor 側に演算子ごとのコード生成を散らさず、Fragment が状態機械・補助 struct・
  lambda の展開方法を所有する。FuncFragment は inline 可能なら式/代入へ展開し、匿名型など
  型名を書けない場合だけ delegate フィールド保持に切り替える。
*/
internal abstract class Fragment(
    int 番号
) {
    private protected const string AggressiveInliningAttribute = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";
    private bool renderingInstanceMember;

    // 生成コード内で enumerator 変数名として使う。
    private protected string e => $"__e{番号}";
    // ループ本体の先頭ラベル名。
    private protected string Head => $"Head{番号}";
    // MoveNext 判定へ戻る開始ラベル名。
    private protected string Start => $"Start{番号}";
    // 条件不成立時に現在要素を捨てるラベル名。
    private protected string Skip => $"Skip{番号}";
    // スカラー演算の早期終了先ラベル名。
    private protected string End => $"End{番号}";
    // 集約結果や中間 List の変数名。
    protected string result => $"__r{番号}";
    // 補助 struct 名。
    private protected string Struct => $"Struct{番号}";
    // Lookup/Dictionary 系演算の一時変数名。
    private protected string lookup => $"__lk{番号}";
    // 現在要素のローカル名。
    private protected string item => $"__it{番号}";
    // 多重評価を避けるための一時変数名。
    private protected string temp => $"__tmp{番号}";
    // index オーバーロード用の添字変数名。
    private protected string a => $"__a{番号}";
    // GroupBy/Join 系のキー変数名。
    private protected string key => $"__k{番号}";
    // GroupBy 系のグループ変数名。
    private protected string group => $"__g{番号}";
    //private protected string seed=> $"seed{番号}";
    // Aggregate などで初回要素を読んだかどうかを表すフラグ名。
    private protected string hasValue => $"__hv{番号}";
    // OrderBy 系の並び替え済みバッファ名。
    private protected string ordered => $"__ord{番号}";
    // Enumerator.Current を保持するプロパティ名。auto-property に統一したため常に "Current"。
    private protected string currentValue => "Current";
    // 複数段階の列挙状態を管理する state 名。
    private protected string state => $"__st{番号}";
    // スカラー結果を 1 回だけ返したかどうかのフラグ名。
    private protected string yielded => $"__yd{番号}";
    // グループ列挙内の現在位置。
    private protected string groupIndex => $"__gi{番号}";
    // Join/Zip 系で外側要素を保持する変数名。
    private protected string outerValue => $"__ov{番号}";
    // 件数取得用の共通ローカル名。
    private protected string count => $"__cnt{番号}";
    // Dictionary/HashSet の既存値用ローカル名。
    private protected string existing => $"__ex{番号}";
    // 内部列挙用の共通ローカル名。
    private protected string innerEnumerator => $"__ie{番号}";
    private FragmentRenderContext renderContext=FragmentRenderContext.Empty;
    // struct closure: 含有クラスの完全修飾型名。null なら @this 不使用。
    public string? ContainingTypeName => this.renderContext.ContainingTypeName;
    // 含有型がジェネリック（例: Base<T>）のとき、その型パラメータ名。
    public string[] OuterTypeParameters => this.renderContext.OuterTypeParameters.ToArray();
    public bool CounterInstrumentationEnabled => this.renderContext.CounterInstrumentationEnabled;
    internal FragmentRenderContext RenderContext => this.renderContext;

    // Enumerator struct 内ではメソッドローカルのキャプチャ変数を直接参照できない。
    // delegate は渡さず、必要なキャプチャ変数だけ primary constructor/static helper に渡して、
    // ラムダ本体は FuncAssign/FuncInvoke で式として展開する。
    private protected bool NeedsStructClosure(FuncFragment func) => func.CapturedLocals.Length>0;

    private protected string ClosurePrimaryCtorSuffix(params FuncFragment[] funcs) =>
        ClosureRenderSupport.PrimaryConstructorSuffix(funcs);

    private protected string ClosureMethodParameterSuffix(params FuncFragment[] funcs) =>
        ClosureRenderSupport.MethodParameterSuffix(funcs);

    private protected string ClosureArgsSuffix(params FuncFragment[] funcs) =>
        ClosureRenderSupport.ArgumentsSuffix(funcs);

    private protected string ClosureFields(params FuncFragment[] funcs) =>
        ClosureRenderSupport.Fields(funcs);

    private protected string ClosureArgsSuffixForCtorBody(params FuncFragment[] funcs) =>
        ClosureRenderSupport.ArgumentsSuffixForConstructorBody(funcs);

    private protected string HoistedLocalDeclarations(params FuncFragment[] funcs) =>
        ClosureRenderSupport.HoistedLocalDeclarations(funcs);

    private protected string StaticDelegateLocalDeclarations(params FuncFragment[] funcs) =>
        ClosureRenderSupport.StaticDelegateLocalDeclarations(funcs);

    protected virtual IEnumerable<Fragment> GetChildFragments() => Array.Empty<Fragment>();

    // ソース式に .Anchor を書いた root か。.Anchor は生成コードでは消える(恒等)ので、root を作るときに構文から記録する。
    internal bool SourceContainsAnchor { get; set; }

    // この断片の木(子の断片とラムダ)のどこかに .Anchor があるか。評価の回数を変える変換をしてよいかの判定に使う。
    internal bool TreeContainsAnchor() =>
        this.SourceContainsAnchor
        ||this.GetChildFunctions().Any(static function => function.ContainsAnchor)
        ||this.GetChildFragments().Any(static child => child.TreeContainsAnchor());
    protected virtual IEnumerable<FuncFragment> GetChildFunctions() => Array.Empty<FuncFragment>();
    protected static IEnumerable<Fragment> EnumerateChildFragments<T>(IEnumerable<T> children) where T:Fragment {
        foreach(var child in children) {
            yield return child;
        }
    }

    // ContainingTypeName と外側型パラメータは明示的な子Visitor経由で伝播する。
    // リフレクションで Fragment 型フィールドを走査するため、個別の override は不要。
    // OuterTypeParameters だけを断片ツリー全体へ伝播する。
    // StructMethod（例: ToArray の InlineArray バッファ struct）はフラグメント構築時に文字列化されるため、
    // PropagateContainingTypeName（@this 用、構築後に呼ばれる）より前にこちらで外側型パラメータを確定させる必要がある。
    // Applies one immutable context snapshot to this fragment tree.
    // The tree is owned by one GenerationState, so no context is shared between targets.
    internal virtual void ApplyRenderContext(FragmentRenderContext context) {
        this.renderContext=context;
        foreach(var child in this.GetChildFragments()) {
            child.ApplyRenderContext(context);
        }
        foreach(var function in this.GetChildFunctions()) {
            if(function is InlinedScalarFunc inlinedScalarFunc) {
                inlinedScalarFunc.ScalarFragment.ApplyRenderContext(context);
            }
        }
    }

    private protected string CounterCall(string methodName) =>
        this.CounterInstrumentationEnabled
            ? $"global::Seamless.Linq.LinqCounterScope.Counter.{methodName}();"
            : string.Empty;

    private protected string CountLogicalDelegateExpression(string expression) =>
        this.CounterInstrumentationEnabled
            ? $"global::Seamless.Linq.LinqCounterScope.Counter.RecordLogicalDelegate({expression})"
            : expression;

    private protected string CountPhysicalDelegateExpression(string expression) =>
        this.CounterInstrumentationEnabled
            ? $"global::Seamless.Linq.LinqCounterScope.Counter.RecordPhysicalDelegate({expression})"
            : expression;

    // UnsafeAccessor コードを断片ツリー全体から収集する。
    public IEnumerable<string> CollectAllUnsafeAccessorCodes() {
        foreach(var function in this.GetChildFunctions()) {
            foreach(var code in function.BodyUnsafeAccessorCodes) {
                yield return code;
            }
            if(function is InlinedScalarFunc inlinedScalarFunc) {
                foreach(var code in inlinedScalarFunc.ScalarFragment.CollectAllUnsafeAccessorCodes()) {
                    yield return code;
                }
            }
        }
        foreach(var child in this.GetChildFragments()) {
            foreach(var code in child.CollectAllUnsafeAccessorCodes()) {
                yield return code;
            }
        }
    }

    // 補助 struct 群の宣言本体。不要なら空文字列を返す。
    public abstract string StructMethod { get; }
    // ValueEnumerable/IValueEnumerator 用の struct 定義本体。
    public abstract string GetEnumeratorStruct { get; }
    // TryGetNext の本体コード片。
    public abstract string GetTryGetNextBody { get; }
    // 生成した enumerator struct の型名。PrimarySource が設定されると自動でジェネリック型名を返す。
    // 含有型がジェネリック（OuterTypeParameters 有り）なら、その型引数を先頭に付ける（例: Struct1<T> / Struct2<T, TEnum>）。
    public virtual string EnumeratorTypeName {
        get {
            var typeArguments = new List<string>(this.OuterTypeParameters);
            if(this.PrimarySource is { } src) {
                typeArguments.Add(src.EnumeratorTypeName);
            }
            return typeArguments.Count==0 ? this.Struct : $"{this.Struct}<{string.Join(", ",typeArguments)}>";
        }
    }
    // OuterTypeParameters を先頭に付けた struct 型パラメータ列（例: "<T, TEnum1, TEnum2>"）。
    // 複数上流源を持つ Fragment（Concat/Zip/Set 系）の StructTypeParameters override から使う。
    private protected string StructTypeParametersWithOuter(params string[] inner) {
        var all = this.OuterTypeParameters.Length==0 ? inner : this.OuterTypeParameters.Concat(inner).ToArray();
        return all.Length==0 ? string.Empty : "<"+string.Join(", ",all)+">";
    }
    // OuterTypeParameters を先頭に付けた enumerator 型名（例: "Struct0<T, {src}, {second}>"）。
    // 複数上流源を持つ Fragment の EnumeratorTypeName override から使う。
    private protected string EnumeratorTypeNameWithOuter(params string[] innerTypeArguments) {
        var all = this.OuterTypeParameters.Length==0 ? innerTypeArguments : this.OuterTypeParameters.Concat(innerTypeArguments).ToArray();
        return all.Length==0 ? this.Struct : $"{this.Struct}<{string.Join(", ",all)}>";
    }
    // Enumerable を直接返す interceptor 用の new Enumerator(...) 式。
    public virtual string GetEnumeratorExpression => throw new NotSupportedException($"{this.GetType().Name} does not support Enumerator mode expression generation.");

    private protected static string RenderEnumeratorExpression(string enumeratorTypeName,params string[] arguments) =>
        new EnumeratorExpressionRenderModel(enumeratorTypeName,arguments).Render();

    private protected static string RenderEnumeratorExpressionSeparated(string enumeratorTypeName,string separator,params string[] arguments) =>
        new EnumeratorExpressionRenderModel(enumeratorTypeName,arguments,separator).Render();

    private protected string RenderEnumeratorExpressionWithClosure(string enumeratorTypeName,FuncFragment[] funcs,params string[] arguments) =>
        new EnumeratorExpressionRenderModel(
            enumeratorTypeName,
            arguments.Concat(ClosureRenderSupport.ArgumentExpressions(funcs)).ToArray()).Render();
    // primary constructor を使う enumerator struct の引数一覧。空なら通常 struct 宣言。
    public virtual string GetEnumeratorPrimaryConstructorParameters => string.Empty;
    // enumerator struct が持つ追加フィールド定義。
    public virtual string GetEnumeratorFields => string.Empty;
    // enumerator struct のコンストラクタ定義。
    public virtual string GetEnumeratorConstructor => string.Empty;
    // enumerator struct の TryGetNonEnumeratedCount 本体。既定は非対応（false）。
    // bool 戻り値を呼び出し側が必ず確認する実行時 fast path のため、断片が安全に override 可能。
    // 本体が既定のままの断片は IValueEnumerator<T> の default interface method に委ね、
    // DefineEnumeratorStructCore がメンバ自体を出力しない（生成コード量の削減）。
    // 条件付き override が base.GetTryGetSpanBody へ落ちた場合も既定として扱えるよう、
    // 判定は「フラグ」ではなく「描画済み本体と既定文字列の一致」で行う。
    private protected const string DefaultTryGetNonEnumeratedCountBody = "count = 0;\nreturn false;";
    private protected const string DefaultTryGetSpanBody = "span = default;\nreturn false;";
    private protected const string DefaultTryCopyToBody = "return false;";
    protected virtual string GetTryGetNonEnumeratedCountBody => DefaultTryGetNonEnumeratedCountBody;
    protected virtual string GetTryGetSpanBody => DefaultTryGetSpanBody;
    protected virtual string GetTryCopyToBody => DefaultTryCopyToBody;
    // Enumerable 断片が List<T> へ結果を積む共通形。
    private protected static string var_List(string name,string ElementType) =>
        $"var {name}=new global::System.Collections.Generic.List<{ElementType}>();";
    private protected string FuncSupport(FuncFragment func,string methodName) =>
        func.SupportingMemberSources;

    // selector/predicate の呼び出しを、可能ならその場の式へ展開する。
    // キャプチャ変数付きラムダもインライン展開可能（キャプチャ変数はインターセプト先メソッドのスコープに存在する）。
    private protected string FuncInvoke(FuncFragment func,string methodName,params string[] arguments) {
        if(func.CanInvokeAsDelegate) {
            return this.CountPhysicalDelegateExpression($"this.{func.DelegateFieldName}.Invoke({string.Join(", ",arguments)})");
        }
        if(func.CanInlineAssign) {
            return this.CountLogicalDelegateExpression(this.RenderInlineExpression(func,arguments));
        }
        throw LambdaInlineUnsupported(func,methodName);
    }

    // 代入文が必要なときの共通ヘルパー。
    private protected string FuncAssign(FuncFragment func,string methodName,string destination,params string[] arguments) {
        if(func.RequiresStatementInlining) {
            return $"{this.CounterCall("IncrementLogicalDelegate")}\n{this.RenderInlineAssign(func,destination,arguments)}";
        }
        if(func.CanInvokeAsDelegate) {
            return $"{destination}={this.CountPhysicalDelegateExpression($"this.{func.DelegateFieldName}.Invoke({string.Join(", ",arguments)})")};";
        }
        if(func.CanInlineAssign) {
            return $"{this.CounterCall("IncrementLogicalDelegate")}\n{this.RenderInlineAssign(func,destination,arguments)}";
        }
        if(func is LambdaFunc) {
            throw LambdaInlineUnsupported(func,methodName);
        }
        return $"{destination}={this.FuncInvoke(func,methodName,arguments)};";
    }
    private protected string FuncAssignIndented(string indent,FuncFragment func,string methodName,string destination,params string[] arguments) =>
        IndentGeneratedStatement(this.FuncAssign(func,methodName,destination,arguments),indent);

    private string RenderInlineAssign(FuncFragment func,string destination,params string[] arguments) =>
        this.renderingInstanceMember
            ? func.RenderInlineAssignWithHoistedReceiver("this.",destination,番号,arguments)
            : func.RenderInlineAssign(destination,番号,arguments);

    private string RenderInlineExpression(FuncFragment func,params string[] arguments) =>
        this.renderingInstanceMember
            ? func.RenderInlineExpressionWithHoistedReceiver("this.",arguments)
            : func.RenderInlineExpression(arguments);

    private string RenderInstanceMember(Func<string> render) {
        var previous = this.renderingInstanceMember;
        this.renderingInstanceMember=true;
        try {
            return render();
        } finally {
            this.renderingInstanceMember=previous;
        }
    }

    internal string FuncAssignForAggregateInline(FuncFragment func,string methodName,string destination,string? delegateLocalName,params string[] arguments) {
        if(func.CanInvokeAsDelegate&&delegateLocalName is { Length:>0 }) {
            return $"{destination}={this.CountPhysicalDelegateExpression($"{delegateLocalName}.Invoke({string.Join(", ",arguments)})")};";
        }

        return this.FuncAssign(func,methodName,destination,arguments);
    }

    // TryGetNext 内で destination フィールドと out current の両方を設定する複合代入を生成する。
    // 単純ラムダなら current = destination = expr; の一行に。
    // RequiresStatementInlining またはデリゲート呼び出しの場合は FuncAssign の後に current = destination; を付加する。
    private protected string FuncAssignCurrent(FuncFragment func,string methodName,string destination,params string[] arguments) {
        if(func.RequiresStatementInlining||(!func.CanInlineAssign&&!func.CanInvokeAsDelegate)) {
            return $"{this.FuncAssign(func,methodName,destination,arguments)}\ncurrent = {destination};";
        }
        var expr = this.FuncInvoke(func,methodName,arguments);
        return $"current = {destination} = {expr};";
    }
    private protected string FuncAssignCurrentIndented(string indent,FuncFragment func,string methodName,string destination,params string[] arguments) =>
        IndentGeneratedStatement(this.FuncAssignCurrent(func,methodName,destination,arguments),indent);

    private protected static string WrapConditionExpression(string conditionExpr) =>
        $"({TrimSingleOuterParentheses(conditionExpr)})";

    private static string TrimSingleOuterParentheses(string expression) {
        var trimmed = expression.Trim();
        if(trimmed.Length<2||trimmed[0]!='('||trimmed[trimmed.Length-1]!=')') {
            return trimmed;
        }

        var depth = 0;
        for(var i = 0;i<trimmed.Length;i++) {
            switch(trimmed[i]) {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if(depth==0&&i<trimmed.Length-1) {
                        return trimmed;
                    }
                    break;
            }
        }

        return depth==0 ? trimmed.Substring(1,trimmed.Length-2).Trim() : trimmed;
    }

    // 述語評価専用ヘルパー。if文の条件式として使う場合に特化。
    // 単純ラムダなら直接式として展開（一時変数なし）。
    // RequiresStatementInlining=true（InlinedScalarFuncなど）なら一時変数経由にフォールバック。
    private protected (string preCode,string conditionExpr) FuncInvokeCondition(FuncFragment func,string methodName,string tempVar,params string[] arguments) {
        // @this をキャプチャしたstruct closureでは、入れ子ラムダから@thisフィールド経由で
        // インスタンスメンバーへアクセスするとCS1673（構造体のthisをラムダがキャプチャできない）になる。
        // 同名のローカル変数でシャドウしてコピーを参照させることで回避する。
        var thisShadow = func.CapturedLocals.Any(c => c.Name=="@this") ? "var @this = this.@this;" : string.Empty;
        if(func.RequiresStatementInlining) {
            // InlinedScalarFunc など複数文が必要な場合は一時変数経由
            var declaration = $"{func.ReturnTypeName} {tempVar} = default!;";
            var assignStatement = this.RenderInlineAssign(func,tempVar,arguments);
            var counterCall = this.CounterCall("IncrementLogicalDelegate");
            var pre = thisShadow.Length>0 ? $"{thisShadow}\n{counterCall}\n{declaration}\n{assignStatement}" : $"{counterCall}\n{declaration}\n{assignStatement}";
            return (pre,tempVar);
        }
        if(func.CanInvokeAsDelegate) {
            return (string.Empty,this.CountPhysicalDelegateExpression($"this.{func.DelegateFieldName}.Invoke({string.Join(", ",arguments)})"));
        }
        if(func.CanInlineAssign) {
            // 単純ラムダなら直接式として展開
            return (thisShadow,this.CountLogicalDelegateExpression(this.RenderInlineExpression(func,arguments)));
        }
        throw LambdaInlineUnsupported(func,methodName);
    }

    // static メソッド本体内（this. が使えない文脈）での述語評価。
    private protected (string preCode,string conditionExpr) FuncInvokeConditionStatic(FuncFragment func,string methodName,string tempVar,params string[] arguments) {
        if(func.RequiresStatementInlining) {
            var declaration = $"{func.ReturnTypeName} {tempVar} = default!;";
            var assignStatement = func.RenderInlineAssign(tempVar,番号,arguments);
            var counterCall = this.CounterCall("IncrementLogicalDelegate");
            return ($"{counterCall}\n{declaration}\n{assignStatement}",tempVar);
        }
        if(func.CanInvokeAsDelegate) {
            return (string.Empty,this.CountPhysicalDelegateExpression($"{func.DelegateFieldName}.Invoke({string.Join(", ",arguments)})"));
        }
        if(func.CanInlineAssign) {
            return (string.Empty,this.CountLogicalDelegateExpression(func.RenderInlineExpression(arguments)));
        }
        throw LambdaInlineUnsupported(func,methodName);
    }

    // auto-property のイニシャライザ式。All のように true で初期化したい場合は override する。
    protected virtual string GetCurrentPropertyDefaultExpression => "default!";
    // 各断片が持つ Current/TryGetNext/Dispose を IValueEnumerator struct にまとめる。
    // Current プロパティの宣言文。BufferedScalar はオートプロパティを GetEnumeratorFields で宣言するためこのメソッドを空文字列に override する。
    protected virtual string GetCurrentPropertyDeclaration(string currentType) =>
        $"public {currentType} Current {{ get; private set; }} = {this.GetCurrentPropertyDefaultExpression};";

    private protected string DefineEnumeratorStructCore(string currentType,string disposeBody) {
        var tryGetNextBody=IndentLines(this.RenderInstanceMember(() => this.GetTryGetNextBody),"        ");
        var dispose=IndentLines(disposeBody,"        ");
        var currentPropDecl=this.GetCurrentPropertyDeclaration(currentType);
        var nonEnumeratedCount=this.EnumeratorProbeMember(
            () => this.GetTryGetNonEnumeratedCountBody,
            DefaultTryGetNonEnumeratedCountBody,
            "public bool TryGetNonEnumeratedCount(out int count)",
            "public bool TryGetNonEnumeratedCount(out int count) { count = 0; return false; }");
        var getSpan=this.EnumeratorProbeMember(
            () => this.GetTryGetSpanBody,
            DefaultTryGetSpanBody,
            $"public bool TryGetSpan(out global::System.ReadOnlySpan<{currentType}> span)",
            $"public bool TryGetSpan(out global::System.ReadOnlySpan<{currentType}> span) {{ span = default; return false; }}");
        var copyTo=this.EnumeratorProbeMember(
            () => this.GetTryCopyToBody,
            DefaultTryCopyToBody,
            $"public bool TryCopyTo(scoped global::System.Span<{currentType}> destination,global::System.Index __offset)",
            $"public bool TryCopyTo(scoped global::System.Span<{currentType}> destination,global::System.Index __offset) => false;");

        return EnumeratorRenderPlan.Create(
            this.GetEnumeratorStructDeclaration($"global::Seamless.Linq.IValueEnumerator<{currentType}>"),
            this.Struct,
            this.GetEnumeratorPrimaryConstructorParameters,
            this.GetEnumeratorConstructor,
            this.GetEnumeratorFields,
            currentPropDecl,
            nonEnumeratedCount,
            getSpan,
            copyTo,
            tryGetNextBody,
            dispose,
            currentType).Render();
    }

    // TryGetNonEnumeratedCount/TryGetSpan/TryCopyTo の出力。
    // 本体が既定（常に false）のままなら 1 行の圧縮形で出す。1 struct あたり 11 行前後を削減できる。
    //
    // メンバ自体を省いて IValueEnumerator<T> の default interface method へ委ねる案は採らない。
    // 理由は 2 つあり、どちらも実測・実験で確認済み:
    //   1. DIM は「実装していないメンバ」なので constrained. callvirt となり値型が box される。
    //      RyuJIT は DIM を devirtualize せず（CORINFO_DEVIRTUALIZATION_FAILED_DIM）、
    //      .NET 10 の escape analysis でも消えない。実測で 24 バイト/呼び出しの割当が発生した。
    //   2. DIM は具象型のレシーバーからは呼べない（インターフェース型か制約付き型引数のみ）。
    //      生成コードは enumerator を具象 struct のローカルとして保持する箇所が多く、CS1061 になる。
    // 既定本体でも AggressiveInlining を付けないのは、常に false を返すだけの本体は
    // 属性が無くても JIT が確実にインライン化するため。
    private string EnumeratorProbeMember(Func<string> render,string defaultBody,string signature,string compactMember) {
        var rendered=this.RenderInstanceMember(render);
        return EnumeratorProbeRenderModel.Render(rendered,defaultBody,signature,compactMember,AggressiveInliningAttribute);
    }
    private protected static string IndentLines(string text,string indent) =>
        GeneratedTextFormatting.IndentLines(text,indent);
    // 単一上流源を持つ断片はこれをオーバーライドする。
    // 設定すると StructTypeParameters/StructTypeConstraints/EnumeratorTypeName が自動導出される。
    protected virtual Enumerable? PrimarySource => null;

    // 生成 struct に付ける型パラメータ（"<TEnum>" 等）。PrimarySource が設定されると "<TEnum>" になる。
    // 含有型がジェネリック（OuterTypeParameters 有り）なら、その型パラメータを先頭に付ける（例: "<T>" / "<T, TEnum>"）。
    protected virtual string StructTypeParameters {
        get {
            var parameters = new List<string>(this.OuterTypeParameters);
            if(this.PrimarySource is not null) {
                parameters.Add("TEnum");
            }
            return parameters.Count==0 ? string.Empty : "<"+string.Join(", ",parameters)+">";
        }
    }
    // 生成 struct に付ける where 制約。PrimarySource が設定されると struct 制約付きの where 句になる。
    // 生成 struct は IValueEnumerator<T> だけを実装する（IEnumerator<T> は実装しない）。
    // foreach / IEnumerator が要る場面はランタイムの ValueEnumerator<TEnumerator,T> で包む。
    protected virtual string StructTypeConstraints =>
        this.PrimarySource is { } src
            ? $"where TEnum : struct, global::Seamless.Linq.IValueEnumerator<{src.ElementType}>"
            : string.Empty;

    private protected string GetEnumeratorStructDeclaration(string interfaceType) =>
        EnumeratorDefinitionRenderModel.RenderDeclaration(this.Struct,this.StructTypeParameters,interfaceType,this.StructTypeConstraints);

    private protected string GetGenericEnumeratorStructDeclaration(string typeParameters,string interfaceType) =>
        EnumeratorDefinitionRenderModel.RenderDeclaration(this.Struct,typeParameters,interfaceType,this.StructTypeConstraints);
    // struct の TryGetNext 内で使う。static ラムダまたはキャプチャ変数付きラムダならインライン展開する。
    private protected string FuncInvokeField(FuncFragment func,string methodName,params string[] arguments) {
        if(func.CanInlineAssign) {
            return this.CountLogicalDelegateExpression(func.RenderInlineExpressionWithHoistedReceiver("this.",arguments));
        }
        throw LambdaInlineUnsupported(func,methodName);
    }

    // static メソッド本体内（this. が使えない文脈）でのデリゲート呼び出し生成。
    private protected string FuncInvokeStatic(FuncFragment func,string methodName,params string[] arguments) {
        if(func.CanInvokeAsDelegate) {
            return this.CountPhysicalDelegateExpression($"{func.DelegateFieldName}.Invoke({string.Join(", ",arguments)})");
        }
        if(func.CanInlineAssign) {
            return this.CountLogicalDelegateExpression(func.RenderInlineExpression(arguments));
        }
        throw LambdaInlineUnsupported(func,methodName);
    }

    // static メソッド本体内（this. が使えない文脈）での代入文生成。
    private protected string FuncAssignStatic(FuncFragment func,string methodName,string destination,params string[] arguments) {
        if(func.RequiresStatementInlining) {
            return $"{this.CounterCall("IncrementLogicalDelegate")}\n{func.RenderInlineAssign(destination,番号,arguments)}";
        }
        if(func.CanInvokeAsDelegate) {
            return $"{destination}={this.CountPhysicalDelegateExpression($"{func.DelegateFieldName}.Invoke({string.Join(", ",arguments)})")};";
        }
        if(func.CanInlineAssign) {
            return $"{this.CounterCall("IncrementLogicalDelegate")}\n{func.RenderInlineAssign(destination,番号,arguments)}";
        }
        throw LambdaInlineUnsupported(func,methodName);
    }
    private protected string FuncAssignStaticIndented(string indent,FuncFragment func,string methodName,string destination,params string[] arguments) =>
        IndentGeneratedStatement(this.FuncAssignStatic(func,methodName,destination,arguments),indent);

    // 根の列挙子を最後まで回すループの頭(while 文の開き括弧と、要素を受ける局所変数の宣言まで)。
    // IEnumerable の根は列挙子を局所変数に取って MoveNext/Current を直接呼ぶ。根の構造体の TryGetNext は
    // 遅延の GetEnumerator・Current と添字の保存を要素ごとに行うので、int の要素で理想版の約1.4倍かかっていた(Work/exp/ienumroot)。
    // 列挙子は根の構造体が持ったままなので、破棄は根の Dispose が受け持つ。
    internal static (string Preamble,string Condition,string Declaration) DrainParts(Enumerable root,string enumeratorName,string itemName) =>
        root.ProvidesRemainingEnumerator
            ? ($"var __rest_{enumeratorName}={enumeratorName}.GetRemainingEnumerator();\n",$"__rest_{enumeratorName}.MoveNext()",$"\nvar {itemName}=__rest_{enumeratorName}.Current;")
            : (string.Empty,$"{enumeratorName}.TryGetNext(out var {itemName})",string.Empty);

    internal static string DrainLoopHead(Enumerable root,string enumeratorName,string itemName) {
        var (preamble,condition,declaration) = DrainParts(root,enumeratorName,itemName);
        return $"{preamble}while({condition}){{{declaration}";
    }

    private protected static string IndentGeneratedStatement(string text,string indent) =>
        IndentLines(text.Trim(),indent);

    // IEnumerable<T> / T[] の文字列型名から要素型名を取り出す。
    private protected static string SequenceElementTypeName(string sequenceTypeName) {
        if(sequenceTypeName.EndsWith("[]",StringComparison.Ordinal)) {
            return sequenceTypeName.Substring(0,sequenceTypeName.Length-2);
        }

        const string enumerablePrefix = "global::System.Collections.Generic.IEnumerable<";
        if(sequenceTypeName.StartsWith(enumerablePrefix,StringComparison.Ordinal)
            &&sequenceTypeName.EndsWith(">",StringComparison.Ordinal)) {
            return sequenceTypeName.Substring(enumerablePrefix.Length,sequenceTypeName.Length-enumerablePrefix.Length-1);
        }

        throw new InvalidOperationException($"Could not determine sequence element type from '{sequenceTypeName}'.");
    }

    // これらのメソッドは struct closure が不要になったため削除された。
    // 代わりに FuncInvokeField/FuncAssign を直接使用する。

    // FuncFragment から index 番目の引数型名を取り出す。
    private protected static string FuncParameterTypeName(FuncFragment func,int index) {
        if(func.ParameterTypeNames.Length>index&&!string.IsNullOrEmpty(func.ParameterTypeNames[index])) {
            return func.ParameterTypeNames[index];
        }
        throw new InvalidOperationException($"FuncFragment parameter type {index} is not available.");
    }

    // FuncFragment の戻り値型名を取り出す。
    private protected static string FuncReturnTypeName(FuncFragment func) {
        return func.ReturnTypeName;
    }

    // 型推論用ファクトリメソッドの名前。
    // "_Factory" サフィックスは 補助メンバーをメソッド専用クラスへ退避() の Struct{番号} リネーム正規表現に対応している。
    // 例: Struct0_Factory → {prefix}Struct0_Factory （メソッドごとにユニークな名前に変換される）。
    private protected string FactoryMethodName => $"{this.Struct}_Factory";

    // ファクトリメソッド本体のコンストラクタ呼び出し引数サフィックス。
    // ClosureArgsSuffix と異なり、DelegateConstructorArg（ラムダ式）ではなく DelegateFieldName（フィールド名）を使う。
    private protected string ClosureArgsSuffixForFactory(params FuncFragment[] funcs) =>
        ClosureRenderSupport.ArgumentsSuffixForFactory(funcs);

    private protected static InvalidOperationException LambdaInlineUnsupported(FuncFragment func,string methodName) =>
        new($"ラムダ式をインライン展開できませんでした。'{methodName}' をデリゲート Invoke 呼び出しへ逃がすことは許可されていません。Body: '{func.InlineBody}'");

    internal static string JoinStaticMethods(string left,string right) =>
        string.Join("\n",new[] { left,right }.Where(static s => !string.IsNullOrWhiteSpace(s)));
}

internal delegate bool BuildEnumerableFragmentDelegate(
    SemanticModel semanticModel,
    ExpressionSyntax source,
    INamedTypeSymbol containingType,
    ref int uniqueNumber,
    out Enumerable fragment,
    out string staticMethods,
    List<string>? collector);

internal delegate bool BuildFuncFragmentDelegate(
    ExpressionSyntax expression,
    ITypeSymbol delegateType,
    ref int uniqueNumber,
    out FuncFragment fragment,
    SemanticModel? semanticModel,
    INamedTypeSymbol? containingType);

internal delegate Fragment? CreateFragmentDelegate(
    string methodName,
    string variantName,
    int number,
    object?[] extraArgs);

internal delegate bool TryGetLambdaBodyDelegate(
    LambdaExpressionSyntax lambda,
    out ExpressionSyntax body);

internal delegate List<string> GetLambdaParametersDelegate(
    LambdaExpressionSyntax lambda);

internal delegate bool BuildInvocationFragmentDelegate(
    SemanticModel semanticModel,
    InvocationExpressionSyntax invocation,
    INamedTypeSymbol containingType,
    ref int uniqueNumber,
    out Fragment fragment,
    out string staticMethods,
    List<string>? unsafeAccessorCollector);
