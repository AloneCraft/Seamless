> 追補: 親へのfast-forward省略を明示mergeへ修正し、45e873dbも統合した。現行の接続・検証・保全は [追補監査](../explicit-parent-returns-20261030/README.md) を参照。以下は前回受入時の記録。

# ブランチ統合・履歴整理の監査（2026-10-30）

保守系列は `main → 0.1 → 0.1.0`。完成した共通履歴をmainへ集約した後、mainから0.1、0.1から0.1.0を作る。初期点は3枝ともタグ `v0.1.0` と同じコミットになる。Gitは枝の親を保持しないため、親の規則を [運用規則](../../git-branch-policy.md) と [親一覧](../../git-branch-parents.json) に記録する。

## 全コミット・全枝の対応

- [旧1,001コミットの対応表](all-original-commits.csv): original、旧parents、件名、所属枝、採用判断、置換先。
- [旧42ブランチ参照の戻し先](all-branch-routes.csv): ローカル34・リモート8。直接の親、先端の置換先、archiveタグ、利用中保持を記録。
- [書換えと統合操作の対応](rewrite-map.json)、[全コミット監査](all-commit-audit.json)。

| 判断 | コミット数 |
|---|---:|
| 同一IDで保持 | 892 |
| 正しい親系列へ載せ直し | 66 |
| 同値パッチとして一度だけ採用 | 23 |
| 誤った統合トポロジーをarchiveへ保全 | 20 |
| TPC-C実験の保留（追補で解除） | 0 |
| 未分類 | 0 |

20個の旧mergeは `--remerge-diff` でも監査した。14個には独自の競合解決差分がなく、6個にあった解決内容は、3種類のファイル・10照合行の全てでレビュー済み後続ソースと現在のblobが一致した。単に旧mergeを捨てた扱いにはしていない。証拠は [解決差分](old-merge-resolution-audit.json) と [内容の照合](old-merge-resolution-content.json)。

子は直接の親へ戻した。TPC-Eのcompliance／generated-errors／FLinqの親は利用者指定のtpce-benchmark。TPC-Cとその後のrefactorも元の親系列へ載せ直した。追加SQL/SSMSは source `d0f8d31e` → 1846 `2c3df34d` → 4252 `847e4d67` → master候補 `a9840e48` の順で戻し、途中の親を飛ばしていない。旧0.1.0の変更も履歴上のmasterへ戻してから、新しい版の枝を作る。

## 承認済み未コミット変更

470ファイル、TPC-C/C12の384製品ファイル、追加SQL/SSMS28ファイルを、承認済みの各スナップショットから候補へ取り込んだ。選択は各review/selection JSONに保存した。ファイル単位の欠落は0。[正規化比較](approved-source-coverage-current.json) は重複選択を含む群別比較で、群の件数を単純合計して固有ファイル数とはしない。

ソリューション登録はC12とSQLの両方を残して再生成した。109 csprojと17 shproj、合計126がSeamless.slnxに登録され、SharedGUID・ProjectGuid・projitems importは全て一致する。証拠は [project監査](static-project-final-audit.json)。

SSMSの7 SQLリソースは、文字列リテラル内のCRLFを保持した。空白token内の行末空白だけを正規化し、文字列・識別子等の非空白tokenが承認元と全て一致することをScriptDomで確認した。[照合結果](ssms-resource-token-equivalence.json)。

## 未完成・対象外の保全

- TPC-C spikeの3コミットは追補指示で統合。元実験worktreeの未コミット26ファイルは今回のSHA指定とは分けて保全。
- 旧TPC-E作業の18ファイルは、現行EGen harnessが利用する公開Frame1～6を削除する互換性問題があり保留。現行APIへ移植して受入する必要がある。
- TestSeamlessの新規34受入ケースを実行し、30成功・4失敗だった。4件は取り込み元にも対応実装がない未完成範囲なので、[失敗根拠と復元可能な保留パッチ](held-protocol-tests.md) に分離した。修正済みとは扱わず、Skipや実行フィルターで成功扱いにもしていない。
- 同期前バックアップ265ファイル、scratch/lock 5ファイル、途中の検証文書・実行証跡等は製品へ入れず保全。focused solutionは再生成。

