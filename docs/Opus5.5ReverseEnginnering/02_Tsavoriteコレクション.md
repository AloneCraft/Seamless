---
phase: opus5.5-reverse
reverse_document: true
base_commit: d3a5a3e8
target: Seamless.Collections.Tsavorite
evidence_sources:
  - Seamless.Collections.Tsavorite/Stores/StoreExtensions.cs
  - Seamless.Collections.Tsavorite/Stores/TsavoriteLayout.cs
  - Seamless.Collections.Tsavorite/Stores/TsavoriteTuning.cs
  - Seamless.Collections.Tsavorite/Stores/CollectionStore.cs
  - Seamless.Collections.Tsavorite/Stores/DictionaryStore.cs
  - Seamless.Collections.Tsavorite/Stores/Store.cs
  - Seamless.Collections.Tsavorite/Collections/Collection.cs
  - Seamless.Collections.Tsavorite/Collections/HashSet.cs
  - Seamless.Collections.Tsavorite/Collections/Dictionary.cs
  - Seamless.Collections.Tsavorite/Collections/IDictionary.cs
  - Seamless.Collections.Tsavorite/Collections/TryAddResult.cs
  - Test/Seamless/Stores/StoreExtensions.cs
  - Test/Seamless/Tsavorite/HashSet.cs
  - Work/qa/handoff.md
  - C:/Users/kazuh/.claude/plans/qa-workflow-md-validated-stream.md
confidence: high
review_status: draft
---

# 02 Tsavorite コレクション（Seamless.Collections.Tsavorite）

## 行うべき機能

作り方は「共通ストア → データ本体 → 操作用オブジェクト（スレッドごと）」の3段階。

```csharp
using var store=Seamless.Execution.Store.Open(path,new(){MemoryBytes=1L<<30,DiskBytes=64L<<30});
using var orders=store.CreateTsavoriteHashSet<long>("orders",expectedCount:10_000_000); // データ本体
using(var set=orders.Create()){set.Add(1);}                                          // 操作用オブジェクト
store.Save();
```

