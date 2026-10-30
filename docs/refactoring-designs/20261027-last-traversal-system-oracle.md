# Independent Last traversal fixture design

Decision: 製品変更前に System 実行を oracle にする外部観測 fixture を追加する。既存pureのscope期待を意味の正解へ転記しない。製品修正・Last全体のrefactorは本納品に含めない。

## 候補

`newfixture/Last.cs.txt` を、root source X が取得できた時に `Test/SourceGenerator/LastTraversal/Last.cs` へ取り込む候補とする。既存 Test.csproj の通常のCompile glob下なので新project・共有projectは追加しない。取込前に親担当が当該ディレクトリの追加指示を確認する。

5 source shapes:

1. int[]を返すarray factory。
2. List<int>を返すfactory。
3. IEnumerable<int>を返すcustom iterator(factory呼出しとyield開始を分離)。
4. IEnumerable<int>型を返しruntime実体がarray。
5. IEnumerable<int>型を返しruntime実体がList。

各shapeでAnchor有/無を独立generation(10 compilation)。Last(predicate)、LastOrDefault(predicate)、nested Last、nested LastOrDefault、Where.Last、Where.LastOrDefault、Where.Select.Last、nested Last のinnerをTargetで照合する版、nested LastOrDefault のinnerをTargetで照合する版の9query。最後の2版ではno-match入力のinner無matchも独立に検証する。All / None / CounterOnly / System の4実装を同じソースへ置き、公開Runから各private属性付きqueryを呼ぶ。query毎に個別interceptor名を検査し、System/Systemに空振りする試験を防ぐ。

8 inputs: empty、single match、single no-match、tail match、duplicates、negative、no-match、head-only-match。5 throw modes: none、outer head、outer tail、inner head、inner tail。nested はouter Hitとinner Hitを分ける。末尾match時に先頭がthrowし得る入力を独立して持ち、末尾throwと逆走査のtrace差も観測する。

計画上の比較セル数: 5 shapes *2 Anchor *9queries *8inputs *5throw modes *3実装=10,800。System実行を毎セルの基準として別に採る。これは計画数であり実行済み数ではない。

## 判定の分離

- Value / ExceptionType / ExceptionMessage: 各列をSystemと比較。nomatchのmessage差を評価順不一致と混同しない。
- trace: `H:tag:value`=body Hit、`F:tag`=source式factory取得、`E:tag:index`=iteratorのyield、`D:tag`=Dispose/finally。nested inner tagにはouter pを含め、別のinner評価を集計上混ぜない。
- nonAnchor All: token毎の回数がSystem以下。総数だけの上限比較は禁止。正常値・例外は別に一致が必要。
- Anchor All / None / CounterOnly: traceを厳密比較。Noneが意味の正解ではなくSystemを正解にする。
- outer source式は各queryで1回。inner source式は到達したouter bodyごとに取得し、Systemの同じeventを上回らない。Anchor/None/CounterOnlyはsource eventも含め厳密順序。
- LogicalDelegate / PhysicalDelegate: 記録のみ。throw前後のcounter加算時機は意味判定に混ぜない。
- EnumerableFallback: 独立して0を要求。generation errorsとInterceptionMissはruntime意味不一致へ置換しない。
- source、generated、diagnostics、observations、source/generated/generator hash、runtime versionとSystem.Linq assembly名を保存。

## 親担当でのbefore実行

1. renderer333source build/paired loaderの完了と停止証拠を確認し、root source S freezeを解除できる状態を確認する。
2. parentが既存BGO手順でsource Xを取得して候補を取込み、owner/path/hashを記録する。
3. source S と対象build-dir Xを持つBGO runの中で、Release/net10.0の必要なビルド後、下記filterを実行する。現在の生成器DLLと対象Test.dllの対応をhashで確認する。`--no-build`はその組が検証済みの場合だけ使う。

```powershell
# 既存のBGO run specの内側に置く。raw実行は本候補では未承認。
$env:SEAMLESS_LAST_TRAVERSAL_ARTIFACT_DIR='G:\GitHub\Seamless\Work\Orchestration\argon-last-traversal-audit-01\before-run\artifacts'
dotnet test Test/Test.csproj -c Release -f net10.0 -m:1 -p:UseSharedCompilation=false --disable-build-servers --no-restore --filter 'FullyQualifiedName~Test.SourceGenerator.LastTraversal.Last' --logger 'trx;LogFileName=last-traversal-before.trx' --results-directory 'G:\GitHub\Seamless\Work\Orchestration\argon-last-traversal-audit-01\before-run'
```

4. beforeの差をValue/Exception/UpperBound/Trace/Fallback/Interception/Compileに分類してから最小修正の設計へ進む。今回の候補を合格として納品しない。generator生成・compilation実行自体をこの担当では一切行っていない。

## Guardrail

旧pure記録・source snapshot・OriginalIdealBodyは無変更。既存20期待を2へ一括置換しない。counter scopeを変えて20を隠さない。全sourceを逆走査する一括修正はcustom IEnumerableのSystem順序を壊すため禁止。Systemより先にsource式やpredicateを評価しない。fallbackで成功扱いにしない。現在renderer333freeze/S/pairedloaderを壊さない。