登録済み17 worktreeの扱いは [一覧](worktree-disposition.json)。利用中の旧8枝と未コミット内容・index・HEADを保護する。登録先が存在しないworktreeも勝手にpruneしない。元worktreeへreset/stash/cleanは行わない。比較元SQL Server、稼働中サーバー、実データも変更しない。

## 検証

製品ソースの検証基準は `864d591c3c4bed5881e2c44999d9135355cdc57e`。未完成4ケースの保留後の基準は `7529303ba3dd4a085f8d963e352e7b53c14dcd36` で、製品実装は同一。タグv0.1.0の先端には、この監査文書・枝の運用規則と18パッケージのrelease設定を加える。

[受入記録](final-validation-accepted.json): 18段階、構成別の重複を含む実行数合計6,151成功・失敗0・スキップ0。Debug main 2,305とRelease main 2,304の差1件はDebug専用の複数スレッド拒否テストである。

| 段階 | 成功 | 失敗 | Skip |
|---|---:|---:|---:|
| gateway-final-debug-01 | 136 | 0 | 0 |
| query-final-debug-01 | 22 | 0 | 0 |
| main-final-debug-02 | 2305 | 0 | 0 |
| main-final-release-01 | 2304 | 0 | 0 |
| transactions-final-release-01 | 25 | 0 | 0 |
| database-transactions-final-release-01 | 30 | 0 | 0 |
| query-final-release-01 | 22 | 0 | 0 |
| gateway-final-release-01 | 136 | 0 | 0 |
| tpcc-final-release-01 | 65 | 0 | 0 |
| tsavorite-collections-final-release-01 | 368 | 0 | 0 |
| execution-final-release-01 | 44 | 0 | 0 |
| dynamic-final-release-01 | 384 | 0 | 0 |
| infrastructure-final-release-01 | 24 | 0 | 0 |
| scalable-final-release-01 | 26 | 0 | 0 |
| tpce-final-release-01 | 27 | 0 | 0 |
| tpce-compliance-unit-final-release-01 | 16 | 0 | 0 |
| main-windows-final-release-01 | 187 | 0 | 0 |
| testseamless-final-release-03 | 30 | 0 | 0 |

[実EGenの検証](native-validation-accepted.json): Debug/Release各6ケース、合計12成功、失敗・Skip 0。資産を必須にし、DLL/Loader/ValidateをC12受入時のSHA256で照合。実取引、WAL replay、Persisted/Tsavorite両backendの繰り返し中断復旧、ABI、harness TradeStatusを確認した。C12の全37ケースの過去受入は別証拠であり、今回の候補で全37を再実行したとは主張しない。

[配布依存](release-project-closure.json): 旧12個にTransactions、Transactions.Collections、Database.SqlServer、Database.Client、Database.Query、Database.Gatewayを加えた18パッケージ。owned dependencyの欠落・Coreのmanaged DLL3個/native10個・license/provenance/patch・Samplesを検証する。専用フィードのPackageReferenceだけを使う独立EXEでメモリとディスクのAdd/Contains/件数/Remove、Core DLLのSHA一致、CLIローカル導入とsamples実行も成功した。[記録](package-consumer-proof.json)。最終タグと同じコミットから配布一式を再作成して確認する。

計算処理は子孫を含む同じ最大8論理CPU mask 0xFF、作業・実データD、ログ・キャッシュF、専用の直接実行helperを使用した。

## 参照の保全と公開

旧ローカル・リモート枝/tagは44個のGitHubタグ `archive/20261030T053031Z/` とローカルbackup参照でOIDを照合済み。[一覧](remote-backup-verified.json)。元のbundle・全worktreeスナップショット・TRX・操作receiptは `F:/CodexTemp/SeamlessBranchIntegration-20261030T053031Z/` に保全する。

切替はOIDを照合してmain/0.1/0.1.0とv0.1.0を更新し、GitHub既定枝をmainとする。削除対象はバックアップと一致し、統合済みかつ利用中でない旧枝、および当作業の一時候補枝だけ。切替・削除時に再照合し、他者が進めた枝や利用中の枝は削除しない。旧8作業枝は保持し、再開時は対応する修正済み親系列へ移植する。

GitHub Actionsの過去ジョブはアカウントの支払い失敗または利用上限により開始されなかった。CI成功とは扱わず、請求・上限設定は利用者側で解消が必要。Releaseは最終コミットからローカルで作成・検証した成果物を使用できる。nuget.orgへの公開は行わない。