| 機能 | 内容 | 状態 | 根拠 |
|---|---|---|---|
| ストアから作る | `CreateTsavorite<K,V>`（辞書）・`CreateTsavoriteHashSet<T>`・`CreateTsavoriteSet<K,R>` | 実装済み | `Stores/StoreExtensions.cs:9-18` |
| 配置の自動決定 | デバイス、保存フォルダ、索引・ログの大きさをストアの種類・上限・予想件数から決める | 実装済み | `Stores/TsavoriteLayout.cs:44-60` |
| 詳細設定の公開 | ページ・セグメント・索引・自動拡張・ストレージ方式は公開 `TsavoriteTuning` で指定する。省略時の自動決定を維持 | 実装済み（2026-09-25 の追加承認） | `Stores/TsavoriteTuning.cs`、`Stores/StoreExtensions.cs` |
| 件数の管理 | データ本体が件数を持つ。シングル用(`Create()`、同時に1つ)は追加・削除のたびに増減し O(1)。マルチ用(`BeginParallel()` の区間の `Create()`)は件数を持たず `ScanCount()` で毎回数える。区間を閉じると「不明」に戻し、次に全走査で数え直す | 実装済み | `Stores/CollectionStore.cs`(`OnAccessorCreated`・`FinishParallelRegion`)、`Stores/ParallelRegion.cs` |
| 索引の自動拡張 | ログの伸びから件数を推定し、混んでいれば索引を倍にする。拡張分は予算から先に予約。並列区間の中では広げず、区間を開くときに見込み件数分まで先に広げ、閉じたときに混んでいれば広げる | 実装済み | `Stores/CollectionStore.cs`(`GrowIndexIfCrowded`・`BeginParallelCore`) |
| 詰め直しと無駄の測定 | `Compact()` が生きているレコードを末尾へ少しずつ移し、先頭を進めて空きを返す(メモリ・一時・永続のすべて)。`MeasureLogUsage()` が生きているレコードの大きさと無駄の割合を測る。`GetStatistics()` には件数(分かっていれば)とログの大きさが入る | 実装済み | `Stores/CollectionStore.cs`(`CompactCore`)、`Stores/CompactionResult.cs`、`Stores/TsavoriteStatistics.cs`、`Test/Seamless/Stores/Compaction.cs` |
| 保存と復元 | ストアの `Save()` で確定。保存時にログを flush（EXP-0002） | 実装済み | `Test/Seamless/Stores/StoreExtensions.cs:121-175`、【記録】EXP-0002 |
| 標準の集合インターフェイス | `HashSet` が `System.Collections.Generic.ISet<T>` を実装 | 実装済み | `Collections/HashSet.cs:9` |
| 関係演算の逐次化 | 部分集合判定などで自分を全件メモリに載せない | 実装済み | `Collections/HashSet.cs:10-35` |
| `IReadOnlySet<T>` の実装 | 決定10 | **未実装**（文書コメントだけが実装済みと書いている） | `Collections/HashSet.cs:5` と `:9` の食い違い |
| 辞書の型引数を2つにする | 現在は `Dictionary<TKey,TValue,TKeySerializer,TValueSerializer>` の4つ | **未実装**（段階3d） | `Collections/Dictionary.cs:9-22` |
| 比較子の指定 | 既定はバイト一致、比較子を渡せば標準と同じ判定（決定7） | **未実装**（段階3d） | 比較子を受け取る生成 API がない（`Stores/StoreExtensions.cs`） |
| `TryAdd` を `bool` にする | 標準と同じ戻り値。容量超過は専用例外（決定10） | **未実装**（段階3d） | `Collections/IDictionary.cs:23`、`Collections/TryAddResult.cs:3-7` |
| 誤った並行使用の検出 | Release でも安価に検出して例外（段階3） | **未実装**（DEBUG で「作成スレッド以外からの使用」を警告するだけ） | `Collections/Collection.cs:135`、`:640-650` |
| 旧 API の削除 | 設定値 struct の `Store`、多数の `Create`、`IDevice` を受け取る公開 API、公開 `TsavoriteKV` フィールド | **未実装**（段階3b） | `Stores/Store.cs:6-50`、`Stores/CollectionStore.cs:94` |
| データ本体の改名 | `HashSetStore<T>`→`HashSet<T>.Store`、`DictionaryStore<K,V>`→`Dictionary<K,V>.Store`（決定12） | **未実装**（段階3b） | 現在の型名 `Stores/HashSetStore.cs`、`Stores/DictionaryStore.cs` |
| リフレクション生成の除去 | 辞書の生成が `MakeGenericType`/`Activator` に頼っている | **未実装** | `Stores/DictionaryStore.cs:71-83` |

## 1. 不変条件

