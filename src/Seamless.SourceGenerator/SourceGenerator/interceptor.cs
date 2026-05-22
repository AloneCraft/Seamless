/*
このファイルをAIに編集してもらうときの指示
	コード生成方法
		Fragmentsフォルダの下のクラスが各メソッドをためのコード片になる
		linqメソッドでIEnumerable<T>を返すメソッドはstruct Enumeratorを実装してコンストラクタ、MoveNext,Resetをコード片から作る
	インターセプターするべきもの
		linqメソッドを使っているメソッドX全体をインターセプトする。インターセプト先メソッドYを生成する。
		Xを呼んでいる部分をYを呼ぶようにする。
		仮想関数呼び出しならそのままにしておく
	インターセプト先メソッドの変更点
		メソッドX内部のlinqメソッドチェーンをループも含めてインライン展開する
		キャプチャ変数もメソッドX内部全てをインライン展開することでラムダ式を非デリゲート化する。
		ラムダ式ではないデリゲートはデリゲート呼び出しのまま
		インライン展開方法はFragments以下のクラスを使いメソッドに対応するコード片から組み立てる
		メソッドチェーンの末尾メソッドで処理するメソッドを生成する。
		末尾以外のメソッドチェーンはキャリアメソッドにより末尾にデータを送る。生成するコードは構文エラーや足りないものはあるが概ね以下のような意図である
			outer.Join(inner,outerKeySelector,innerKeySelector,resultSelector).Where(predicate)ならば
			struct Enumerator<TOuter,TInner,TKey,TResult,TOuterEnumerator,TInnerEnumerator>where struct,TOuterEnumerator<TOuter>where struct,TInnerEnumerator<TInner>{
				TOuterEnumerator OuterEnumerator:
				TInnerEnumerator InnerEnumerator
				public Enumerator(TOuterEnumerator OuterEnumerator,TInnerEnumerator InnerEnumerator){
					this.OuterEnumerator=OuterEnumerator;
					this.InnerEnumerator=InnerEnumerator;
					this.State=0;
				}
				public int State;
				public TResult Current{get;private set;}
				private
				public bool MoveNext(Join<TOuter,TInner,TKey,TResult>source,Func<TResult,bool>predicate){
					switch(state){
					case 0:{
						goto Start;
						Head:
						d.Add(innerKeySelector(InnerEnumerator.Current));
						Start:
						if(InnerEnumerator.MoveNext())goto Head;
						goto Start;
							goto case 1;
					}
					case 1:{
						if(OuterEnumerator.MoveNext()){
							if(d.TryGetValue(outerKeySelector(OuterEnumerator.Current),out var r)){
								if(!predicate(r))goto Skip2;
								this.Current=resultSelector(OuterEnumerator.Current,InnerEnumerator.Current);
								return true;
							}
						}
						return false;
					}
						return false;
				}
			}
			Enumerable<TResult>WhereInline(Join<TOuter,TInner,TKey,TResult>source,Func<TResult,bool>predicate){
				return new Enumerable<TResult>(new Enumerator<TOuter,TInner,TKey,TResult>(source.outer.GetEnumerator(),source.inner.GetEnumerator()));
			}
	インターセプター先コード生成でやるべきこと
		privateメンバーにアクセスする場合があるのでUnsafeAccessorを使ってアクセスする
		全てはFragment継承クラスで定義してあるものに従い、interceptor.csで例外的な処理は入れない。
*/
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using Seamless.SourceGenerator.Linq.Fragments;
using Enumerable = Seamless.SourceGenerator.Linq.Fragments.Enumerable;

namespace Seamless.SourceGenerator;

[Generator]
public sealed partial class SeamlessIncrementalGenerator : IIncrementalGenerator {
    private sealed class フラグメント候補 {
        public required Type FragmentType { get; init; }
        public required ConstructorInfo Constructor { get; init; }
        public required string MethodName { get; init; }
        public required string VariantName { get; init; }
        public required ParameterInfo[] Parameters { get; init; }
    }

