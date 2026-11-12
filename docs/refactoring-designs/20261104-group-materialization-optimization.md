# 返却groupの複数整列の固定費削減

## 背景

利用者から、groupを返す複数整列の固定費を改善し、同じ条件で性能とメモリを再比較し、他の改善点も調べて進める指示を受けた。[比較基準](20261104-ordered-join-comparison.md)の16条件、4,096/16,384行、Memory/Storage、worker上限1/4、各10反復を維持する。

## 現状の観察

- QueryGrouping.MaterializeはgroupごとにQueryResultBufferを生成する。そのconstructorはCodecRegistry.Forkと型/codec解決を毎回行う。予約/所有登録もgroupごとに増える。
- OwnedGroupingはIReadOnlyCollectionだけを実装し、標準LINQがCount/ToArrayのために使うICollectionの高速経路には入らない。
- 分割Lookupの内部groupは要素数を保持しているが、外側のgroupへその情報を公開していない。結果bufferは4要素から拡張する。
- 結果はLookup/query/取引終了後にも読める必要がある。未知の参照と独自codecの処理をworkerへ移さない。

## 過去設計から引き継ぐ仕様

DeferredMvccImplementation、上記比較報告、MV06/MV09/MV24/MV39/MV40の所有権・寿命・例外順序・予算・全worker終了条件を維持する。元のstatic Materialize APIも維持する。collectible型の解決cacheをDBの長寿命scopeやglobal cacheへ移さない。

## 目的

query内で再利用できるcodec解決とgroup構築状態を共有し、小さいgroup多数の固定費を減らす。候補ごとの観測を残し、有望な未検証候補を残したまま完了扱いにしない。

## 非目的

NUMA、保存形式変更、私有PersistedのDB用途化、callerが一つのgroupを保持しただけで全groupの大きい共通slabを保持させる変更、予算保証の省略は行わない。

## 提案設計

1. query-localなQueryGrouping.Materializerを一つ作り、private codec fork/解決と集約した予約を共有する。最初に所有groupを必要とした時だけ初期化する。返却groupはkeyと所有配列だけを持ち、Materializerを参照しない。構築途中の失敗では当該group分だけ予約を巻き戻す。
2. 実測後、読取り専用ICollection/IListの公開によるCount/ToArray高速化を検証する。変異APIは拒否する。
3. 内部の信頼できるcountを使う正確な配列確保、buffer拡張/余分な列挙/予約検査の固定費を調べる。独自codecや参照payloadは既存と同じ呼出し・計上を保つ。安全性の確認できない短絡は採用しない。
4. 残る時間/割当とコードを調べ、他の有望候補があれば同様に比較する。利用者の追加指示に従い、効果が小さく実質的に些末な案しか残らなければ停止する。効果なし・誤差範囲・意味/寿命/予算を弱める案は不採用理由を残す。将来の全ハードウェアで改善不能という数学的主張はしない。

## 影響範囲

Seamless.Linqのgroup materializationと内部Lookup、FLinqの生成接続、Execution/DBの回帰試験、比較用runner/報告。QueryResultBufferの一般APIやRoot CodecRegistryの寿命は変更しない方針。

## 移行手順

static APIを残して生成コードの所有groupだけを新Materializerへ接続する。既存の借用group集計や通常Array/Listの経路を巻き込まない。実装変更時には生成コメントも更新する。

## テスト計画

値/順序/keyと要素の参照同一性、空/少数/多数、custom codec、参照payload、source例外/取消/予算不足と巻戻し、scope終了後の列挙と解放、count/CopyToの意味を確認する。ExecutionとDBのDebug/Release回帰、同じ16条件の640標本/64観測を行う。変更前の結果を保持し、各実験の入力/DLL/生成物/receiptを別に記録する。

## リスクと対策

cache共有で型が長寿命化しないようqueryだけが所有する。予約の集約で失敗時の旧group予約を消さず、未完成groupを返さない。count最適化は内部実装の契約へ限定し、未知の利用者groupのgetterを勝手に呼ばない。測定の時間・割当・保持量・予算・OS値を混同しない。

## 採否

三段階を採用し、同じ16条件と旧/新の交互実行32条件を監査した。結果・回帰・残る候補と停止理由は[最終報告](20261104-group-materialization-results.md)に記録した。大きな固定費を除去した後の追加効果が小さくなったため、利用者の停止条件に従って終了した。

## 合意状態

利用者の即時実装と反復改善の指示を根拠に進める。既存の意味/寿命/予算を変える選択が必要になった場合は、この指示の範囲で勝手に弱めない。

## 第一段階の観測

query-localな構築器の共有だけを16条件で測定した。4,096行/Memory/worker1のGroupEscapeMultipleはmanaged割当6,191,072→1,413,932バイト、16,384行では8,550,844→3,773,408バイト。時間は2.588ms/7.517msとなったが、通常LINQの時間も旧測定から変動したため、最終判定では旧DLLと最終DLLを巡回して測る。第一段階の監査はF:/CodexTemp/Seamless/group-optimization-A-20261104/ordered-comparison-audit.json。旧snapshotとの差分はgroup runtimeと生成接続の3ファイルだけだった。

## 第二段階の具体化

内部groupの保持countを内部interfaceで渡し、countが信頼できる時だけ配列を正確に確保する。未知の利用者groupのCount getterには触れない。scope/owner検査と実列挙数の一致を検証する。ReadOnly IListを返却groupに実装して、標準LINQと同様にCount/CopyTo等を使えるようにする。変異操作は拒否する。

既定codecの参照なし・nullableなしのfieldだけで構成される値型は符号化長が値によらないため、query内で一度求めた長さを使う。既存のCanUseParallelCodec判定に加え、nullableを再帰的に除外する。独自codecの登録、参照、nullable、FixedTextは従来どおり値ごとにGetEncodedLengthする。既知countと固定長が揃った時だけ全payloadを配列確保前に予約する。未知countでは既存の有界拡張を維持する。

第二段階の16条件は監査済み。Memory/worker1では4,096行が1.297ms/1,029,144バイト、16,384行が3.442ms/2,159,952バイト。通常LINQは1.119ms/2,031,800バイト、4.094ms/7,601,336バイト。Execution94件とDB展開19件がDebug/Releaseとも成功。証跡はF:/CodexTemp/Seamless/group-optimization-B-20261104。

## 最後の候補

worker1のgroup列挙でもPriorityQueueによるmergeと、優先順位の取得のための各groupのFirst列挙を行っている。ScopedLookup.Groups自体が初出順なので、worker1だけ直接列挙して同じ順序を維持する。複数workerのmerge、scope検査、groupの借用/所有境界は維持する。既存のMemory/Spillとworker1/3の順序試験、DB展開試験、同条件比較で採否を決める。以降はこの固定費の減少量と残る所有化部分の寄与を見て、些末な案のみなら終了する。

この候補を採用した。Memory/worker1の割当を両行数とも約131KB減らし、追加の時間改善は独立測定間で約1～3%だった。Execution94件、DB展開19件、Native統合2件がDebug/Releaseとも成功。最終の旧/新交互実行32条件・1,280標本・128観測が成功し、旧保存snapshotと新版/現在入力、全runtime DLLと生成物のhashを照合した。