1. **等値判定の既定はシリアライズ後のバイト列の一致**（`Collections/HashSet.cs:6`）。比較子を導入しても、既定の意味は変えない（決定7）。
2. **列挙の順序は保証しない。** 順序付き LINQ 演算子を使う側は、`AsEnumerable()` などで明示的に受け入れる（`Seamless.Linq.Analyzers` の `SEAMLINQ001`）。
3. **件数の正本は `long`（`GetLongCount()`）**（`Stores/CollectionStore.cs:236`）。シングル用では追加・削除のたびに増減する。辞書の代入は Tsavorite の RMW で新規か上書きかを受け取るので、件数は分かったまま保たれる。一括の集合演算のあとや並列区間を閉じたあとは「不明」にして、次回に全走査で数え直す。
4. **シングル用の操作用オブジェクトはデータ本体に同時に1つだけ。** 2つ目の `Create()`、並列区間が開いている間(閉じ中も含む)の `Create()`・読み込み・2つ目の区間は `InvalidOperationException`。マルチ用は件数を変えない。作成に失敗した操作用オブジェクトは、ファイナライザで他の権利を返さない(BUG-0018)。
5. **並列区間の中では索引を拡張しない**(`CollectionStore.GrowIndexIfCrowded`)。索引の拡張・保存・読み込み・件数の走査・統計・詰め直しは、データ本体の関所で順番に行う(拡張は保存とも同時に走れない)。Tsavorite の `GrowIndexAsync` を並行操作中に呼ぶと停止するため（【記録】EXP-0004。2.0.1-beta.5 と 2.1.8 で確認。2.1.8 でガードを外した複製で、4スレッドの約2.6%・8スレッドの約9%・16スレッドの過半数が停止または例外）。
6. **索引拡張のメモリは予算から先に予約し、予約できなければ拡張しない。** 正しさは変わらず、性能だけが落ちる（`CollectionStore.cs:307-329`）。
7. **メモリ種は上限を超える追加を `ResourceLimitException` にし、それまでの要素を失わない**（`Test/Seamless/Stores/StoreExtensions.cs:75`）。退避先は `NullDevice`、判定は実アドレス＋余裕で行う（【記録】EXP-0001）。
8. **一時ストア・永続ストアでは、メモリ上のログを超えた分をディスクへ退避し、すべて読める**（`Test/Seamless/Stores/StoreExtensions.cs:91`）。ディスク上限を超える追加は `ResourceLimitException`（`:109`）。
9. **保存は明示的な `Save()` の時点だけが確定。** 保存後の追加は、開き直すと消える（`Test/Seamless/Stores/StoreExtensions.cs:121`）。
10. **索引の大きさはストアの保存状態に記録し、開き直すと拡張後の大きさで復元される**（`Test/Seamless/Stores/StoreExtensions.cs:132`、`:175`）。
11. **同じ名前のデータ本体は1つのストアに2つ作れない**（`Test/Seamless/Stores/StoreExtensions.cs:197`、`Seamless.Execution/Store.cs:111-115`）。
12. **FLinq 生成器が前提にしているメンバーを保つ。** `Iterate(ref strategy)`、`.Count`(シングル用だけ。マルチ用では件数を読まずに集め、素の `Count()` は `ScanCount()` を呼ぶ)、`.Contains`、型名 `HashSet`/`Set`/`ParallelHashSet`/`ParallelSet`/`Dictionary`。生成器は型名を文字列で照合している（【記録】承認済み計画「壊してはいけない結合点」：`ProviderFacts.cs`、`UnorderedSourceSafety.cs`、`OptimizeVisitor.cs`）。
13. **チェックポイントはバッファ付きのデバイスで書く**（`CollectionStore.cs` の `CreateCheckpointManager`）。索引は 64 バイトから始まり、主索引の書き込み長は索引と同じなので、Tsavorite 既定のバッファなし書き込み（長さがセクターの倍数でないと Win32 エラー 87）では小さいストアの `Save()` が失敗する。2.0.1-beta.5 はこの失敗を黙って捨てて主索引を保存していなかった（2.1.8 は例外にする）。【記録】2026-09-29 の実験：索引 64・128・256 バイトで beta.5 は主索引領域が全ゼロ、2.1.8＋バッファ付きは全サイズで保存・復元に成功し中身が書かれる。
14. **`Save()` が終わった時点では、チェックポイントは OS のキャッシュに残っていてよい。ディスクへ確実に書くのは `Flush()`。** `Flush()` は、保存済み（まだ `Flush` していない）チェックポイントのフォルダ、索引サイズのメタデータ（`.Tsavorite`）、ログとオブジェクトログのデバイスのセグメントのファイルを `RandomAccess.FlushToDisk` で書き出す（`CollectionStore.cs` の `Flush`、`GetFlushTargets`）。`Save()` を続けて呼んだあとに 1 回呼べば、まとめて書き出せる。共通ストアの永続ストアは保存のたびに、マニフェストを書く前に自分で呼ぶ。【記録】2026-09-29 の実測：永続ストアの `Save()` は、書き出しなしの一時ストアより約 13〜17ms 長い（要素数 1000〜100万でほぼ一定）。
15. **復元は、一時的な LogSizeTracker を付けて行う。復元後に末尾ページのスロットが先頭ページのスロットと重なっていたら例外にする**（`CollectionStore.cs` の `RecoverWithSizeTrackerAsync`、`ThrowIfRecoveredTailCollidesWithHead`）。Garnet 2.0.1-beta.6 以降（2.1.8 でも確認）の復元は、LogSizeTracker が無いと復元中にページを追い出さず（`TrimLogPages` の先頭が `logSizeTracker is null` で何もしない）、バッファに収まらないページの内容を失う。次のどちらでも起きる（黙ってデータが欠ける。索引の入口はあるのに読めない）。(1) 保存時にログの末尾がページ境界ちょうどで、バッファが満杯だった（`RecoveryReset` が末尾ページを、先頭ページと同じスロットにゼロクリアする）。(2) 保存時より小さいメモリで開き直した（`HeadAddress` が進まず、上書きされたページが「メモリ上にある」ままになる）。追跡は開始せず（背景の追い出しは動かさない）復元の間だけ付けて外す。復元側の修正なので、既に境界で保存済みのチェックポイントも復元できる。【記録】2026-09-30 の実験：`HashSet<long>` は 1 件 32B・ページ 4096B なので、バッファ 16 ページなら件数 2016・2142 … で 100% 再現し、メモリを縮めた開き直しは約半数で欠落した（ランダム 600 試行）。beta.5 では起きない。beta.5〜2.1.8 の二分探索で beta.6 が原因（upstream PR #1892 の復元の追い出しの作り直し）。追跡ありの復元は、境界の件数の掃引・ランダム 600 試行・管理型の辞書・FoldOver・SaveLogOnly・強制終了ハーネスで欠落 0、2.1.8 で境界保存されて欠落していた元のチェックポイントも復元できた。保存の前にヘッドを進める案は、既存のチェックポイントを救えないので採らなかった。復元後に `numPagesToPreload` を渡す案、チェックポイントの前にフラッシュする案、索引チェックポイントを使う復元は効かなかった（実験済み）。Garnet が直ったかは、`Test/Seamless/Stores/CollectionStore.cs` の `ThrowIfRecoveredTailCollidesWithHead`（Garnet の素の復元が衝突することを確かめるカナリア）が失敗したら、追跡を外せるか同じ実験で確かめる。
16. **削除した最新の墓標を無効化させず(`RevivificationSettings.InChainOnly`)、保存後のログの書き出しはセクター境界までにする**（`CollectionStore.cs` の KVSettings、`FlushLogToSectorBoundary`）。保存後に更新・削除してから開き直しても、最後に保存した時点へ戻る、という契約のため。次の 2 つの機構で、戻らないことがあった(Garnet の 2.0.1-beta.5 でも 2.1.8 でも同じ。ランダムな操作列 1600 本のうち約 2.4%)。(a) 欠落: 保存後の書き出しが末尾(セクターの途中)まで書くと、次の書き出しが末尾を含む 512B セクターを頭から書き直し、その間にコピー更新で無効化(Sealed+Invalid)された古いレコードが書き込まれて、保存時点の復元で見えなくなる(決定的な再現: 100 件保存→保存→最後の 6 件のどれかを削除→FlushAndEvict→開き直し)。セクター境界までに限れば、末尾を含むセクターはスナップショットに残る。(b) 復活: 既定では、削除の最新の墓標をハッシュ表の連鎖から外して無効化する(`HandleRecordElision`)。メモリ上では古いレコードも無効なので削除は見えるが、ログデバイス上の古いレコードは有効なまま残り、ログを走査して索引を作り直す復元で削除したキーが復活する。`InChainOnly` は墓標を有効なまま残し、同じキーの再追加でその場で再利用するだけの設定で、自由レコードプールは使わない。【記録】2026-09-30 の実験：ランダム 1600 本（`NOLOAD=1`）の失敗は、修正前 約 38 本、`InChainOnly` のみ約 4 本(復活型 0)、セクター境界フラッシュのみ約 30 本(欠落型 0)、両方 0 本。索引チェックポイントを使う復元(`RecoverAsync(token,token)`)は、復活型を 0 にするが、開き直し後に別の欠落が出て悪化するので採らなかった。単体テスト `StoreExtensions.Save_RandomOperations_ReopenRestoresSavedState`（修正前に失敗するシードのモデル照合）。古い保存への `Store.Load(token)` は、Tsavorite の復元が新しいログを切り詰め、Persisted の `RollbackTo` もファイルを巻き戻すので、より新しい保存を無効にする。無効な保存を読むと、例外にならず黙って誤った状態になる(実測 54/54)。この仕様の扱いは未決定。