    private static readonly SymbolDisplayFormat 完全修飾形式 = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
    // Fragments.Fragment を継承した具体型を列挙し、ローカルメソッド再生成にもそのまま使う。
    // これで Aggregate の overload 個別対応を増やすたびに辞書を手で更新しなくてよい。
    private static readonly IReadOnlyDictionary<string,ImmutableArray<フラグメント候補>> フラグメント候補一覧 = フラグメント候補一覧構築23();

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var interceptTargets = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_) => node is InvocationExpressionSyntax,
                static (syntaxContext,cancellationToken) => Linq傍受対象生成(syntaxContext,cancellationToken))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var typeTargets = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_) => node is TypeDeclarationSyntax,
                static (syntaxContext,_) => 型対象生成(syntaxContext))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var combined = interceptTargets.Collect().Combine(typeTargets.Collect());
        context.RegisterSourceOutput(combined,static (productionContext,pair) => {
            var interceptTargets = pair.Left;
            var typeTargets = pair.Right;

            if(!typeTargets.IsDefaultOrEmpty) {
                var databaseSource = データベースソース構築(typeTargets);
                if(databaseSource.Length>0) {
                    productionContext.AddSource("Seamless.SourceGenerator.Schema.g.cs",SourceText.From(空行除去(databaseSource),Encoding.UTF8));
                }
            }

            if(interceptTargets.IsDefaultOrEmpty) {
                return;
            }

            var source = インターセプターソース構築(interceptTargets.Distinct(LinqInterceptTargetComparer.Instance).ToImmutableArray());
            if(source.Length>0) {
                productionContext.AddSource("Seamless.SourceGenerator.LinqInterceptors.g.cs",SourceText.From(source,Encoding.UTF8));
            }
        });
    }

    private static Linq傍受対象型? Linq傍受対象生成(GeneratorSyntaxContext context,System.Threading.CancellationToken cancellationToken) {
        //呼び出しである必要がある
        if(context.Node is not InvocationExpressionSyntax invocation) {
            return null;
        }

        if(context.SemanticModel.GetSymbolInfo(invocation,cancellationToken).Symbol is not IMethodSymbol method) {
            return null;
        }

        return TryCreateLocalMethodInterceptionTarget(context,invocation,method,cancellationToken,out var localMethodTarget)
            ? localMethodTarget
            : null;
    }

    private static bool TryCreateLocalMethodInterceptionTarget(
        GeneratorSyntaxContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        System.Threading.CancellationToken cancellationToken,
        out Linq傍受対象型? target) {
        target=null;

        // インターセプト対象にする主条件は 2 つ:
        // 1. その場所が通常の関数呼び出しであること
        // 2. 呼び出し先メソッドの内部に LINQ 呼び出しがあること
        // それ以外の条件は、生成したインターセプターがコンパイル不能になったり、
        // 仮想呼び出しの動的ディスパッチを壊したりしないための安全制約として残す。
        if(method.MethodKind!=MethodKind.Ordinary
            || method.ContainingType is null
            || method.IsGenericMethod
            || method.ContainingType.IsGenericType
            || (!method.IsStatic && (method.IsAbstract || method.IsVirtual || method.IsOverride))
            || ContainsGeneratedTypeAccessibilityRisk(method.ContainingType)
            || method.ContainingType.IsRefLikeType
            || (!method.IsStatic && method.ContainingType.IsValueType)
            || method.DeclaringSyntaxReferences.Length==0
            || ContainsAnonymousType(method.ReturnType)
            || ContainsGeneratedTypeAccessibilityRisk(method.ReturnType)
            || method.ReturnType.IsRefLikeType
            || ContainsTypeParameter(method.ReturnType)
            || method.Parameters.Any(static parameter => parameter.RefKind!=RefKind.None)
            || method.Parameters.Any(static parameter => ContainsAnonymousType(parameter.Type))
            || method.Parameters.Any(static parameter => ContainsGeneratedTypeAccessibilityRisk(parameter.Type))
            || method.Parameters.Any(static parameter => parameter.Type.IsRefLikeType)
            || method.Parameters.Any(static parameter => ContainsTypeParameter(parameter.Type))) {
            return false;
        }

        if(invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } containingMethodSyntax) {
            return false;
        }

        var containingMethod = context.SemanticModel.GetDeclaredSymbol(containingMethodSyntax,cancellationToken);
        if(containingMethod is null) {
            return false;
        }

        if(!メソッド内部Linq呼び出し含む36(method,context.SemanticModel.Compilation)) {
            return false;
        }

        var interceptableLocation = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptableLocation(context.SemanticModel,invocation,cancellationToken);
        if(interceptableLocation is null) {
            return false;
        }

        var interceptsLocationAttributeText = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptsLocationAttributeSyntax(interceptableLocation);
        if(string.IsNullOrWhiteSpace(interceptsLocationAttributeText)) {
            return false;
        }

        var invocationLineSpan = invocation.GetLocation().GetLineSpan();
        var line = invocationLineSpan.StartLinePosition.Line+1;
        var character = invocationLineSpan.StartLinePosition.Character+1;
        var identifier = SanitizeIdentifier($"{method.Name}_{System.IO.Path.GetFileNameWithoutExtension(invocationLineSpan.Path)}_{containingMethod.Name}_{line}_{character}");
        var parameterSpecs = method.Parameters
            .Select(static parameter => new InterceptParameterSpec(parameter.Name,parameter.Type.ToDisplayString(完全修飾形式),parameter.RefKind))
            .ToArray();

        target = new Linq傍受対象型(
            identifier,
            containingMethod.Name,
            interceptsLocationAttributeText,
            method,
            method,
            invocation,
            containingMethod.ContainingType,
            context.SemanticModel,
            parameterSpecs,
            isLocalMethodInterception: true);
        return true;
    }

    private static bool メソッド内部Linq呼び出し含む36(IMethodSymbol method,Compilation compilation) {
        if(method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax) {
            return false;
        }

        var semanticModel = compilation.GetSemanticModel(methodSyntax.SyntaxTree);
        var invocationNodes = methodSyntax.Body is not null
            ? methodSyntax.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
            : methodSyntax.ExpressionBody is not null
                ? methodSyntax.ExpressionBody.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                : global::System.Linq.Enumerable.Empty<InvocationExpressionSyntax>();

        return invocationNodes
            .Select(invocation => semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol)
            .Where(static invokedMethod => invokedMethod is not null)
            .Select(static invokedMethod => invokedMethod!.ReducedFrom ?? invokedMethod)
            .Any(Linqメソッド判定);
    }

    private static bool Linqメソッド判定(IMethodSymbol method) {
        var definition = method.ReducedFrom ?? method;
        if(!definition.IsExtensionMethod
            ||definition.ContainingType.ToDisplayString()!="System.Linq.Enumerable") {
            return false;
        }

        if(definition.Parameters.Length==0
            || definition.Parameters[0].Type is not INamedTypeSymbol sourceType
            ||sourceType.TypeArguments.Length!=1
            ||!IsIEnumerableType(sourceType)) {
            return false;
        }

        if(method.TypeArguments.Any(static typeArgument => ContainsAnonymousType(typeArgument) || ContainsGeneratedTypeAccessibilityRisk(typeArgument))
            || ContainsAnonymousType(method.ReturnType)
            || ContainsGeneratedTypeAccessibilityRisk(method.ReturnType)
            || method.Parameters.Any(static parameter => ContainsAnonymousType(parameter.Type))
            || method.Parameters.Any(static parameter => ContainsGeneratedTypeAccessibilityRisk(parameter.Type))
            || ContainsTypeParameter(method.ReturnType)
            || method.Parameters.Any(static parameter => ContainsTypeParameter(parameter.Type))) {
            return false;
        }

        return true;
    }

    private static string インターセプターソース構築(ImmutableArray<Linq傍受対象型> targets) {
        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace System.Runtime.CompilerServices {");
        builder.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method,AllowMultiple=true)]");
        builder.AppendLine("    file sealed class InterceptsLocationAttribute : global::System.Attribute {");
        builder.AppendLine("        public InterceptsLocationAttribute(int version,string data) {");
        builder.AppendLine("            this.Version=version;");
        builder.AppendLine("            this.Data=data;");
        builder.AppendLine("        }");
        builder.AppendLine("        public int Version { get; }");
        builder.AppendLine("        public string Data { get; }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("namespace Seamless.SourceGenerator.Generated {");
        builder.AppendLine("    internal static partial class LinqInvocationInterceptors {");
        インデント付き追記(builder,RenderCarrierHelperTypes(),"        ");

        var index = 0;
        var emittedMethodNames = new HashSet<string>(StringComparer.Ordinal);
        foreach(var target in targets) {
            try {
                if(target.IsLocalMethodInterception) {
                    var ローカル静的メソッド群 = string.Empty;
                    var ローカル本体 = string.Empty;
                    if(生成ローカルメソッド本体構築0(target,out ローカル静的メソッド群,out ローカル本体)) {
                        builder.AppendLine($"        /// <summary>Intercepts {target.Method.Name} inside {target.ContainingMethodName}.</summary>");
                        if(!string.IsNullOrWhiteSpace(ローカル静的メソッド群)) {
                            インデント付き追記(builder,ローカル静的メソッド群,"        ");
                        }
                        builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                        builder.AppendLine($"        {target.InterceptsLocationAttributeText}");
                        builder.AppendLine($"        public static {target.ReturnTypeName} {target.MethodName}{target.MethodTypeParameterList}({target.InterceptorParameterList}) {{");
                        インデント付き追記(builder,ローカル本体,"            ");
                        builder.AppendLine("        }");
                        builder.AppendLine();
                        index++;
                        continue;
                    }

                    builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                    builder.AppendLine($"        {target.InterceptsLocationAttributeText}");
                    builder.AppendLine($"        public static {target.ReturnTypeName} {target.MethodName}{target.MethodTypeParameterList}({target.InterceptorParameterList}) {{");
                    if(ローカルメソッド未対応Enumerable取得31(target) is { Count: > 0 } unsupportedMethods) {
                        インデント付き追記(builder,RenderUnsupportedLocalMethodBody(unsupportedMethods),"            ");
                    } else if(ローカルメソッドEnumerable呼び出し含む34(target)) {
                        // LINQ を含むローカルメソッドをインライン展開できない場合、元メソッドへ
                        // reflection forward すると最適化失敗を隠してしまうため、明示的に例外を生成する。
                        インデント付き追記(builder,RenderUninlineableLocalMethodBody(target),"            ");
                    } else {
                        if(TryRenderSimpleLocalMethodBody(target,out var simpleLocalMethodBody)) {
                            インデント付き追記(builder,simpleLocalMethodBody,"            ");
                        } else {
                            // 以前はここで常に reflection forward していた。
                            // インライン化できない単純ローカルメソッドの退避経路として残しておく。
                            // インデント付き追記(builder,RenderLocalMethodForwardBody(target),"            ");
                            インデント付き追記(builder,RenderLocalMethodForwardBody(target),"            ");
                        }
                    }
                    builder.AppendLine("        }");
                    builder.AppendLine();
                    index++;
                    continue;
                }

                var 直接静的メソッド群 = string.Empty;
                var 直接本体 = string.Empty;
                if(!直接呼び出しソース生成19(target,out 直接静的メソッド群,out 直接本体)) {
                    直接本体=RenderUnsupportedDirectMethodBody(target);
                }

                if(!emittedMethodNames.Add(target.MethodName)) {
                    index++;
                    continue;
                }

                builder.AppendLine($"        /// <summary>Intercepts {target.Method.Name} inside {target.ContainingMethodName}.</summary>");
                if(!string.IsNullOrWhiteSpace(直接静的メソッド群)) {
                    インデント付き追記(builder,直接静的メソッド群,"        ");
                }
                builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                builder.AppendLine($"        {target.InterceptsLocationAttributeText}");
                builder.AppendLine($"        public static {target.ReturnTypeName} {target.MethodName}{target.MethodTypeParameterList}({target.InterceptorParameterList}) {{");
                インデント付き追記(builder,直接本体,"            ");
                builder.AppendLine("        }");
                builder.AppendLine();
            } catch(Exception ex) {
                if(emittedMethodNames.Contains(target.MethodName)) {
                    index++;
                    continue;
                }

                emittedMethodNames.Add(target.MethodName);
                builder.AppendLine($"        /// <summary>Intercepts {target.Method.Name} inside {target.ContainingMethodName}.</summary>");
                builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                builder.AppendLine($"        {target.InterceptsLocationAttributeText}");
                builder.AppendLine($"        public static {target.ReturnTypeName} {target.MethodName}{target.MethodTypeParameterList}({target.InterceptorParameterList}) {{");
                インデント付き追記(builder,RenderUnsupportedDirectMethodBody(target,ex.Message),"            ");
                builder.AppendLine("        }");
                builder.AppendLine();
            }
            index++;
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    // ローカルメソッドを丸ごと再構成し、actual 代入文だけをフラグメント実行へ差し替える。
    private static bool 生成ローカルメソッド本体構築0(Linq傍受対象型 対象,out string 静的メソッド群,out string 本体) {
        静的メソッド群=string.Empty;
        本体=string.Empty;

        // 以前は識別子ハッシュから開始番号を作っていたため Struct1144748 のような読みにくい名前になっていた。
        // いまはメソッドごとに 0 から連番を振り、必要なら外側で補助クラスに包んで衝突を避ける。
        var フラグメント番号 = フラグメント番号初期値取得18();
        if(対象.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax { Body: { } メソッド本体 } メソッド構文) {
            return false;
        }

        var actual文位置 = -1;
        VariableDeclaratorSyntax? actual変数 = null;
        InvocationExpressionSyntax? actual呼び出し = null;
        for(var 文番号 = 0; 文番号<メソッド本体.Statements.Count; 文番号++) {
            if(メソッド本体.Statements[文番号] is not LocalDeclarationStatementSyntax ローカル宣言) {
                continue;
            }

            var 候補変数 = ローカル宣言.Declaration.Variables.FirstOrDefault(static 変数 => 変数.Identifier.ValueText=="actual");
            if(候補変数?.Initializer?.Value is InvocationExpressionSyntax 呼び出し式) {
                actual文位置=文番号;
                actual変数=候補変数;
                actual呼び出し=呼び出し式;
                break;
            }
        }

        if(actual文位置<0 || actual変数 is null || actual呼び出し is null) {
            return false;
        }

        var セマンティックモデル = 対象.SemanticModel.Compilation.GetSemanticModel(メソッド構文.SyntaxTree);
        if(!呼び出し式フラグメント生成1(セマンティックモデル,actual呼び出し,対象.EnclosingType,ref フラグメント番号,out var フラグメント,out 静的メソッド群)) {
            return false;
        }

        if(フラグメント is Enumerable actualEnumerableFragment) {
            静的メソッド群=静的メソッド連結14(静的メソッド群,actualEnumerableFragment.GetEnumeratorStruct);
        }

        var 本体ビルダー = new StringBuilder();
        for(var 文番号 = 0; 文番号<メソッド本体.Statements.Count; 文番号++) {
            var 文 = メソッド本体.Statements[文番号];
            if(文番号==actual文位置) {
                // `actual` だけは LINQ 呼び出しのまま残さず、生成したフラグメント列で直接評価する。
                if(フラグメント is Enumerable enumerableFragment && !enumerableFragment.CanExpandInLocalMethodBody) {
                    本体ビルダー.AppendLine($"var {actual変数.Identifier.ValueText}={enumerableFragment.GetEnumerableExpression};");
                } else {
                    フラグメント実行文追加15(本体ビルダー,フラグメント);
                    本体ビルダー.AppendLine($"var {actual変数.Identifier.ValueText}={フラグメント.Result};");
                }
                continue;
            }

            // それ以外の文は this 参照やラムダ呼び出しだけ正規化して、元の制御フローを保つ。
            if(!文生成16(文,セマンティックモデル,対象.EnclosingType,out var 生成文)) {
                return false;
            }
            本体ビルダー.AppendLine(生成文);
        }

        本体=本体ビルダー
            .ToString()
            .Replace("throw NoElements();","throw global::Seamless.Linq.Enumerable.NoElements();",StringComparison.Ordinal);

        静的メソッド群と本体を補助クラスへ退避21(対象.MethodName,ref 静的メソッド群,ref 本体);
        return true;
    }

    // 呼び出し式から対応する Fragment を選び、再生成に必要な静的補助コードも回収する。
    /// <summary>
    /// source.Method(...)
    /// ↓
    /// Method に応じた Fragment 連結へ振り分ける
    /// 例:
    /// source.Any(x => x > 0) → Where(x => x > 0) → Any()
    /// source.Join(inner, outerKey, innerKey, result) → Join(...)
    /// </summary>
    private static bool 呼び出し式フラグメント生成1(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods) {
        fragment=null!;
        staticMethods=string.Empty;

        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        var definition = method.ReducedFrom ?? method;
        var methodName = definition.Name;

        if(invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression }) {
            if(methodName=="Any" && method.Parameters.Length==0) {
                return false;
            }
        }

        if(methodName=="Any") {
            return Anyフラグメント生成3(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods);
        }

        return 登録済みフラグメント生成24(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods);
    }
    /// <summary>
    /// source.Any(x => x > 0)
    /// ↓
    /// Where(x => x > 0) → Any()
    /// </summary>
    private static bool Anyフラグメント生成3(SemanticModel semanticModel,InvocationExpressionSyntax invocation,INamedTypeSymbol containingType,ref int uniqueNumber,out Fragment fragment,out string staticMethods) {
        fragment=null!;
        staticMethods=string.Empty;
        if(invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression }) {
            return false;
        }

        if(!列挙フラグメント生成9(semanticModel,sourceExpression,containingType,ref uniqueNumber,out var sourceFragment,out var sourceStaticMethods)) {
            return false;
        }

        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        if(invocation.ArgumentList.Arguments.Count==1) {
            if(!関数フラグメント生成12(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,out var predicate,semanticModel,containingType)) {
                return false;
            }

            var whereVariantName = predicate.ParameterTypeNames.Length>1 ? "index" : string.Empty;
            if(!フラグメント候補選択25("Where",whereVariantName,out var whereCandidate)) {
                return false;
            }

            var whereFragment = (Fragment?)whereCandidate.Constructor.Invoke(new object?[] { uniqueNumber++,sourceFragment,predicate });
            if(whereFragment is not Enumerable whereEnumerable) {
                return false;
            }

            sourceStaticMethods=静的メソッド連結14(sourceStaticMethods,whereFragment.StructMethod);
            sourceFragment=whereEnumerable;
        }

        if(!フラグメント候補選択25("Any",string.Empty,out var anyCandidate)) {
            return false;
        }

        fragment=(Fragment)anyCandidate.Constructor.Invoke(new object?[] { uniqueNumber++,sourceFragment });
        staticMethods=静的メソッド連結14(sourceStaticMethods,fragment.StructMethod);
        return true;
    }

    /// <summary>
    /// Fragment 継承型の具体実装を列挙し、
    /// Aggregate_seed_func や Where_index のような型名から
    /// 「どの LINQ メソッドのどの変種か」を逆引きできる一覧へ正規化する。
    /// </summary>
    private static IReadOnlyDictionary<string,ImmutableArray<フラグメント候補>> フラグメント候補一覧構築23() {
        var fragmentType = typeof(Fragment);
        var helperTypes = new HashSet<Type> {
            typeof(Fragment),
            typeof(Scalar),
            typeof(Enumerable),
            typeof(Root),
        };

        var candidates = fragmentType.Assembly.GetTypes()
            .Where(type => !type.IsAbstract
                && fragmentType.IsAssignableFrom(type)
                && !helperTypes.Contains(type))
            .Select(type => {
                var constructor = type.GetConstructors().SingleOrDefault();
                if(constructor is null) {
                    return null;
                }

                var parameters = constructor.GetParameters();
                if(parameters.Length==0 || parameters[0].ParameterType!=typeof(int)) {
                    return null;
                }

                var parts = type.Name.Split('_');
                return new フラグメント候補 {
                    FragmentType=type,
                    Constructor=constructor,
                    MethodName=parts[0],
                    VariantName=parts.Length>1 ? parts[^1] : string.Empty,
                    Parameters=parameters[1..],
                };
            })
            .Where(static candidate => candidate is not null)
            .Cast<フラグメント候補>()
            .OrderByDescending(static candidate => candidate.Parameters.Length)
            .ThenBy(static candidate => candidate.FragmentType.Name, StringComparer.Ordinal)
            .GroupBy(static candidate => candidate.MethodName, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToImmutableArray(),
                StringComparer.Ordinal);

        return candidates;
    }

    /// <summary>
    /// source.Method(arg1, arg2, ...)
    /// ↓
    /// Fragment 候補一覧から Method 名に対応する型
    /// 例:
    /// Aggregate_func / Aggregate_seed_func / Aggregate_seed_func_resultSelector
    /// Where / Where_index
    /// Join / GroupJoin
    /// を順に試し、成立したチェーンへ正規化する。
    /// </summary>
    private static bool 登録済みフラグメント生成24(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods) {
        fragment=null!;
        staticMethods=string.Empty;

        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression }) {
            return false;
        }

        var methodName = (method.ReducedFrom ?? method).Name;
        if(!フラグメント候補一覧.TryGetValue(methodName,out var candidates)) {
            return false;
        }

        foreach(var candidate in candidates) {
            var snapshot = uniqueNumber;
            if(登録済みフラグメント生成試行26(candidate,semanticModel,invocation,sourceExpression,method,containingType,ref snapshot,out fragment,out staticMethods)) {
                uniqueNumber=snapshot;
                return true;
            }
        }

        return false;
    }

    private static bool フラグメント候補選択25(string methodName,string variantName,out フラグメント候補 candidate) {
        candidate=null!;
        return フラグメント候補一覧.TryGetValue(methodName,out var candidates)
            && (candidate=candidates.FirstOrDefault(current => current.VariantName==variantName)) is not null;
    }

    /// <summary>
    /// source.Method(arg1, arg2, ...)
    /// ↓
    /// candidate(番号, sourceFragment, ...)
    /// のコンストラクター形へ引数を並べ替えて、1 本の Fragment チェーンへ正規化する。
    /// 例:
    /// source.Aggregate(seed, func) → Aggregate_seed_func(sourceFragment, seed, funcFragment)
    /// source.Select(selector) → Select(sourceFragment, selectorFragment)
    /// source.Join(inner, outerKey, innerKey, result, comparer?)
    ///   → Join(outerFragment, innerFragment, outerKeyFragment, innerKeyFragment, resultFragment, comparer)
    /// </summary>
    private static bool 登録済みフラグメント生成試行26(
        フラグメント候補 candidate,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax sourceExpression,
        IMethodSymbol method,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods) {
        fragment=null!;
        staticMethods=string.Empty;

        if(!候補使用可能27(candidate,method)) {
            return false;
        }

        var constructorArguments = new List<object?>(candidate.Parameters.Length+1) { uniqueNumber++ };
        if(!列挙フラグメント生成9(semanticModel,sourceExpression,containingType,ref uniqueNumber,out var sourceFragment,out var sourceStaticMethods)) {
            return false;
        }

        constructorArguments.Add(sourceFragment);
        staticMethods=sourceStaticMethods;

        var argumentIndex = 0;
        for(var parameterIndex = 1; parameterIndex<candidate.Parameters.Length; parameterIndex++) {
            var parameter = candidate.Parameters[parameterIndex];
            if(argumentIndex>=invocation.ArgumentList.Arguments.Count) {
                if(parameter.ParameterType==typeof(string)) {
                    if(TryResolveSyntheticStringArgument(parameter,method,out var syntheticArgument)) {
                        constructorArguments.Add(syntheticArgument);
                        continue;
                    }

                    constructorArguments.Add("null");
                    continue;
                }
                return false;
            }

            var argumentExpression = invocation.ArgumentList.Arguments[argumentIndex].Expression;
            if(parameter.ParameterType==typeof(string)) {
                constructorArguments.Add(RenderExpressionForGeneratedMethod(argumentExpression,semanticModel,containingType));
                argumentIndex++;
                continue;
            }

            if(typeof(FuncFragment).IsAssignableFrom(parameter.ParameterType)) {
                if(!関数フラグメント生成12(argumentExpression,method.Parameters[argumentIndex].Type,out var funcFragment,semanticModel,containingType)
                    || !関数フラグメント利用可能28(funcFragment,candidate.FragmentType)) {
                    return false;
                }

                constructorArguments.Add(funcFragment);
                argumentIndex++;
                continue;
            }

            if(typeof(Enumerable).IsAssignableFrom(parameter.ParameterType)) {
                if(!単独列挙フラグメント生成10(semanticModel,argumentExpression,containingType,ref uniqueNumber,out var enumerableFragment,out var enumerableStaticMethods)) {
                    return false;
                }

                constructorArguments.Add(enumerableFragment);
                staticMethods=静的メソッド連結14(staticMethods,enumerableStaticMethods);
                argumentIndex++;
                continue;
            }

            return false;
        }

        if(argumentIndex!=invocation.ArgumentList.Arguments.Count) {
            return false;
        }

        if(candidate.Constructor.Invoke(constructorArguments.ToArray()) is not Fragment generatedFragment) {
            return false;
        }

        fragment=generatedFragment;
        staticMethods=静的メソッド連結14(staticMethods,fragment.StructMethod);
        return true;
    }

    private static bool TryResolveSyntheticStringArgument(ParameterInfo parameter,IMethodSymbol method,out string value) {
        value=string.Empty;
        if(parameter.Name is not ("resultElementType" or "resultType")) {
            return false;
        }

        if(parameter.Name=="resultType") {
            value=method.ReturnType.ToDisplayString(完全修飾形式);
            return true;
        }

        if(!列挙要素型取得11(method.ReturnType,out var elementType,out _)) {
            return false;
        }

        value=elementType.ToDisplayString(完全修飾形式);
        return true;
    }

    private static bool 候補使用可能27(フラグメント候補 candidate,IMethodSymbol method) {
        if(candidate.MethodName is "Where" or "Select" or "SkipWhile" or "TakeWhile") {
            var isIndexOverload = method.Parameters.Length>0
                && method.Parameters[0].Type is INamedTypeSymbol predicateType
                && predicateType.TypeArguments.Length==3;
            return isIndexOverload
                ? candidate.VariantName=="index"
                : candidate.VariantName.Length==0;
        }

        return true;
    }

    private static bool 関数フラグメント利用可能28(FuncFragment funcFragment,Type fragmentType) {
        if(funcFragment.CanGenerateMethod) {
            return true;
        }

        if(funcFragment is ParameterFunc
            && fragmentType is { } type
            && (type==typeof(Select) || type.Name=="Select_index")) {
            return true;
        }

        return fragmentType==typeof(Aggregate_func);
    }

    /// <summary>
    /// source.Select(...).Where(...)
    /// ↓
    /// Select(...) → Where(...)
    /// のように、入れ子の LINQ 呼び出しなら再帰的に Fragment チェーンへほどく。
    /// LINQ 呼び出しでなければ Root(sourceExpression) として扱う。
    /// </summary>
    private static bool 列挙フラグメント生成9(SemanticModel semanticModel,ExpressionSyntax expression,INamedTypeSymbol containingType,ref int uniqueNumber,out Enumerable fragment,out string staticMethods) {
        if(expression is InvocationExpressionSyntax invocation
            && 呼び出し式フラグメント生成1(semanticModel,invocation,containingType,ref uniqueNumber,out var nestedFragment,out staticMethods)
            && nestedFragment is Enumerable enumerableFragment) {
            fragment=enumerableFragment;
            return true;
        }

        return 単独列挙フラグメント生成10(semanticModel,expression,containingType,ref uniqueNumber,out fragment,out staticMethods);
    }

    /// <summary>
    /// Source() / Second() / rows
    /// ↓
    /// Root(sourceExpression)
    /// として、メソッドチェーンの起点になる列挙ソースへ正規化する。
    /// </summary>
    private static bool 単独列挙フラグメント生成10(SemanticModel semanticModel,ExpressionSyntax expression,INamedTypeSymbol containingType,ref int uniqueNumber,out Enumerable fragment,out string staticMethods) {
        staticMethods=string.Empty;
        if(!列挙要素型取得11(semanticModel.GetTypeInfo(expression).Type,out var elementType,out var useEnumerableInterface)) {
            fragment=null!;
            return false;
        }

        fragment=CreateRootEnumerableFragment(
            RenderExpressionForGeneratedMethod(expression,semanticModel,containingType),
            elementType.ToDisplayString(完全修飾形式),
            useEnumerableInterface,
            uniqueNumber++);
        return true;
    }

    private static bool 列挙要素型取得11(ITypeSymbol? type,out ITypeSymbol elementType,out bool useEnumerableInterface) {
        switch(type) {
            case IArrayTypeSymbol arrayType:
                elementType=arrayType.ElementType;
                useEnumerableInterface=true;
                return true;
            case INamedTypeSymbol namedType when IsIEnumerableType(namedType) && namedType.TypeArguments.Length==1:
                elementType=namedType.TypeArguments[0];
                useEnumerableInterface=true;
                return true;
            case INamedTypeSymbol namedType:
                var enumerableInterface = namedType.AllInterfaces.FirstOrDefault(static candidate => IsIEnumerableType(candidate) && candidate.TypeArguments.Length==1);
                if(enumerableInterface is not null) {
                    elementType=enumerableInterface.TypeArguments[0];
                    useEnumerableInterface=false;
                    return true;
                }
                break;
        }

        elementType=null!;
        useEnumerableInterface=false;
        return false;
    }

    /// <summary>
    /// x => x * 2 / (left, right) => left + right
    /// ↓
    /// Select や Aggregate に渡せる FuncFragment へ正規化する。
    /// 生成可能な static ラムダなら補助メソッド化し、
    /// 閉包付きラムダならインライン代入だけ許可する。
    /// </summary>
    private static bool 関数フラグメント生成12(
        ExpressionSyntax expression,
        ITypeSymbol delegateType,
        out FuncFragment fragment,
        SemanticModel? semanticModel = null,
        INamedTypeSymbol? containingType = null) {
        fragment=null!;
        if(expression is LambdaExpressionSyntax lambda
            && TryCreateLambdaFuncFragment(lambda,delegateType,out var lambdaSpec)) {
            var canGenerateMethod = lambda.Modifiers.Any(SyntaxKind.StaticKeyword) || IsEffectivelyStaticLambda(lambda);
            fragment = new LambdaFunc {
                Type = Func型解決13(lambdaSpec.ParameterTypeNames.Length),
                ReturnTypeName = lambdaSpec.ReturnTypeName,
                ParameterTypeNames = lambdaSpec.ParameterTypeNames,
                ParameterNames = lambdaSpec.ParameterNames,
                DefineLambda = $" => {lambdaSpec.Body};",
                CanGenerateMethod = canGenerateMethod,
                CanInlineAssign = true,
                InlineBody = lambdaSpec.Body
            };
            return true;
        }

        if(semanticModel is not null
            && containingType is not null
            && TryCreateDelegateParameterFuncFragment(expression,delegateType,semanticModel,containingType,out fragment)) {
            return true;
        }

        return false;
    }

    private static Type Func型解決13(int parameterCount) => parameterCount switch {
        1 => typeof(Func<,>),
        2 => typeof(Func<,,>),
        _ => throw new InvalidOperationException($"Unsupported parameter count: {parameterCount}."),
    };

    private static bool TryCreateDelegateParameterFuncFragment(
        ExpressionSyntax expression,
        ITypeSymbol delegateType,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        out FuncFragment fragment) {
        fragment=null!;
        if(delegateType is not INamedTypeSymbol funcType
            || !IsInlineDelegateType(funcType)
            || funcType.TypeArguments.Any(ContainsAnonymousType)) {
            return false;
        }

        var convertedType = semanticModel.GetTypeInfo(expression).ConvertedType;
        if(convertedType is not INamedTypeSymbol convertedFuncType
            || !IsInlineDelegateType(convertedFuncType)) {
            return false;
        }

        var typeArguments = funcType.TypeArguments.Select(typeArgument => typeArgument.ToDisplayString(完全修飾形式)).ToArray();
        var parameterTypeNames = typeArguments.Take(typeArguments.Length-1).ToArray();
        fragment = new ParameterFunc {
            ParameterName=RenderExpressionForGeneratedMethod(expression,semanticModel,containingType),
            Type=Func型解決13(parameterTypeNames.Length),
            ReturnTypeName=typeArguments[^1],
            ParameterTypeNames=parameterTypeNames,
            ParameterNames=Array.Empty<string>(),
            CanGenerateMethod=false,
            CanInlineAssign=false,
        };
        return true;
    }

    private static string 静的メソッド連結14(string left,string right) {
        return string.Join("\n",new[] { left,right }.Where(static text => !string.IsNullOrWhiteSpace(text)));
    }

    private static void フラグメント実行文追加15(StringBuilder builder,Fragment fragment) {
        // 生成したフラグメントは「前準備 -> ループ外初期化 -> ループ -> 片付け」の順で実行する。
        AppendBlock(builder,fragment.GetBeginGlobal);
        AppendBlock(builder,fragment.GetBeginLoopOutside);
        AppendBlock(builder,fragment.GetBeginLoop);
        AppendBlock(builder,fragment.GetBeginLoopInside);
        if(fragment is Enumerable enumerableFragment) {
            AppendBlock(builder,enumerableFragment.GetAdd);
        }
        AppendBlock(builder,fragment.GetEndLoopInside);
        AppendBlock(builder,fragment.GetEndLoop);
        AppendBlock(builder,fragment.GetEndLoopOutside);
        AppendBlock(builder,fragment.GetEndGlobal);
    }

    private static bool 文生成16(StatementSyntax statement,SemanticModel semanticModel,INamedTypeSymbol containingType,out string generatedStatement) {
        switch(statement) {
            case LocalDeclarationStatementSyntax localDeclaration:
                generatedStatement=ローカル宣言生成17(localDeclaration,semanticModel,containingType);
                return true;
            case ExpressionStatementSyntax expressionStatement when 式文生成29(expressionStatement,semanticModel,containingType,out generatedStatement):
                return true;
            case ExpressionStatementSyntax expressionStatement:
                generatedStatement=$"{RenderExpressionForGeneratedMethod(expressionStatement.Expression,semanticModel,containingType)};";
                return true;
            default:
                generatedStatement=statement.ToString();
                return true;
        }
    }

    private static string ローカル宣言生成17(LocalDeclarationStatementSyntax statement,SemanticModel semanticModel,INamedTypeSymbol containingType) {
        var variables = statement.Declaration.Variables.Select(variable => {
            if(variable.Initializer?.Value is null) {
                return variable.ToString();
            }
            return $"{variable.Identifier.ValueText}={RenderExpressionForGeneratedMethod(variable.Initializer.Value,semanticModel,containingType)}";
        });
        return $"{statement.Declaration.Type} {string.Join(", ",variables)};";
    }

    private static int フラグメント番号初期値取得18() => 0;

    private static bool 式文生成29(ExpressionStatementSyntax statement,SemanticModel semanticModel,INamedTypeSymbol containingType,out string generatedStatement) {
        generatedStatement=string.Empty;
        return statement.Expression is InvocationExpressionSyntax invocation
            && 非公開同一型呼び出し文生成30(invocation,semanticModel,containingType,out generatedStatement);
    }

    private static bool 非公開同一型呼び出し文生成30(InvocationExpressionSyntax invocation,SemanticModel semanticModel,INamedTypeSymbol containingType,out string generatedStatement) {
        generatedStatement=string.Empty;
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType,containingType)
            || method.DeclaredAccessibility!=Accessibility.Private) {
            return false;
        }

        var originalMethod = method.OriginalDefinition;
        var localFunctionTypeParameters = originalMethod.TypeParameters.Length==0
            ? string.Empty
            : "<"+string.Join(", ",originalMethod.TypeParameters.Select(static parameter => parameter.Name))+">";
        var targetTypeName = method.ContainingType.ToDisplayString(完全修飾形式);
        var accessorFunctionName = $"__UnsafeAccessor_{originalMethod.Name}";
        var localFunctionParameters = new List<string>();
        var accessorFunctionParameters = new List<string>();
        var localFunctionArguments = new List<string>();
        if(method.IsStatic) {
            accessorFunctionParameters.Add($"{targetTypeName} _");
        } else {
            accessorFunctionParameters.Add($"{targetTypeName} @this");
        }
        for(var index = 0; index<invocation.ArgumentList.Arguments.Count; index++) {
            var parameter = originalMethod.Parameters[index];
            var parameterDeclaration = $"{RefKind接頭辞取得35(parameter.RefKind)}{parameter.Type.ToDisplayString(完全修飾形式)} {parameter.Name}";
            localFunctionParameters.Add(parameterDeclaration);
            accessorFunctionParameters.Add(parameterDeclaration);
            localFunctionArguments.Add(RenderArgumentForGeneratedMethod(invocation.ArgumentList.Arguments[index],semanticModel,containingType));
        }

        var accessorKind = method.IsStatic
            ? "global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod"
            : "global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method";
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine($"[global::System.Runtime.CompilerServices.UnsafeAccessorAttribute({accessorKind}, Name = \"{originalMethod.Name}\")]");
        builder.AppendLine($"extern static {method.ReturnType.ToDisplayString(完全修飾形式)} {accessorFunctionName}{localFunctionTypeParameters}({string.Join(", ",accessorFunctionParameters)});");
        var accessorArguments = method.IsStatic
            ? $"default!, {string.Join(", ",localFunctionParameters.Select(static parameter => parameter.Split(' ').Last()))}"
            : $"@this, {string.Join(", ",localFunctionParameters.Select(static parameter => parameter.Split(' ').Last()))}";
        accessorArguments=accessorArguments.TrimEnd(' ', ',');
        var localWrapperModifier = method.IsStatic ? "static " : string.Empty;
        if(method.ReturnsVoid) {
            builder.AppendLine($"{localWrapperModifier}void {originalMethod.Name}{localFunctionTypeParameters}({string.Join(", ",localFunctionParameters)}) => {accessorFunctionName}({accessorArguments});");
        } else {
            builder.AppendLine($"{localWrapperModifier}{method.ReturnType.ToDisplayString(完全修飾形式)} {originalMethod.Name}{localFunctionTypeParameters}({string.Join(", ",localFunctionParameters)}) => {accessorFunctionName}({accessorArguments});");
        }
        builder.AppendLine($"{originalMethod.Name}({string.Join(", ",localFunctionArguments)});");
        builder.AppendLine("}");
        generatedStatement=builder.ToString().TrimEnd();
        return true;
    }

    private static bool 直接呼び出しソース生成19(Linq傍受対象型 target,out string staticMethods,out string body) {
        staticMethods=string.Empty;
        body=string.Empty;
        var uniqueNumber=フラグメント番号初期値取得18();

        if(直接呼び出しEnumerator生成33(target,ref uniqueNumber,out staticMethods,out body)) {
            静的メソッド群と本体を補助クラスへ退避21(target.MethodName,ref staticMethods,ref body);
            return true;
        }

        if(TryRenderCarrierDirectMethodBody(target,out body)) {
            静的メソッド群と本体を補助クラスへ退避21(target.MethodName,ref staticMethods,ref body);
            return true;
        }

        // Fragment/Carrier として最適化できなかった LINQ 呼び出しを
        // System.Linq.Enumerable へ戻すフォールバックは生成しない。
        // フォールバックを出すと「インターセプトされたが最適化されていない」状態が
        // テストや生成コード上で見えなくなるため、呼び出し側で例外 body を生成させる。
        return false;
    }

    private static bool 直接呼び出し式生成20(
        Linq傍受対象型 target,
        InvocationExpressionSyntax invocation,
        ref int uniqueNumber,
        out string staticMethods,
        out string expressionText) {
        staticMethods=string.Empty;
        expressionText=string.Empty;

        if(target.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || method.ReducedFrom is null
            || invocation.Expression is not MemberAccessExpressionSyntax) {
            return false;
        }

        var sourceText="source";

        var parameters = method.Parameters;
        var arguments = invocation.ArgumentList.Arguments;
        if(parameters.Length!=arguments.Count) {
            return false;
        }

        var renderedArguments = new string[arguments.Count+1];
        renderedArguments[0]=sourceText;
        for(var index = 0; index<arguments.Count; index++) {
            var argumentExpression = arguments[index].Expression;
            var parameter = parameters[index];

            if(argumentExpression is LambdaExpressionSyntax lambda) {
                if(!TryCreateLambdaFuncFragment(lambda,parameter.Type,out var lambdaSpec)) {
                    return false;
                }

                var isStaticLambda = lambda.Modifiers.Any(SyntaxKind.StaticKeyword) || IsEffectivelyStaticLambda(lambda);
                if(isStaticLambda) {
                    var structName = $"Struct{uniqueNumber++}";
                    var methodName = parameter.Name;
                    var definition = $$"""
                        private struct {{structName}}{
                            public static {{lambdaSpec.ReturnTypeName}} {{methodName}}({{string.Join(", ",lambdaSpec.ParameterTypeNames.Zip(lambdaSpec.ParameterNames,(type,name) => $"{type} {name}"))}}) => {{lambdaSpec.Body}};
                        }
                        """;
                    staticMethods=string.Join(
                        "\n",
                        new[] { staticMethods,definition }.Where(static text => !string.IsNullOrWhiteSpace(text)));
                    renderedArguments[index+1]=$"{structName}.{methodName}";
                    continue;
                }

                renderedArguments[index+1]=parameter.Name;
                continue;
            }

            renderedArguments[index+1]=NormalizeExpressionText(argumentExpression.ToString());
        }

        expressionText=$"global::System.Linq.Enumerable.{method.Name}({string.Join(", ",renderedArguments)})";
        return true;
    }

    private static bool 直接呼び出しEnumerator生成33(
        Linq傍受対象型 target,
        ref int uniqueNumber,
        out string staticMethods,
        out string body) {
        staticMethods=string.Empty;
        body=string.Empty;

        if(target.Method.ReturnType is not INamedTypeSymbol returnType
            || !IsIEnumerableType(returnType)
            || !列挙要素型取得11(target.Method.ReturnType,out var returnElementType,out _)
            || target.Method.ReducedFrom is null
            || target.Invocation.Expression is not MemberAccessExpressionSyntax) {
            return false;
        }

        var methodName = target.Method.ReducedFrom.Name;
        if(!フラグメント候補一覧.TryGetValue(methodName,out var candidates)) {
            return false;
        }

        foreach(var candidate in candidates) {
            var snapshot = uniqueNumber;
            if(!候補使用可能27(candidate,target.Method)) {
                continue;
            }

            var constructorArguments = new List<object?>(candidate.Parameters.Length+1) {
                snapshot++,
                CreateRootEnumerableFragment("source",target.SourceElementTypeName,useEnumerableInterface: true,snapshot++)
            };

            var argumentIndex = 0;
            var supported = true;
            for(var parameterIndex = 1; parameterIndex<candidate.Parameters.Length; parameterIndex++) {
                var parameter = candidate.Parameters[parameterIndex];
                if(argumentIndex>=target.Invocation.ArgumentList.Arguments.Count) {
                    if(parameter.ParameterType==typeof(string)) {
                        if(TryResolveSyntheticStringArgument(parameter,target.Method,out var syntheticArgument)) {
                            constructorArguments.Add(syntheticArgument);
                            continue;
                        }
                    }
                    supported=false;
                    break;
                }

                var argumentExpression = target.Invocation.ArgumentList.Arguments[argumentIndex].Expression;
                if(parameter.ParameterType==typeof(string)) {
                    constructorArguments.Add(RenderExpressionForGeneratedMethod(argumentExpression,target.SemanticModel,target.EnclosingType));
                    argumentIndex++;
                    continue;
                }

                if(typeof(FuncFragment).IsAssignableFrom(parameter.ParameterType)) {
                    if(argumentIndex>=target.Parameters.Length
                        || target.Parameters[argumentIndex].TemplateArgument is not FuncFragment funcFragment
                        || !関数フラグメント利用可能28(funcFragment,candidate.FragmentType)) {
                        supported=false;
                        break;
                    }

                    constructorArguments.Add(funcFragment);
                    argumentIndex++;
                    continue;
                }

                supported=false;
                break;
            }

            if(!supported || argumentIndex!=target.Invocation.ArgumentList.Arguments.Count) {
                continue;
            }

            if(candidate.Constructor.Invoke(constructorArguments.ToArray()) is not Enumerable fragment
                || fragment.ElementType!=returnElementType.ToDisplayString(完全修飾形式)) {
                continue;
            }

            uniqueNumber=snapshot;
            staticMethods=静的メソッド連結14(fragment.StructMethod,fragment.GetEnumeratorStruct);
            body=$"return {fragment.GetEnumerableExpression};";
            return true;
        }

        return false;
    }

    // Struct0 / Struct1 ... のような補助型はクラス直下だと他のインターセプターと衝突するため、
    // メソッド単位の補助クラスへ退避し、本体側の参照だけを差し替える。
    private static void 静的メソッド群と本体を補助クラスへ退避21(string methodName,ref string staticMethods,ref string body) {
        if(string.IsNullOrWhiteSpace(staticMethods)) {
            return;
        }

        var 補助クラス名 = $"{SanitizeIdentifier(methodName)}_補助0";
        staticMethods = $$"""
            private static class {{補助クラス名}}{
            {{staticMethods.Replace("private struct ","public struct ",StringComparison.Ordinal)}}
            }
            """;
        body = System.Text.RegularExpressions.Regex.Replace(
            body,
            @"\bStruct(?<番号>\d+)(?<接尾辞>_[A-Za-z_][A-Za-z0-9_]*)?\b",
            $"{補助クラス名}.Struct${{番号}}${{接尾辞}}",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static string SanitizeIdentifier(string value) {
        if(string.IsNullOrWhiteSpace(value)) {
            return "Aggregate";
        }

        var builder = new StringBuilder(value.Length);
        foreach(var c in value) {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        if(builder.Length==0 || !SyntaxFacts.IsIdentifierStartCharacter(builder[0])) {
            builder.Insert(0,'_');
        }

        return builder.ToString();
    }

    private static string 名前空間テキスト取得(INamespaceSymbol namespaceSymbol) {
        return namespaceSymbol.IsGlobalNamespace ? string.Empty : namespaceSymbol.ToDisplayString();
    }

    private static void インデント付き追記(StringBuilder builder,string value,string indent) {
        foreach(var line in value.Replace("\r\n","\n").Split('\n')) {
            if(line.Length==0) {
                builder.AppendLine();
                continue;
            }

            builder.Append(indent);
            builder.AppendLine(line);
        }
    }

    private static string 空行除去(string source) {
        var lines = source.Replace("\r\n","\n").Split('\n');
        var builder = new StringBuilder(source.Length);
        var firstWritten = true;
        foreach(var line in lines) {
            if(string.IsNullOrWhiteSpace(line)) {
                continue;
            }
            if(!firstWritten) {
                builder.Append('\n');
            }
            builder.Append(line);
            firstWritten=false;
        }
        return builder.ToString();
    }

    private static 型対象型? 型対象生成(GeneratorSyntaxContext context) {
        if(context.Node is not TypeDeclarationSyntax declaration) {
            return null;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(declaration);
        if(symbol is null) {
            return null;
        }

        var isMarker = symbol.Name=="DatabaseGenerator";
        var isPartial = declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
        var isContainerCandidate = symbol.TypeKind==TypeKind.Class;
        if(!isMarker && !isPartial && !isContainerCandidate) {
            return null;
        }

        return new 型対象型(symbol,isPartial);
    }

    private static bool IsEffectivelyStaticLambda(LambdaExpressionSyntax lambda) {
        // 実質staticか判定: 明示的なstatic修飾があるか、非staticでも閉じ込める変数がない等の判定ロジックを実装
        // ここでは単純にstatic修飾有無を判定し、本来は閉じ込み変数の有無も検査すべき
        if(lambda.Modifiers.Any(SyntaxKind.StaticKeyword)) {
            return true;
        }

        // 追加実装例: 変数の閉じ込みがないなら実質staticと見なす
        // 今は単純にfalse返す
        return false;
    }

    private static bool TryCreateSourceFragment(SemanticModel semanticModel,ExpressionSyntax sourceExpression,string elementTypeName,ref int loopNumber,out Enumerable fragment) {
        if(sourceExpression is InvocationExpressionSyntax sourceInvocation
            && semanticModel.GetSymbolInfo(sourceInvocation).Symbol is IMethodSymbol sourceMethod
            && sourceMethod.ReducedFrom is not null
            && Linqメソッド判定(sourceMethod)) {
            if(sourceInvocation.Expression is not MemberAccessExpressionSyntax { Expression: var nestedSourceExpression }) {
                fragment=null!;
                return false;
            }

            if(!TryCreateSourceFragment(semanticModel,nestedSourceExpression,GetSourceElementTypeName(sourceMethod),ref loopNumber,out var nestedSourceFragment)) {
                fragment=null!;
                return false;
            }

            if(!TryCreateTemplateArguments(semanticModel,sourceMethod,sourceInvocation.ArgumentList.Arguments,ref loopNumber,out var templateArguments)) {
                fragment=null!;
                return false;
            }
            fragment=null!;
            return false;
        }

        fragment=CreateRootEnumerableFragment("source",elementTypeName,useEnumerableInterface: true,loopNumber++);
        return true;
    }

    private static bool TryCreateTemplateArguments(SemanticModel semanticModel,IMethodSymbol method,SeparatedSyntaxList<ArgumentSyntax> arguments,ref int loopNumber,out object[] templateArguments) {
        var parameters = method.Parameters;
        if(parameters.Length!=arguments.Count) {
            templateArguments=Array.Empty<object>();
            return false;
        }

        templateArguments = new object[parameters.Length];
        for(var index = 0; index<parameters.Length; index++) {
            var parameter = parameters[index];
            var argumentExpression = arguments[index].Expression;

            if(argumentExpression is LambdaExpressionSyntax lambda) {
                if(!TryCreateLambdaFuncFragment(lambda,parameter.Type,out var lambdaSpec)) {
                    return false;
                }

                var isEffectivelyStatic = lambda.Modifiers.Any(SyntaxKind.StaticKeyword) || IsEffectivelyStaticLambda(lambda);
                templateArguments[index] = isEffectivelyStatic
                    ? CreateStaticTemplateArgument(lambdaSpec)
                    : CreateParameterTemplateArgument(parameter.Name,lambdaSpec);
                continue;
            }

            templateArguments[index]=NormalizeExpressionText(argumentExpression.ToString());
        }

        return true;
    }

    private static List<string> ローカルメソッド未対応Enumerable取得31(Linq傍受対象型 target) {
        if(target.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax) {
            return [];
        }

        var semanticModel = target.SemanticModel.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
        return methodSyntax.Body?
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol)
            .Where(static method => method is not null)
            .Select(static method => method!.ReducedFrom ?? method)
            .Where(static method => method.ContainingType.ToDisplayString()=="System.Linq.Enumerable")
            .Where(static method => !Enumerableメソッドフラグメント対応判定32(method.Name))
            .Select(static method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static methodName => methodName,StringComparer.Ordinal)
            .ToList()
            ?? [];
    }

    private static bool Enumerableメソッドフラグメント対応判定32(string methodName)
        => methodName=="Any" || フラグメント候補一覧.ContainsKey(methodName);

    private static bool ローカルメソッドEnumerable呼び出し含む34(Linq傍受対象型 target) {
        if(target.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax) {
            return false;
        }

        var semanticModel = target.SemanticModel.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
        return methodSyntax.Body?
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol)
            .Where(static method => method is not null)
            .Select(static method => method!.ReducedFrom ?? method)
            .Any(static method => method.ContainingType.ToDisplayString()=="System.Linq.Enumerable")
            ?? false;
    }

    private static object CreateStaticTemplateArgument(LambdaSpec lambda) {
        return new LambdaFunc {
            Type=Func型解決13(lambda.ParameterTypeNames.Length),
            ReturnTypeName=lambda.ReturnTypeName,
            ParameterTypeNames=lambda.ParameterTypeNames,
            ParameterNames=lambda.ParameterNames,
            DefineLambda=$" => {lambda.Body};",
            CanGenerateMethod=true,
            CanInlineAssign=true,
            InlineBody=lambda.Body
        };
    }

    private static object CreateParameterTemplateArgument(string parameterName,LambdaSpec lambda) {
        return new ParameterFunc {
            ParameterName=parameterName,
            Type=Func型解決13(lambda.ParameterTypeNames.Length),
            ReturnTypeName=lambda.ReturnTypeName,
            ParameterTypeNames=lambda.ParameterTypeNames,
            ParameterNames=lambda.ParameterNames,
            CanGenerateMethod=false,
            CanInlineAssign=false,
        };
    }

    private static string GetSourceElementTypeName(IMethodSymbol method) {
        var definition = method.ReducedFrom ?? method;
        if(definition.Parameters.Length==0
            ||definition.Parameters[0].Type is not INamedTypeSymbol sourceType
            ||sourceType.TypeArguments.Length!=1) {
            throw new InvalidOperationException($"Could not determine source element type for '{method.ToDisplayString(完全修飾形式)}'.");
        }

        return sourceType.TypeArguments[0].ToDisplayString(完全修飾形式);
    }

    private static bool TryGetInvocationSourceElementTypeName(SemanticModel semanticModel,InvocationExpressionSyntax invocation,IMethodSymbol method,out string sourceElementTypeName) {
        sourceElementTypeName=string.Empty;
        var definition = method.ReducedFrom ?? method;
        if(definition.Parameters.Length>0
            && definition.Parameters[0].Type is INamedTypeSymbol parameterSourceType
            && IsIEnumerableType(parameterSourceType)
            && parameterSourceType.TypeArguments.Length==1
            && !ContainsTypeParameter(parameterSourceType.TypeArguments[0])) {
            sourceElementTypeName=parameterSourceType.TypeArguments[0].ToDisplayString(完全修飾形式);
            return true;
        }

        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return false;
        }

        if(!列挙要素型取得11(semanticModel.GetTypeInfo(memberAccess.Expression).Type,out var elementType,out _)) {
            return false;
        }

        sourceElementTypeName=elementType.ToDisplayString(完全修飾形式);
        return true;
    }

    private static string[] GetReferencedTypeParameterNames(SemanticModel semanticModel,InvocationExpressionSyntax invocation,IMethodSymbol method,System.Threading.CancellationToken cancellationToken) {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if(invocation.Expression is MemberAccessExpressionSyntax memberAccess) {
            CollectTypeParameterNames(semanticModel.GetTypeInfo(memberAccess.Expression,cancellationToken).Type,names,seen);
        }

        CollectTypeParameterNames(method.ReturnType,names,seen);
        foreach(var parameter in method.Parameters) {
            CollectTypeParameterNames(parameter.Type,names,seen);
        }

        return names.ToArray();
    }

    private static void CollectTypeParameterNames(ITypeSymbol? type,List<string> names,HashSet<string> seen) {
        switch(type) {
            case null:
                return;
            case ITypeParameterSymbol typeParameter:
                if(seen.Add(typeParameter.Name)) {
                    names.Add(typeParameter.Name);
                }
                return;
            case IArrayTypeSymbol arrayType:
                CollectTypeParameterNames(arrayType.ElementType,names,seen);
                return;
            case IPointerTypeSymbol pointerType:
                CollectTypeParameterNames(pointerType.PointedAtType,names,seen);
                return;
            case INamedTypeSymbol namedType:
                foreach(var typeArgument in namedType.TypeArguments) {
                    CollectTypeParameterNames(typeArgument,names,seen);
                }
                return;
        }
    }

    private static Enumerable CreateRootEnumerableFragment(string sourceExpression,string ElementType,bool useEnumerableInterface,int 番号) {
        return new Root(
            番号,
            sourceExpression,
            ElementType,
            useEnumerableInterface);
    }

    private static bool TryCreateLambdaFuncFragment(LambdaExpressionSyntax lambda,ITypeSymbol delegateType,out LambdaSpec fragment) {
        fragment=null!;
        if(!TryGetLambdaExpressionBody(lambda,out var body)) {
            return false;
        }

        var parameters = GetLambdaParameters(lambda);
        if(delegateType is not INamedTypeSymbol funcType
            ||!IsInlineDelegateType(funcType)) {
            return false;
        }

        if(funcType.TypeArguments.Any(ContainsAnonymousType)) {
            return false;
        }

        var typeArguments = funcType.TypeArguments.Select(typeArgument => typeArgument.ToDisplayString(完全修飾形式)).ToArray();
        var parameterTypeNames = typeArguments.Take(typeArguments.Length-1).ToArray();
        if(parameters.Count!=parameterTypeNames.Length) {
            return false;
        }

        fragment=new LambdaSpec(
            typeArguments[^1],
            parameterTypeNames,
            parameters.ToArray(),
            NormalizeExpressionText(body.ToString()));
        return true;
    }

    private static bool IsInlineDelegateType(ITypeSymbol type)
        => type is INamedTypeSymbol namedType
            && IsInlineDelegateType(namedType);

    private static bool IsInlineDelegateType(INamedTypeSymbol namedType)
        => namedType.ContainingNamespace.ToDisplayString()=="System"
            && namedType.MetadataName is "Func`2" or "Func`3"
            && !IsSecondarySequenceType(namedType.TypeArguments[^1]);

    private static bool IsIEnumerableType(INamedTypeSymbol namedType)
        => namedType.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"
            && namedType.MetadataName=="IEnumerable`1";

    private static bool IsSecondarySequenceType(ITypeSymbol type)
        => type is INamedTypeSymbol namedType
            && IsIEnumerableType(namedType);

    private static bool ContainsAnonymousType(ITypeSymbol type) {
        if(type.IsAnonymousType) {
            return true;
        }

        return type switch {
            IArrayTypeSymbol arrayType => ContainsAnonymousType(arrayType.ElementType),
            INamedTypeSymbol namedType => namedType.TypeArguments.Any(ContainsAnonymousType),
            _ => false,
        };
    }

    private static bool ContainsTypeParameter(ITypeSymbol type) {
        return type switch {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol arrayType => ContainsTypeParameter(arrayType.ElementType),
            INamedTypeSymbol namedType => namedType.TypeArguments.Any(ContainsTypeParameter),
            IPointerTypeSymbol pointerType => ContainsTypeParameter(pointerType.PointedAtType),
            _ => false,
        };
    }

    private static bool ContainsGeneratedTypeAccessibilityRisk(ITypeSymbol type) {
        return type switch {
            IArrayTypeSymbol arrayType => ContainsGeneratedTypeAccessibilityRisk(arrayType.ElementType),
            IPointerTypeSymbol pointerType => ContainsGeneratedTypeAccessibilityRisk(pointerType.PointedAtType),
            INamedTypeSymbol namedType => NamedTypeAccessibilityRisk(namedType) || namedType.TypeArguments.Any(ContainsGeneratedTypeAccessibilityRisk),
            _ => false,
        };
    }

    private static bool NamedTypeAccessibilityRisk(INamedTypeSymbol namedType) {
        if(namedType.IsAnonymousType) {
            return true;
        }

        if(namedType.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal or Accessibility.ProtectedOrInternal) {
            return true;
        }

        return namedType.ContainingType is not null && NamedTypeAccessibilityRisk(namedType.ContainingType);
    }

    private static string GetLambdaBodyText(LambdaExpressionSyntax lambda) {
        return TryGetLambdaExpressionBody(lambda,out var body)
            ? NormalizeExpressionText(body.ToString())
            : string.Empty;
    }

    private static bool TryGetLambdaExpressionBody(LambdaExpressionSyntax lambda,out ExpressionSyntax body) {
        body=null!;
        if(lambda.Body is ExpressionSyntax expressionBody) {
            body=expressionBody;
            return true;
        }
        if(lambda.Body is BlockSyntax {
            Statements: [
                ReturnStatementSyntax { Expression: { } returnExpression }
            ]
        }) {
            body=returnExpression;
            return true;
        }
        return false;
    }

    private static List<string> GetLambdaParameters(LambdaExpressionSyntax lambda) {
        if(lambda is SimpleLambdaExpressionSyntax simpleLambda) {
            return [simpleLambda.Parameter.Identifier.ValueText];
        }
        if(lambda is ParenthesizedLambdaExpressionSyntax parenthesizedLambda) {
            return parenthesizedLambda.ParameterList.Parameters.Select(static parameter => parameter.Identifier.ValueText).ToList();
        }
        return [];
    }

    private static string NormalizeExpressionText(string value) {
        return value.Replace("\r\n"," ").Replace('\n',' ').Trim();
    }

    private static string RenderArgumentForGeneratedMethod(ArgumentSyntax argument,SemanticModel semanticModel,INamedTypeSymbol containingType) {
        var prefix = RefKind接頭辞取得35(argument.RefKindKeyword.Kind());
        return $"{prefix}{RenderExpressionForGeneratedMethod(argument.Expression,semanticModel,containingType)}";
    }

    private static string RefKind接頭辞取得35(RefKind refKind) => refKind switch {
        RefKind.Ref => "ref ",
        RefKind.Out => "out ",
        RefKind.In => "in ",
        _ => string.Empty,
    };

    private static string RefKind接頭辞取得35(SyntaxKind syntaxKind) => syntaxKind switch {
        SyntaxKind.RefKeyword => "ref ",
        SyntaxKind.OutKeyword => "out ",
        SyntaxKind.InKeyword => "in ",
        _ => string.Empty,
    };

    private static string RenderExpressionForGeneratedMethod(ExpressionSyntax expression,SemanticModel semanticModel,INamedTypeSymbol containingType) {
        var text = NormalizeExpressionText(expression.ToString())
            .Replace("SystemEnumerable.","global::System.Linq.Enumerable.",StringComparison.Ordinal)
            .Replace("this.","@this.",StringComparison.Ordinal);

        foreach(var invocation in expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()) {
            if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                || !method.IsStatic
                || !SymbolEqualityComparer.Default.Equals(method.ContainingType,containingType)) {
                continue;
            }

            var invocationText = NormalizeExpressionText(invocation.ToString());
            if(TryRenderSimpleLocalMethodInvocationExpression(method,semanticModel,containingType,out var inlineInvocationText)) {
                text=text.Replace(invocationText,inlineInvocationText,StringComparison.Ordinal);
                continue;
            }

            if(method.DeclaredAccessibility!=Accessibility.Public
                && !method.IsGenericMethod) {
                text=text.Replace(invocationText,RenderStaticLocalMethodForwardExpression(method,invocation,semanticModel,containingType),StringComparison.Ordinal);
                continue;
            }

            var qualifiedInvocationText = $"{method.ContainingType.ToDisplayString(完全修飾形式)}.{method.Name}({string.Join(", ",invocation.ArgumentList.Arguments.Select(static argument => NormalizeExpressionText(argument.ToString())))} )";
            qualifiedInvocationText=qualifiedInvocationText.Replace(" )",")",StringComparison.Ordinal);
            text=text.Replace(invocationText,qualifiedInvocationText,StringComparison.Ordinal);
        }

        foreach(var memberAccess in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()) {
            if(semanticModel.GetSymbolInfo(memberAccess).Symbol is not IMethodSymbol method
                || method.MethodKind!=MethodKind.Ordinary
                || method.DeclaredAccessibility==Accessibility.Public
                || semanticModel.GetTypeInfo(memberAccess).ConvertedType is not INamedTypeSymbol delegateType
                || !IsInlineDelegateType(delegateType)) {
                continue;
            }

            var memberText = NormalizeExpressionText(memberAccess.ToString())
                .Replace("this.","@this.",StringComparison.Ordinal);
            var receiver = method.IsStatic
                ? null
                : NormalizeExpressionText(memberAccess.Expression.ToString()).Replace("this","@this",StringComparison.Ordinal);
            text=text.Replace(
                memberText,
                RenderDelegateCreateExpression(method,delegateType,receiver),
                StringComparison.Ordinal);
        }

        return text;
    }

    private static bool TryRenderSimpleLocalMethodInvocationExpression(IMethodSymbol method,SemanticModel semanticModel,INamedTypeSymbol containingType,out string expressionText) {
        expressionText=string.Empty;

        if(!method.IsStatic
            || method.Parameters.Length!=0
            || method.DeclaredAccessibility!=Accessibility.Private
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType,containingType)
            || method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax) {
            return false;
        }

        if(methodSyntax.ExpressionBody is { Expression: var expressionBody }
            && IsSimpleInlineableLocalMethodExpression(expressionBody)) {
            expressionText=RenderExpressionForGeneratedMethod(expressionBody,semanticModel,containingType);
            return true;
        }

        if(methodSyntax.Body is {
            Statements: [
                ReturnStatementSyntax { Expression: { } returnExpression }
            ]
        } && IsSimpleInlineableLocalMethodExpression(returnExpression)) {
            expressionText=RenderExpressionForGeneratedMethod(returnExpression,semanticModel,containingType);
            return true;
        }

        return false;
    }

    private static string RenderStaticLocalMethodForwardExpression(IMethodSymbol method,InvocationExpressionSyntax invocation,SemanticModel semanticModel,INamedTypeSymbol containingType) {
        var bindingFlags = "global::System.Reflection.BindingFlags.Static | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic";
        var candidates = $"typeof({method.ContainingType.ToDisplayString(完全修飾形式)}).GetMethods({bindingFlags})";
        var methodSelector = $"global::System.Linq.Enumerable.Single({candidates}, static candidate => candidate.Name==\"{method.Name}\" && candidate.GetParameters().Length=={method.Parameters.Length} && candidate.GetGenericArguments().Length==0)";
        var arguments = invocation.ArgumentList.Arguments.Count==0
            ? "global::System.Array.Empty<object?>()"
            : $"new object?[] {{ {string.Join(", ",invocation.ArgumentList.Arguments.Select(argument => $"(object?)({RenderArgumentForGeneratedMethod(argument,semanticModel,containingType)})"))} }}";
        return $"(({method.ReturnType.ToDisplayString(完全修飾形式)}){methodSelector}.Invoke(null, {arguments})!)";
    }

    private static string RenderDelegateCreateExpression(IMethodSymbol method,INamedTypeSymbol delegateType,string? receiver) {
        var delegateTypeName = delegateType.ToDisplayString(完全修飾形式);
        var declaringType = method.ContainingType.ToDisplayString(完全修飾形式);
        var bindingFlags = method.IsStatic
            ? "global::System.Reflection.BindingFlags.Static | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic"
            : "global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic";
        var parameterTypes = method.Parameters.Length==0
            ? "global::System.Type.EmptyTypes"
            : $"new global::System.Type[] {{ {string.Join(", ",method.Parameters.Select(static parameter => $"typeof({parameter.Type.ToDisplayString(完全修飾形式)})"))} }}";
        var methodInfo = $"typeof({declaringType}).GetMethod(\"{method.Name}\", {bindingFlags}, null, {parameterTypes}, null)!";
        return method.IsStatic
            ? $"({delegateTypeName})global::System.Delegate.CreateDelegate(typeof({delegateTypeName}), {methodInfo})"
            : $"({delegateTypeName})global::System.Delegate.CreateDelegate(typeof({delegateTypeName}), {receiver}, {methodInfo})";
    }

    private static bool TryRenderSimpleLocalMethodBody(Linq傍受対象型 target,out string body) {
        body=string.Empty;

        if(!target.Method.IsStatic
            || target.Method.DeclaredAccessibility!=Accessibility.Private
            || !SymbolEqualityComparer.Default.Equals(target.Method.ContainingType,target.EnclosingType)
            || target.Parameters.Length!=0) {
            return false;
        }

        if(target.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax) {
            return false;
        }

        if(methodSyntax.ParameterList.Parameters.Count!=target.Parameters.Length) {
            return false;
        }

        if(methodSyntax.ExpressionBody is { Expression: var expressionBody }
            && IsSimpleInlineableLocalMethodExpression(expressionBody)) {
            var renderedExpression = RenderExpressionForGeneratedMethod(expressionBody,target.SemanticModel,target.EnclosingType);
            body=target.Method.ReturnsVoid
                ? $"{renderedExpression};"
                : $"return {renderedExpression};";
            return true;
        }

        if(methodSyntax.Body is {
            Statements: [
                ReturnStatementSyntax { Expression: { } returnExpression }
            ]
        } && IsSimpleInlineableLocalMethodExpression(returnExpression)) {
            body=$"return {RenderExpressionForGeneratedMethod(returnExpression,target.SemanticModel,target.EnclosingType)};";
            return true;
        }

        if(target.Method.ReturnsVoid
            && methodSyntax.Body is {
                Statements: [
                    ExpressionStatementSyntax expressionStatement
                ]
            }
            && IsSimpleInlineableLocalMethodExpression(expressionStatement.Expression)) {
            body=$"{RenderExpressionForGeneratedMethod(expressionStatement.Expression,target.SemanticModel,target.EnclosingType)};";
            return true;
        }

        return false;
    }

    private static bool IsSimpleInlineableLocalMethodExpression(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax { Expression: var innerExpression }) {
            expression=innerExpression;
        }

        return expression switch {
            CollectionExpressionSyntax => true,
            ArrayCreationExpressionSyntax => true,
            ImplicitArrayCreationExpressionSyntax => true,
            LiteralExpressionSyntax => true,
            DefaultExpressionSyntax => true,
            _ => false,
        };
    }

    private static string RenderLocalMethodForwardBody(Linq傍受対象型 target) {
        var builder = new StringBuilder();
        var bindingFlags = target.Method.IsStatic
            ? "global::System.Reflection.BindingFlags.Static | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic"
            : "global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic";

        builder.AppendLine($"var __declaringType = typeof({target.Method.ContainingType.ToDisplayString(完全修飾形式)});");
        builder.AppendLine($"var __candidates = __declaringType.GetMethods({bindingFlags});");
        builder.AppendLine($"var __method = global::System.Linq.Enumerable.Single(__candidates,static candidate => candidate.Name==\"{target.Method.Name}\" && candidate.GetParameters().Length=={target.Parameters.Length} && candidate.GetGenericArguments().Length=={target.Method.TypeArguments.Length});");

        if(target.Method.TypeArguments.Length>0) {
            builder.AppendLine($"__method = __method.MakeGenericMethod({string.Join(", ",target.Method.TypeArguments.Select(static type => $"typeof({type.ToDisplayString(完全修飾形式)})"))});");
        }

        var instanceTarget = target.Method.IsStatic ? "null" : "@this";
        var arguments = target.Parameters.Length==0
            ? "global::System.Array.Empty<object?>()"
            : $"new object?[] {{ {string.Join(", ",target.Parameters.Select(static parameter => parameter.Name))} }}";

        if(target.Method.ReturnsVoid) {
            builder.AppendLine($"__method.Invoke({instanceTarget},{arguments});");
            return builder.ToString();
        }

        builder.AppendLine($"var __result = __method.Invoke({instanceTarget},{arguments});");
        builder.AppendLine($"return ({target.ReturnTypeName})__result!;");
        return builder.ToString();
    }

    private static string RenderUnsupportedLocalMethodBody(IReadOnlyList<string> unsupportedMethods) {
        var methodList = string.Join(", ",unsupportedMethods);
        return $"throw new global::System.InvalidOperationException(\"Local method interception does not support System.Linq.Enumerable methods without Fragment implementations: {methodList}.\");";
    }

    private static string RenderUninlineableLocalMethodBody(Linq傍受対象型 target) {
        var methodName = target.Method.ToDisplayString(完全修飾形式)
            .Replace("\\","\\\\",StringComparison.Ordinal)
            .Replace("\"","\\\"",StringComparison.Ordinal);
        return $"throw new global::System.InvalidOperationException(\"Local method interception could not inline '{methodName}'. Fallback forwarding is disabled for methods that contain System.Linq.Enumerable calls.\");";
    }

    private static bool TryRenderCarrierDirectMethodBody(Linq傍受対象型 target,out string body) {
        body=string.Empty;
        if(target.Method.ReducedFrom is null) {
            return false;
        }

        return target.Method.ReducedFrom.Name switch {
            "Select" => TryRenderSelectCarrierBody(target,out body),
            "Where" => TryRenderWhereCarrierBody(target,out body),
            "Take" => TryRenderTakeCarrierBody(target,out body),
            _ => false,
        };
    }

    private static bool TryRenderSelectCarrierBody(Linq傍受対象型 target,out string body) {
        body=string.Empty;
        if(!列挙要素型取得11(target.Method.ReturnType,out var returnElementType,out var _)) {
            return false;
        }

        var sourceType = target.SourceElementTypeName;
        var resultType = returnElementType.ToDisplayString(完全修飾形式);
        var selectorIsIndex = target.Method.Parameters.Length>0
            && target.Method.Parameters[0].Type is INamedTypeSymbol funcType
            && funcType.TypeArguments.Length==3;

        var builder = new StringBuilder();
        if(selectorIsIndex) {
            builder.AppendLine($"if(source is __SelectCarrier<{sourceType}> __selectCarrier0) return new __SelectCarrier<{resultType}>(__selectCarrier0.RawSource, (raw, rawIndex) => selector(__selectCarrier0.Select(raw, rawIndex), rawIndex));");
            builder.AppendLine($"if(source is __WhereCarrier<{sourceType}> __whereCarrier0) return new __WhereSelectIndexCarrier<{sourceType}, {resultType}>(__whereCarrier0, selector);");
            builder.AppendLine($"return new __SelectCarrier<{resultType}>(source, (raw, rawIndex) => selector(({sourceType})raw!, rawIndex));");
        } else {
            builder.AppendLine($"if(source is __SelectCarrier<{sourceType}> __selectCarrier0) return new __SelectCarrier<{resultType}>(__selectCarrier0.RawSource, (raw, rawIndex) => selector(__selectCarrier0.Select(raw, rawIndex)));");
            builder.AppendLine($"if(source is __WhereCarrier<{sourceType}> __whereCarrier0) return new __WhereCarrier<{resultType}>(__whereCarrier0.RawSource, __whereCarrier0.Predicate, (raw, rawIndex) => selector(__whereCarrier0.Select(raw, rawIndex)));");
            builder.AppendLine($"if(source is __TakeCarrier<{sourceType}> __takeCarrier0) return new __TakeSelectCarrier<{sourceType}, {resultType}>(__takeCarrier0.Source, __takeCarrier0.Count, selector);");
            builder.AppendLine($"if(source is __SelectTakeCarrier<{sourceType}> __selectTakeCarrier0) return new __SelectTakeSelectCarrier<{sourceType}, {resultType}>(__selectTakeCarrier0.RawSource, __selectTakeCarrier0.Select, __selectTakeCarrier0.Count, selector);");
            builder.AppendLine($"return new __SelectCarrier<{resultType}>(source, (raw, _) => selector(({sourceType})raw!));");
        }

        body=builder.ToString().TrimEnd();
        return true;
    }

    private static bool TryRenderWhereCarrierBody(Linq傍受対象型 target,out string body) {
        body=string.Empty;
        var sourceType = target.SourceElementTypeName;
        var predicateIsIndex = target.Method.Parameters.Length>0
            && target.Method.Parameters[0].Type is INamedTypeSymbol funcType
            && funcType.TypeArguments.Length==3;

        var builder = new StringBuilder();
        if(predicateIsIndex) {
            builder.AppendLine($"if(source is __SelectCarrier<{sourceType}> __selectCarrier0) return new __WhereCarrier<{sourceType}>(__selectCarrier0.RawSource, (raw, rawIndex) => predicate(__selectCarrier0.Select(raw, rawIndex), rawIndex), (raw, rawIndex) => __selectCarrier0.Select(raw, rawIndex));");
            builder.AppendLine($"if(source is __WhereCarrier<{sourceType}> __whereCarrier0) return new __WhereWhereIndexCarrier<{sourceType}>(__whereCarrier0, predicate);");
            builder.AppendLine($"return new __WhereCarrier<{sourceType}>(source, (raw, rawIndex) => predicate(({sourceType})raw!, rawIndex), (raw, _) => ({sourceType})raw!);");
        } else {
            builder.AppendLine($"if(source is __SelectCarrier<{sourceType}> __selectCarrier0) return new __WhereCarrier<{sourceType}>(__selectCarrier0.RawSource, (raw, rawIndex) => predicate(__selectCarrier0.Select(raw, rawIndex)), (raw, rawIndex) => __selectCarrier0.Select(raw, rawIndex));");
            builder.AppendLine($"if(source is __WhereCarrier<{sourceType}> __whereCarrier0) return new __WhereCarrier<{sourceType}>(__whereCarrier0.RawSource, (raw, rawIndex) => __whereCarrier0.Predicate(raw, rawIndex) && predicate(__whereCarrier0.Select(raw, rawIndex)), (raw, rawIndex) => __whereCarrier0.Select(raw, rawIndex));");
            builder.AppendLine($"return new __WhereCarrier<{sourceType}>(source, (raw, _) => predicate(({sourceType})raw!), (raw, _) => ({sourceType})raw!);");
        }

        body=builder.ToString().TrimEnd();
        return true;
    }

    private static bool TryRenderTakeCarrierBody(Linq傍受対象型 target,out string body) {
        body=string.Empty;
        if(target.Method.Parameters.Length!=1
            || target.Method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
            return false;
        }

        var sourceType = target.SourceElementTypeName;
        body=$"""
            if(source is __SelectCarrier<{sourceType}> __selectCarrier0) return new __SelectTakeCarrier<{sourceType}>(__selectCarrier0.RawSource, __selectCarrier0.Select, count);
            return new __TakeCarrier<{sourceType}>(source, count);
            """;
        return true;
    }

    private static string RenderUnsupportedDirectMethodBody(Linq傍受対象型 target,string? detail = null) {
        var methodName = target.Method.ReducedFrom?.ToDisplayString(完全修飾形式) ?? target.Method.ToDisplayString(完全修飾形式);
        var message = $"Direct interception could not inline '{methodName}' inside '{target.ContainingMethodName}'.";
        if(!string.IsNullOrWhiteSpace(detail)) {
            message=$"{message} {detail}";
        }

        message=message
            .Replace("\\","\\\\",StringComparison.Ordinal)
            .Replace("\"","\\\"",StringComparison.Ordinal);
        return $"throw new global::System.InvalidOperationException(\"{message}\");";
    }

    private static string RenderCarrierHelperTypes() => """
        private sealed class __SelectCarrier<T> : global::System.Collections.Generic.IEnumerable<T> {
            public __SelectCarrier(global::System.Collections.IEnumerable rawSource, global::System.Func<object?, global::System.Int32, T> select) { this.RawSource=rawSource; this.Select=select; }
            public global::System.Collections.IEnumerable RawSource { get; }
            public global::System.Func<object?, global::System.Int32, T> Select { get; }
            public global::System.Collections.Generic.IEnumerator<T> GetEnumerator() { var rawIndex=0; foreach(var raw in this.RawSource) yield return this.Select(raw,rawIndex++); }
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        private sealed class __WhereCarrier<T> : global::System.Collections.Generic.IEnumerable<T> {
            public __WhereCarrier(global::System.Collections.IEnumerable rawSource, global::System.Func<object?, global::System.Int32, global::System.Boolean> predicate, global::System.Func<object?, global::System.Int32, T> select) { this.RawSource=rawSource; this.Predicate=predicate; this.Select=select; }
            public global::System.Collections.IEnumerable RawSource { get; }
            public global::System.Func<object?, global::System.Int32, global::System.Boolean> Predicate { get; }
            public global::System.Func<object?, global::System.Int32, T> Select { get; }
            public global::System.Collections.Generic.IEnumerator<T> GetEnumerator() { var rawIndex=0; foreach(var raw in this.RawSource) { if(this.Predicate(raw,rawIndex)) yield return this.Select(raw,rawIndex); rawIndex++; } }
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        private sealed class __TakeCarrier<T> : global::System.Collections.Generic.IEnumerable<T> {
            public __TakeCarrier(global::System.Collections.Generic.IEnumerable<T> source, global::System.Int32 count) { this.Source=source; this.Count=count; }
            public global::System.Collections.Generic.IEnumerable<T> Source { get; }
            public global::System.Int32 Count { get; }
            public global::System.Collections.Generic.IEnumerator<T> GetEnumerator() { if(this.Count<=0) yield break; var taken=0; foreach(var item in this.Source) { yield return item; if(++taken>=this.Count) yield break; } }
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        private sealed class __SelectTakeCarrier<T> : global::System.Collections.Generic.IEnumerable<T> {
            public __SelectTakeCarrier(global::System.Collections.IEnumerable rawSource, global::System.Func<object?, global::System.Int32, T> select, global::System.Int32 count) { this.RawSource=rawSource; this.Select=select; this.Count=count; }
            public global::System.Collections.IEnumerable RawSource { get; }
            public global::System.Func<object?, global::System.Int32, T> Select { get; }
            public global::System.Int32 Count { get; }
            public global::System.Collections.Generic.IEnumerator<T> GetEnumerator() { if(this.Count<=0) yield break; var rawIndex=0; var taken=0; foreach(var raw in this.RawSource) { yield return this.Select(raw,rawIndex++); if(++taken>=this.Count) yield break; } }
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        private sealed class __WhereSelectIndexCarrier<TSource, TResult> : global::System.Collections.Generic.IEnumerable<TResult> {
            public __WhereSelectIndexCarrier(__WhereCarrier<TSource> source, global::System.Func<TSource, global::System.Int32, TResult> selector) { this.Source=source; this.Selector=selector; }
            public __WhereCarrier<TSource> Source { get; }
            public global::System.Func<TSource, global::System.Int32, TResult> Selector { get; }
            public global::System.Collections.Generic.IEnumerator<TResult> GetEnumerator() { var filteredIndex=0; var rawIndex=0; foreach(var raw in this.Source.RawSource) { if(!this.Source.Predicate(raw,rawIndex)) { rawIndex++; continue; } yield return this.Selector(this.Source.Select(raw,rawIndex),filteredIndex++); rawIndex++; } }
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        private sealed class __WhereWhereIndexCarrier<T> : global::System.Collections.Generic.IEnumerable<T> {
            public __WhereWhereIndexCarrier(__WhereCarrier<T> source, global::System.Func<T, global::System.Int32, global::System.Boolean> predicate) { this.Source=source; this.Predicate=predicate; }
            public __WhereCarrier<T> Source { get; }
            public global::System.Func<T, global::System.Int32, global::System.Boolean> Predicate { get; }
            public global::System.Collections.Generic.IEnumerator<T> GetEnumerator() { var filteredIndex=0; var rawIndex=0; foreach(var raw in this.Source.RawSource) { if(!this.Source.Predicate(raw,rawIndex)) { rawIndex++; continue; } var value=this.Source.Select(raw,rawIndex); if(this.Predicate(value,filteredIndex++)) yield return value; rawIndex++; } }
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        private sealed class __TakeSelectCarrier<TSource, TResult> : global::System.Collections.Generic.IEnumerable<TResult> {
            public __TakeSelectCarrier(global::System.Collections.Generic.IEnumerable<TSource> source, global::System.Int32 count, global::System.Func<TSource, TResult> selector) { this.Source=source; this.Count=count; this.Selector=selector; }
            public global::System.Collections.Generic.IEnumerable<TSource> Source { get; }
            public global::System.Int32 Count { get; }
            public global::System.Func<TSource, TResult> Selector { get; }
            public global::System.Collections.Generic.IEnumerator<TResult> GetEnumerator() { if(this.Count<=0) yield break; var taken=0; foreach(var item in this.Source) { yield return this.Selector(item); if(++taken>=this.Count) yield break; } }
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        private sealed class __SelectTakeSelectCarrier<TSource, TResult> : global::System.Collections.Generic.IEnumerable<TResult> {
            public __SelectTakeSelectCarrier(global::System.Collections.IEnumerable rawSource, global::System.Func<object?, global::System.Int32, TSource> first, global::System.Int32 count, global::System.Func<TSource, TResult> second) { this.RawSource=rawSource; this.First=first; this.Count=count; this.Second=second; }
            public global::System.Collections.IEnumerable RawSource { get; }
            public global::System.Func<object?, global::System.Int32, TSource> First { get; }
            public global::System.Int32 Count { get; }
            public global::System.Func<TSource, TResult> Second { get; }
            public global::System.Collections.Generic.IEnumerator<TResult> GetEnumerator() { if(this.Count<=0) yield break; var rawIndex=0; var taken=0; foreach(var raw in this.RawSource) { yield return this.Second(this.First(raw,rawIndex++)); if(++taken>=this.Count) yield break; } }
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        """;

    private static void AppendBlock(StringBuilder builder,string value) {
        if(string.IsNullOrWhiteSpace(value)) {
            return;
        }
        if(builder.Length>0 && builder[^1]!='\n') {
            builder.AppendLine();
        }
        builder.AppendLine(value.Trim());
    }

    private sealed class Linq傍受対象型 {
        public Linq傍受対象型(string identifier,string containingMethodName,string interceptsLocationAttributeText,IMethodSymbol method,IMethodSymbol templateMethod,InvocationExpressionSyntax invocation,INamedTypeSymbol enclosingType,SemanticModel semanticModel,InterceptParameterSpec[] parameters,string? sourceElementTypeName = null,string[]? methodTypeParameterNames = null,bool isLocalMethodInterception = false) {
            this.Identifier=identifier;
            this.ContainingMethodName=containingMethodName;
            this.InterceptsLocationAttributeText=interceptsLocationAttributeText;
            this.Method=method;
            this.TemplateMethod=templateMethod;
            this.Invocation=invocation;
            this.EnclosingType=enclosingType;
            this.SemanticModel=semanticModel;
            this.Parameters=parameters;
            this.ExplicitSourceElementTypeName=sourceElementTypeName;
            this.MethodTypeParameters=methodTypeParameterNames ?? Array.Empty<string>();
            this.IsLocalMethodInterception=isLocalMethodInterception;
        }

        public string Identifier { get; }
        public string ContainingMethodName { get; }
        public string InterceptsLocationAttributeText { get; }
        public IMethodSymbol Method { get; }
        public IMethodSymbol TemplateMethod { get; }
        public InvocationExpressionSyntax Invocation { get; }
        public INamedTypeSymbol EnclosingType { get; }
        public SemanticModel SemanticModel { get; }
        public InterceptParameterSpec[] Parameters { get; }
        public string? ExplicitSourceElementTypeName { get; }
        public string[] MethodTypeParameters { get; }
        public bool IsLocalMethodInterception { get; }
        public string SourceElementTypeName {
            get {
                if(!string.IsNullOrWhiteSpace(this.ExplicitSourceElementTypeName)) {
                    return this.ExplicitSourceElementTypeName;
                }
                if(this.Method.TypeArguments.Length>0) {
                    return this.Method.TypeArguments[0].ToDisplayString(完全修飾形式);
                }
                var parameters = this.TemplateMethod.Parameters;
                if(parameters.Length==0
                    ||parameters[0].Type is not INamedTypeSymbol sourceType
                    ||sourceType.TypeArguments.Length!=1) {
                    throw new InvalidOperationException($"Could not determine source element type for '{this.TemplateMethod.ToDisplayString(完全修飾形式)}'.");
                }
                return sourceType.TypeArguments[0].ToDisplayString(完全修飾形式);
            }
        }
        public string ReturnTypeName => this.Method.ReturnsVoid ? "void" : this.Method.ReturnType.ToDisplayString(完全修飾形式);
        public string InterceptorParameterList {
            get {
                if(this.IsLocalMethodInterception) {
                    var parameters = new List<string>();
                    if(!this.Method.IsStatic) {
                        parameters.Add($"this {this.Method.ContainingType.ToDisplayString(完全修飾形式)} @this");
                    }
                    parameters.AddRange(this.Parameters.Select(static parameter => parameter.Declaration));
                    return string.Join(", ",parameters);
                }

                return $"this global::System.Collections.Generic.IEnumerable<{this.SourceElementTypeName}> source{this.MethodParameterList}";
            }
        }
        public string MethodParameterList => this.Parameters.Length==0
            ? string.Empty
            : ", "+string.Join(", ",this.Parameters.Select(static parameter => parameter.Declaration));
        public string MethodTypeParameterList => this.MethodTypeParameters.Length==0
            ? string.Empty
            : "<"+string.Join(", ",this.MethodTypeParameters)+">";
        public string MethodName => $"OptimizedLinq_{this.Identifier}";
    }
    private sealed class InterceptParameterSpec {
        public InterceptParameterSpec(string name,string typeName,RefKind refKind = RefKind.None,LambdaSpec? lambda = null,bool lambdaIsStatic = false, LambdaExpressionSyntax? lambdaSyntax = null) {
            this.Name=name;
            this.TypeName=typeName;
            this.RefKind=refKind;
            this.Lambda=lambda;
            this.LambdaIsStatic=lambdaIsStatic;
            this.LambdaSyntax=lambdaSyntax;
        }

        public string Name { get; }
        public string TypeName { get; }
        public RefKind RefKind { get; }
        public LambdaSpec? Lambda { get; }

        // 追加: staticかどうか判定を保存
        public bool LambdaIsStatic { get; }

        // 追加: ラムダ構文の保存
        public LambdaExpressionSyntax? LambdaSyntax { get; }

        public string Declaration => $"{this.RefKindModifier}{this.TypeName} {this.Name}";

        private string RefKindModifier => this.RefKind switch {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            _ => string.Empty,
        };

        public object TemplateArgument => this.Lambda is null
            ? this.Name
            : this.LambdaIsStatic
                ? new LambdaFunc {
                    Type=Func型解決13(this.Lambda.ParameterTypeNames.Length),
                    ReturnTypeName=this.Lambda.ReturnTypeName,
                    ParameterTypeNames=this.Lambda.ParameterTypeNames,
                    ParameterNames=this.Lambda.ParameterNames,
                    DefineLambda=$" => {this.Lambda.Body};",
                    CanGenerateMethod=true,
                    CanInlineAssign=true,
                    InlineBody=this.Lambda.Body
                }
                : new ParameterFunc {
                    ParameterName=this.Name,
                    ReturnTypeName=this.Lambda.ReturnTypeName,
                    ParameterTypeNames=this.Lambda.ParameterTypeNames,
                    ParameterNames=this.Lambda.ParameterNames,
                };
    }
    private sealed class LambdaSpec {
        public LambdaSpec(string returnTypeName,string[] parameterTypeNames,string[] parameterNames,string body) {
            this.ReturnTypeName=returnTypeName;
            this.ParameterTypeNames=parameterTypeNames;
            this.ParameterNames=parameterNames;
            this.Body=body;
        }
        public string ReturnTypeName { get; }
        public string[] ParameterTypeNames { get; }
        public string[] ParameterNames { get; }
        public string Body { get; }
    }
    private sealed class LinqInterceptTargetComparer : IEqualityComparer<Linq傍受対象型> {
        public static readonly LinqInterceptTargetComparer Instance = new();
        public bool Equals(Linq傍受対象型? x,Linq傍受対象型? y) {
            if(ReferenceEquals(x,y)) {
                return true;
            }
            if(x is null || y is null) {
                return false;
            }
            return x.InterceptsLocationAttributeText==y.InterceptsLocationAttributeText;
        }

        public int GetHashCode(Linq傍受対象型 obj) {
            return obj.InterceptsLocationAttributeText.GetHashCode(StringComparison.Ordinal);
        }
    }
    private sealed class 型対象型 {
        public 型対象型(INamedTypeSymbol symbol,bool isPartial) {
            this.Symbol=symbol;
            this.IsPartial=isPartial;
        }
        public INamedTypeSymbol Symbol { get; }
        public bool IsPartial { get; }
    }
}