17. **ログの先頭を進める操作(詰め直し・全削除)のあとの保存は、前の保存を記録(マニフェスト)から外す。ファイルの切り捨ては、保存が確定するまで保留する。** Tsavorite はチェックポイントのあとでログの古いセグメントを消すが、共通ストアの保存は「データ本体のチェックポイント → マニフェスト」の順なので、その間に強制終了すると、マニフェストが指す前の保存のファイルが既に消えていて開けない(【記録】EXP-0005 で再現)。ログのデバイスを `DeferredTruncationDevice` で包み、切り捨ての要求は記録だけして、マニフェストを書いた後の `IStoreComponent.SaveCommitted` で、その保存が必要とする先頭より前だけを消す。`Store.Save` は `InvalidatesOlderSaves` が true の子資源があれば、新しい保存だけを残したマニフェストを書く。`Store.Load(token)` は、記録を変える前に `ValidateLoad` で読み込めるかを確かめる(BUG-0019)。全削除(`Clear`)は、ログのファイルを新しい世代(`gen1-hlog.*`、`gen2-hlog.*`…)へ切り替え、前の保存が使う古い世代のファイルは、新しい保存が確定して前の保存が記録から外れたあとに消す(BUG-0023。以前は同じ名前で先頭から書き直し、保存前に書くと前の保存のログが上書きされた)。保存の状態に世代を記録し、開き直すと保存した世代のファイルを開いて、それ以外(保存されなかった世代・消し残し)は消す。全削除のあと保存せずに、全削除より前の保存を `Store.Load` することはできず(`InvalidOperationException`。開き直せば戻れる。Tsavorite の復元が世代の切り替えを安全に戻せなかったため)。
18. **メモリ種の詰め直しは、写しを書くページの空きがあるときだけ進める。** 写しを書くために新しいページを開くと、満杯のログでは先頭のページ(まだ読み終えていない区切り)が追い出されて失われる(退避先は捨てる)。空きが足りなければ、何も移さずに `ResourceLimitException`(【記録】exp0006。満杯のログで 123 件が黙って消えた)。削除の墓標もログの空きを使うので、満杯に近いメモリストアは詰め直せない。永続・一時ストアは追い出し前にディスクへ書くので影響しない。

19. **シングル用(`Create()`)の共有の読み取りセッションを、複数のスレッドが同時に走査してはいけない。** 同時に列挙すると、誤った結果と `IndexOutOfRangeException` が出る(【記録】exp0007: 8スレッド×8秒で誤った結果488件・例外2件。BUG-0016)。`Contains` だけの同時実行は問題が出なかったが保証はしない。対応は検出だけ: Debug ビルドでは走査の入口で別のスレッドの同時走査を `InvalidOperationException` にし(`ScanScope`、同じスレッドの入れ子は許す。Release ではコストなし)、コンパイル時はアナライザー `SEAMTHREAD001`(警告)が、スレッドを起こす呼び出し(`Task.Run`・`Task.Factory.StartNew`・`Parallel.*`・`new Thread`・`ThreadPool.QueueUserWorkItem`)に渡したラムダが、外のシングル用(`HashSet`・`Set`・`Dictionary` を継承する `Collection<…,ExclusiveAccess>`、Database の `Table`)を捕捉する形を警告する。見逃す形: インターフェイス型の変数、メソッド呼び出しをまたぐ共有、他のオブジェクトのメンバー経由。Database のテーブルは、読み取りゲートが複数の読み手を通すので、同じテーブルを複数のスレッドで同時に読まない前提になる。

## 2. 禁止事項

- 型名 `HashSet`・`Dictionary` を変えない（FLinq の照合が壊れる。決定10）。
- 他の操作用オブジェクトが生きている間に `GrowIndexAsync` を呼ばない（不変条件5）。
- 集合演算・関係演算で自分自身を全件メモリに載せない（承認済み計画 §2-8）。
- ページ・セグメント・`MutableFraction` を利用者向け API に出さない（決定3・6）。
- 1つの操作用オブジェクトを複数スレッドから**同時に**使わない。並行する処理ごとに `Create()` で作る。
- 誤用の検出を「作成スレッドと違えば例外」で実装しない。`Seamless.Database` はテーブルごとに1つの操作用オブジェクトを持ち（`Seamless.Database/DatabaseContainer.cs:408-411`）、書き込みを直列化したうえで別スレッドから順に使う（`AsyncLocal` の実行コンテキスト単位：同 `:18-20`）。スレッド固定で例外にするとこの使い方が壊れる。【推測・中】検出は「同時に2つの操作が入った」ことを数える方式にする。

## 3. 前提条件

- Garnet `Microsoft.Garnet` 2.1.8 に依存する。並行時に索引を拡張できない制約は 2.0.1-beta.5 と 2.1.8 の両方で確認した。ガードは維持して受け入れる。
- シリアライザは型から自動で選ぶ。参照を含む型は MemoryPack、それ以外は生バイト（`Stores/DictionaryStore.cs:26-27`）。
- テストは挙動が分かるよう、わざと最小の大きさで作る。自動決定にしても、テストから `TsavoriteTuning` で最小サイズを指定できる状態を保つ（【記録】承認済み計画「前提（ユーザー指示）」）。

## 4. 変更範囲 / スコープ

- `Seamless.Collections.Tsavorite/**`
- 型名・メンバーを変える場合の照合先：`Seamless.FLinq.Generator/SourceGenerator/FLinqIncrementalGenerator.ProviderFacts.cs`、`UnorderedSourceSafety.cs`、`OptimizeVisitor.cs`、`Linq/Fragments/Tsavorite/*`。
- 利用側：`Seamless.Database/DatabaseContainer.cs`、`Seamless.Linq/Internal/ProviderEnumerators.cs`、`ProviderValueEnumerable.cs`、`Contains.cs`、テスト・ベンチ。

## 5. 非対象 / Out of Scope

- Tsavorite 本体の不具合修正（並行時の索引拡張）。
- 旧形式のチェックポイントとの互換（決定11）。
- 1つの `TRecord` がキーを二重に保存している点の最適化（`Stores/SetStore.cs:5` の `KEYED-RECORD-CODEC-TODO`）。別課題。

## 6. 完了条件 / 成功条件

- 上の「未実装」がすべて実装され、旧 API への参照がリポジトリに残っていない。
- `HashSet` が `ISet<T>` と `IReadOnlySet<T>`、`Dictionary` が `IDictionary<K,V>` と `IReadOnlyDictionary<K,V>` を完全に実装し、戻り値と例外が標準と一致する（決定10）。
- 標準コレクションとの差分テスト（同じ操作列を両方に流して結果を比べる）が、比較子あり・なしの両方で通る。

## 7. 検証項目 / 受け入れ条件

| 確かめること | 手段 |
|---|---|
| 3種類のストアでの追加・上書き・読み取り・保存・復元 | `Test/Seamless/Stores/StoreExtensions.cs:14`（`CreateTsavorite`）、`:55`、`:204` |
| 上限超過（メモリ・ディスク）が例外になり、既存要素が残る | 同 `:75`、`:109` |
| 索引の自動拡張と、拡張後の保存・復元 | 同 `:159`、`:175` |
| 関係演算が標準 `HashSet` と一致 | `Test/Seamless/Tsavorite/HashSet.cs:25-68` |
| 並列区間で書いて閉じたあと件数が正確・区間中の作成や読み込みが例外 | `Test/Seamless/Stores/ParallelRegion.cs` |
| 詰め直しで中身と件数が保たれ空きが戻る・満杯では何も移さず例外・保存の確定まで前の保存へ戻れる・強制終了の窓・前の保存が記録から外れる | `Test/Seamless/Stores/Compaction.cs`、`Execution.Tests/Store.cs`(`Save_DropsOlderSaves` など) |
| シングル用の2つ目の作成が例外・作れなかった操作用オブジェクトが権利を返さない | `Test/Seamless/Stores/CollectionStore.cs`(`OnAccessorCreated`) |
| マルチ用を起点にした FLinq が件数を読まずに System.Linq と同じ結果を返す | `Test/Seamless/Tsavorite/Linq/Enumerable/FLinqParallelSourceTests.cs` |
| 大量件数で常駐メモリが上限内 | `Test.Scalable.Tsavorite.Collections`（1,000万件規模）【記録】承認済み計画「検証」 |
| FLinq の生成コードが引き続きネイティブ走査に展開される | `Test/SourceGenerator/ProviderContainsShapeTests.cs`、`Test.Tsavorite.Linq.Enumerable.*` |

## 8. 移行条件

- 旧 API（`Tsavorite.Store.Create(...)` → `.Create<T>(logDevice,objDevice)`）を使う箇所は、【確認】2026-09-25 時点で **49 ファイル**（`Test` 27、`Shared` 11、ライブラリ自身 3、`Tests` 2、`Benchmark` 2、`Seamless.Database` 1、その他 3）。新 API の利用はテスト 8 ファイルだけ。
- 【記録】2026-09-19 時点の棚卸し：旧 `Store.Create(` 249 か所、デバイス直渡し 90、番号指定の保存・復元 92、型名 57。
- 移行順は DB → LINQ → テスト・ベンチ。改名（段階3b）と型引数・比較子・`TryAdd`（段階3d）は同じ作業で行う。
- 最小サイズを使うテストは 公開 `TsavoriteTuning`へ移す。

## 9. ロールバック条件

- 改名・旧 API 削除の途中でビルドが通らない、または回帰が出たら、そのプロジェクト群の移行だけを戻す。新 API（`73950c6e`）は残す。
- FLinq の形状テスト（ネイティブ走査への展開）が崩れたら、型名・メンバーの変更を戻す。

## 10. 既知の問題 / 許容事項

- 【記録】並列区間の中では索引を拡張しない。大量並列投入では `BeginParallel(見込み件数)` に全体の件数を渡す(区間を開くときに先に広げる)。マルチ用は件数を持たず、`ScanCount()` が毎回全件を数える。
- 【記録】満杯に近いメモリストアは詰め直せない(写しを書くページの空きが要る。不変条件18)。全削除のあと保存する前に書き込むと、前の保存のログが上書きされる(BUG-0023、未修正)。
- 【記録】メモリ種の集合演算の作業リストは `%TEMP%` を使う。
- 【記録】BUG-0001（DEFERRED）：辞書のインデクサ setter が `fixed` の外で Pending を呼んでいる疑い。BUG-0002（DEFERRED）：セッションの二重 Dispose。どちらも段階3b でセッションまわりを作り直すときに解消する予定。
- 【記録】BUG-0006（DEFERRED）：`StoreCheckpoint` の async テストが共有違反で不安定。4つの仮説を否定済み、原因未特定。
- 【記録】作成時に Tsavorite 内部で約 8.5MB を確保する（OBS-0001）。空に近いストアでも減らせない。

## 確認済み / 推測 / 不明

- **確認済み**：新 API の3種類、件数管理、索引拡張の条件、旧 API の残存数、`IReadOnlySet<T>` 未実装、`TryAdd` が列挙型、リフレクション生成、スレッド検査が DEBUG 限定。
- **推測**：なし。
- **不明**：比較子を指定した場合のハッシュ計算と保存形式（比較子でハッシュし、候補を復元して比べる、という決定7の方針はあるが、ディスク上の表現は未設計）。
