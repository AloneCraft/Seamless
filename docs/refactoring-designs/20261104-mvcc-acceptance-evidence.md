# MVCC製品受入の証拠対応

## 判定の範囲

[実装計画](DeferredMvccImplementation.md)のMV01～MV42をそのまま受入対象とする。Gatewayの複数DBの行変更・DDL世代・Native公開を一つの確定へ接続し、FLinqの高速な走査、有限の履歴・作業予算、既存APIを維持する。履歴backendの専用化は比較用prototypeであり、製品採用済みとは扱わない。

この表は、試験名の存在だけで完了を宣言するためのものではない。「確認済み」は下記の観測・assertionの内容をソースで確認したことを表す。実行結果、Debug/Releaseの入力一致、現在のソースとの一致は末尾の最終監査で別に照合した。未確認の条件を試験名の存在だけで完了へ含めない。試験が通っていても必要条件の一部しか確認していなければ、残る条件を明記する。

基準となるソースの場所は以下のとおり。

- 共通取引: `Tests/Seamless.Transactions.Tests/`
- DBと生成器: `Tests/Seamless.Database.Transactions.Tests/`
- Gateway: `Tests/Seamless.Database.Gateway.Tests/`
- 資源管理とLookup: `Execution.Tests/`
- TPC-E: `Benchmark/Benchmark.TpcE.Tests/`

## 条件ごとの対応

| 条件 | 現在の具体的な証拠・確認する観測 | ソース内容の照合 |
|---|---|---|
| MV01 snapshot | `VersionedTableTests.RealTsavoriteTablesShareASnapshotAndMergePrivateChangesIntoBothIterationPaths`は別取引の確定後も右表の旧値100を読み、現在の右表200・左表70を新しいscopeで確認する。Gatewayの`SqlUsesOneSnapshotAcrossDatabasesAndReplaysTheCommonDecision`でも複数DBと再openを扱う | CoreとGatewayの旧値・共通確定・checkpoint有無での再openを確認済み |
| MV02 自己変更 | 同Core試験は更新・削除・新規キーをnative Iterateと通常列挙で照合する。DBの`AsyncTransactionOwnerExpandsItsInlineQueryAndKeepsReadonlyAndAsyncDisposal`は自己変更した左右入力のJoinを通常LINQと比較する。終端比較試験はGroupByも扱う | 点操作・native走査・Join/GroupBy比較の接続確認済み。Coreの自己変更試験へ同一キーの削除・再挿入を加え、独立した期待総和と点読取りを検査する。演算間は同じ私有変更overlayを使う |
| MV03 依存競合 | `WriteSkewAcrossTwoTablesAbortsTheOlderSnapshot`、`MissingKeysAndWholeTableScansAreDependencies`とGatewayの`ConstraintDependenciesAbortTheWholeMultiDatabaseTransaction`。UNIQUE/FKで負けた取引の別DBの変更も消えることが必要 | lost update・二表write skew・不存在/走査phantom・UNIQUE/FKの勝者両順序・別DBの部分変更不在を確認済み |
| MV04 取引の重なり | `PreparedWalWaitAllowsAnotherWriterToReadComputeAndStageAndSnapshotsStayUnpublished`は最初のWAL準備の耐久化後を障壁で止め、別取引の読取り・計算・変更の準備を完了させる。両者の未公開値と古いsnapshotを確認する。`QueryOperationKeepsTheSnapshotProtectedThroughWorkerCompletionAndFinalMerging`は同一scopeの別公開操作を拒否する | 確認済み |
| MV05 子と保存点 | `JoinedVoteIsNotDurableAndRollbackAbortsParent`、`RestoreSpilledChangesAndReuseSavepoint`、`HistoryPositionsSharePayloadAndRewindUnderExhaustedBudgets`。保存点を戻しても依存を消さない条件は`SavepointRollbackKeepsReadDependencies` | 子の投票/全体中止、退避した保存点の復帰と再利用、残予算0での復帰、読取り依存の保持を確認済み |
| MV06 可変値 | `MutablePayloadsCannotChangeStagedCommittedOrOtherTablesRows`のMemory/Storage、codecと他表の所有権分離。DBの返却groupは`GroupLifetime`で取引終了後に初めて列挙する | Memory/Storageの元入力・私有変更・返却値・native Iterate・退避後の読取り・別表・旧/新snapshotの所有権とgroup寿命を確認済み |
| MV07 ReadOnly | `TransactionReadOnlyTests`と`HistoryPressureAbortsTheOldReaderReclaimsAndRequiresAnExplicitNewWriter`。中止後の保持済み表操作はHistoryPressureで拒否し、次の明示writerは成功する | 更新/追加/削除/任意Enlist/未対応participantの拒否、子のReadOnly継承、終了後の利用拒否、中止後の操作と次のwriterを確認済み |
| MV08 native Pending | `NativePendingReadKeepsItsOperationUntilTheRealIoCallbackIsReleased`は実デバイスの正常完了と転送量を確認し、通知を保留する。寿命検査でHistoryPressureを確認してから活動中operation・物理処理未開始・世代不変を照合する。解放後の中止・世代更新・新readerの100も確認する | assertionと最終Debug/Releaseの実行・現在入力の一致を確認済み |
| MV09 終了と解放 | `CompletionHooksRunOnceAtTheRootAndCanBeUnregisteredEarly`、`AFailingCompletionStillReleasesOtherResourcesAndFaultsTheDomain`、`ReclamationReleasesAbortedReadersBuffersBeforeTheCallerDisposesItsEnumerator`。中止された遅延列挙はMoveNextで例外になる | root確定後/取消後のDisposeでcompletion回数が増えないこと、遅いDispose前の解放と列挙拒否を確認済み。CloseRootのInterlockedによる一度だけの終了とoperation障壁を実装でも照合 |
| MV10 有界な反復 | `TsavoriteHistoryTests.Memory`とProductTransactionsの256行Churn。履歴だけでなく現行データ・固定native配列・metadata・作業領域を区別し、明示した結果忘却の有無を比較する | 現在のソースで256行・64周期をMemory/Storage・両構成で強制ビルドして再実行した。独立モデル・点/走査/Join/GroupBy・削除/再挿入・忘却・再open・予算を検査し、256周期記録/8,192変更の直接監査が成功。512周期の先行試験も別snapshotの証拠として保持 |
| MV11 回収できない条件 | `DrainTimeoutReportsNoPhysicalWorkAndTheNextReclaimCanSucceed`は更新scopeを残して排出期限切れを作り、物理処理0・障害停止なし・次回成功を照合する。Pending/workerの障壁は参照保持中のCompact禁止を扱う | 更新scopeだけを保持した排出期限切れ・物理処理0・次回成功を確認済み。予約不足の受付不変はMV30のRetirement試験に対応 |
| MV12 アドレスと削除 | `CollidingKeysAreSkippedWhileWalkingTheNativeHashChain`、`CollidingWideKeysReturnTheirActualRecordAddressAndDeletedHeadsAreReclaimed`、`ReclamationRejectsLiveAccessorsAndInvalidatesOldAddressTokens`、Storage再openと退避条件 | 衝突・実アドレスからの再読取り・削除head・Storage退避/再open・生きたaccessor拒否・回収後の旧token拒否を確認済み |
| MV13 通常の中止 | `ConflictingWriterAbortsWithoutFaultingFollowingTransactions`、`MetadataExhaustionAbortsOnlyTheOwningTransaction`、`CancellationBeforeAcceptanceAbortsOnlyThatTransaction`。Failureが空で後続取引が成功することを確認する | 競合・metadata不足・受付前取消の個別中止、現在値不変、Failureなし、後続writer成功を確認済み |
| MV14 一括設置 | `SnapshotStartsBeforeAllParticipantInstallationFinishes`、`SyncIsTheCompletionBoundary`、Gatewayの複数DB試験、TPC-Eの主表・索引digest | Coreの二表設置障壁とSync前のDurable未完了、Gatewayの複数DBの部分公開不在、TPC-Eの主表と索引head/chunkの同時確定・取消を確認済み |
| MV15 公開frontier | `LaterDurabilityCannotPublishPastAnEarlierIncompleteInstallation`の通常/Prepared WAL。先行設置を止めたまま後続の公開が進まないことを確認する | 後続耐久LSNの増加・後続未完了・公開番号0・旧値、先行解放後の全完了を確認済み |
| MV16 確定結果 | `DurabilityBoundaries`、`PreparedTransactions`、`DurableCommitRemainsKnownWhenApplyOrCleanupFails`、Gatewayの各CommitPhase障害。AbortedとCommitted、再open後の結果を分ける | 受付前/準備のみ/最終耐久化後、Sync未完了/失敗、設置/公開障害、応答喪失後の照会、後始末失敗後のCommittedを確認済み |
| MV17 実プロセス終了 | Gatewayの`VersionedProcessKillNeverPublishesOnlyOneDatabaseOrSchema`、TPC-Eの`ProcessTerminationRecoversBusinessRowsAndDriverAtTheSameCommit`。後者は実子プロセスを各4境界で終了し、独立再現driverと全表digest・cursorを照合する | TPC-Eの8条件とGatewayの12境界で、所有した実子プロセスの終了、fault-hit、両DBの値/型・Native再構築・rowversion・driver digest/cursorを確認する。旧アドレスtoken拒否はMV12へ対応 |
| MV18 checkpointと忘却 | `InterruptedCommonCheckpointRetainsWalAndRecoversEveryDatabase`は既存checkpointの有無とDB1/2での中断の4条件で、旧manifest不変・受付停止・再open後の全行とCommittedを照合する。`CoordinatorRecoversAfterCoveredWalSegmentsHaveBeenPruned`、`OutcomesSurviveRestartAndExplicitForget`、`IncompleteReplayPreventsAcceptanceAndCanReleaseAllReaders`を補完条件とする | Gatewayの部分checkpoint、prune後のCovered LSN・Failureなし、忘却後のUnknownと再open、未完了replay時の受付拒否と全件再replay成功を確認済み |
| MV19 native展開 | `GeneratedTransactionQueryIsExpandedInOneCompilation`、`NativeOrdinaryTableQueriesUseFLinqWithoutExplicitTransactionRows`は生成コードのIterate・予算付き結果を検査し、自己変更と取消を実行する。`GroupLifetime`は例外と早期終了も通常LINQへ照合する | 生成物と動作の接続確認済み。Report.Totalは自己変更後のCount=1・Any=true・Contains=trueを検査し、現在値のCountへ置換しない。NativeのJoin/整列も専用経路の生成assertionを持つ |
| MV20 性能比較 | [製品比較報告](20261103-product-mvcc-comparison.md)に通常native・取引FLinq・通常LINQ、独立取引数と一クエリ内worker数、Memory/Storageの測定を分離する | 保存済み2,560 query・960混在・24資源標本と791入力の監査を確認済み。比較queryは整列を含まず、恒等キー/複数整列の専用経路は[追加測定](20261104-ordered-join-comparison.md)の16条件・640標本で別に評価した。数値は報告の固定snapshotでの測定として扱う |
| MV21 TPC-E | `LockstepComparesLegacyAndVersionedQueriesAndReopensTheirCommonCheckpoint`はTL/VL/VFに同じ要求列を与え、32要求以上と保存・再openを確認する。Driver metadataと主表の同時復旧・cursor不整合拒否も別条件で確認する | lockstepと独立digest、主表と索引head/全chunkのcommit/rollback/reopen、observerのUpdate→UndoUpdate、別行writerの重なりと同一行の一方Conflict、hook失敗後のbulk再開を確認済み |
| MV22 Gateway共通確定 | `TwoSchemaChangesAndTheirNativeRebuildRecoverAsOneDecision`は2DBのDDL・DMLとNativeを一つの取引へ入れ、準備/設置/公開障害後の両DBとNativeを再openで照合する。`NativePublicationAndDdlUseTheSameCatalogueSnapshotAndRecoverTogether`は旧Native計画の可視性も確認する | 製品接続も照合済み。Gatewayは一つのPreparedTransactionJournalを所有し、既存方式はCommitExternalAsync、版付き方式は同じjournalを使うCommitOwnedAsyncへ接続する。全DB反映とDDL/Native公開は共通の確定metadataに従う |
| MV23 既存契約 | `SqlTransactionSemantics`のnested/savepoint/XACT_ABORT、`NativeDisconnectedTransaction`の切断取消、`RuntimeSchemaStorageContractTests`の同期取込、既存consumerを変更せず新runtimeへ差し替える互換プローブ | 既存同期取込の独立/親取引への接続、SQLのnested/savepoint/XACT_ABORT/切断取消、旧profile拒否とExclusive再openを対応させた。旧consumerのSHA256を維持し、最新runtimeへの差替え実行も成功。外部SQL Server比較4件は別途skipとして開示する |
| MV24 世代の解放 | `CheckpointReleasesRetiredStoresAndRecoversWithoutTheirCodecs`は旧世代保持中と解放後の親予算、ALC解放後のDeferredファイル削除、codecなし再openを確認する。製品側では強制GCしない | 世代store/予算/ファイルに加え、CollectibleNativeDtoFormattersはglobal formatter cacheから型を除きclient契約を残す。HeldSqlPlanAndServerとReclaimedCodeUnloadsは古いplan/moduleを保持したままALCの弱参照解放を確認済み |
| MV25 後から読むDB | `SqlUsesOneSnapshotAcrossDatabasesAndReplaysTheCommonDecision`はAの100を読んだ後に別scopeがBを250へ更新しても旧scopeがBの200を読む。`DdlKeepsLateReadsOnTheOriginalGenerationAndRestoresAnEarlierGenerationAtASavepoint`はAだけを読んだscopeがBのDDL後も元の行を読み、新列を拒否する。保存点復帰と再openも照合する | 行とDDL世代の両assertionに加え、CoreのLateGenerationSelectionUsesTheOriginalCatalogAndNativePlanSnapshotで後から選ぶNative計画も確認済み |
| MV26 回収中の受付 | `NewRootsWaitThroughDrainCheckpointAndHandleReopeningWhileTheExistingWriterFinishes`は既存writerの子Commit・保存点復帰・確定を進め、排出・保存・再取得の3段階で新rootを待たせる | 確認済み |
| MV27 待機取消 | `AdmissionCancellationAndDeadlineDoNotCancelMaintenanceOrAReplacementReader`は再取得を障壁で止め、取消/期限切れ後に入場0、共通回収継続、代替readerの成功と予算返却を確認する | 確認済み |
| MV28 自身の排出 | `HistoryPressureAbortsTheOldReaderReclaimsAndRequiresAnExplicitNewWriter`は圧迫を起こした更新を中止してから次のwriterを明示開始する。長いwriterは排出期限切れ試験で保持する | 履歴不足を起こしたwriterはHistoryPressureで個別中止し、scope終了で参照を排出してから明示した次のwriterを許す。更新scopeを強制退役せず排出期限切れを返す条件も確認済み |
| MV29 合流と停止 | `MaintenanceReportsTheSharedDrainAndPhysicalIntervalsAndFreezesTheCompletedResult`は同時要求の同じ番号と物理回収1回を確認する。`StoppingDuringDrainOrCheckpointNeverAdmitsWaitingRootsAndOtherDomainsProgress`は停止後の入場0と別domainの確定を確認する | 確認済み |
| MV30 回収失敗 | `PhysicalFailureIsRecordedForReclaimCheckpointAndHandleReopening`の3条件は失敗・PhysicalStarted・Failure・以降の開始拒否を確認する。回収前の不足はMV11と分離する | `RetirementValidatesBeforeAdmissionAndRefusesAmbientWaits`は残予算0の要求拒否後に所属・受付・予算・次の読取りを維持する。物理処理3段階の障害停止も確認済み |
| MV31 回収と計測 | Churnの独立モデル・削除head・metadata忘却・Storage再openと、`MaintenanceReportsTheSharedDrainAndPhysicalIntervalsAndFreezesTheCompletedResult`のDrainWait/PhysicalWork/Totalを対応させる。遅いreader Disposeを待たない条件はMV09 | 計測の合流・固定値・和、遅いDispose非依存を確認済み。現在のChurn report全256記録で区間値が有限/非負、内部合計=排出待ち+物理、呼出し全体時間以下、CPU枠0と資源上限内を直接監査済み |
| MV32 自動ReadOnly | `BoundaryAnalysisCombinesKnownHelpersAndKeepsWritersAndUnknownDelegatesWritable`は属性の重複宣言なしのread helper/alias/block/nestedを生成し、読み取り境界5個と実際のHistoryPressure中止を照合する | 確認済み |
| MV33 A読取/B更新 | 同試験の`WriterPressure`はOrdersへ変更を保持したwriterを履歴圧迫の読取り専用中止にせず、元のAccounts snapshotを確認した後Conflictで全体中止し、Ordersの部分変更がないことを確認する | 確認済み |
| MV34 不明なアクセス | 同試験の別名・既知helper書込み・Action呼出し、`GeneratedReadOnlyEntryRejectsWritesAndLeavesNormalEntryUsable`。不明な呼出しをReadOnlyにしない | 分岐内のalias経由writerを境界解析試験へ追加した。解析器はIOperationの全ChildOperationsを走査する。明示ReadOnlyの更新/追加/削除/Enlistと未対応participantの実行前拒否も確認済み |
| MV35 外側の更新取引 | 同試験は通常の外側scopeでExternalReaderを呼んだ後、同じ表を70へ更新して確定する。読取り呼出しだけでは取引を降格しない | 確認済み |
| MV36 業務処理合成 | `GeneratedTransactionQueryIsExpandedInOneCompilation`は同じtxのPlaceOrder/UsePointsを呼び、Accountsの正常70・後者例外時100と、Ordersの正常1件・例外時0件を確認する。Optimisticでは両処理の後に独立writerを80で確定させ、元の合成取引のConflict・Orders不在・勝者80を確認する | assertionとDebug/Releaseの最終実行を確認済み |
| MV37 外部呼出し | 境界解析試験のUnknown(Action)は更新可能のまま80へ更新・確定し、同じコンパイルで解析可能なqueryのnative/parallel展開も検査する | Unknown(Action)の呼出し先は境界解析にソース情報がないため更新可能で扱う。同じ共通APIで変更後に例外を返すcallbackを追加し、55が残らず直前の80となることを検査する。解析可能なqueryの展開も維持する |
| MV38 CPU共有 | `HostSlotsBoundConcurrentChunksAndAreReturnedAfterCancellationAndExceptions`は上限まで占有し、取消済み待機者が動かず枠が0へ戻ることを確認する。`IoWaitReturnsSlotsAndCallbacksReacquireThemWithoutCreatingNestedWorkers`はI/O待ち中0・別処理の実行・callback再取得・nested処理と最終0を照合する | 確認済み |
| MV39 Join/GroupBy | `JoinAndGroupedTerminalsKeepLinqValuesCallbacksAndEarlyExit`、`EscapingGroupsKeepOrderCallbacksAndLifetimeAcrossTerminals`、`PartitionedLookupBuilder`で通常LINQ/逐次との値・例外・callback列・worker上限を比較する。複数整列・途中投影・比較器factoryも対象 | 自己変更・重複・空入力と複数整列/短絡/寿命を照合済み。Executionの同じ退避試験へ1,024件中1,000件が同じキーになる偏りを追加した。null・OrdinalIgnoreCase・退避後の最初のstringキー保持も別条件で確認 |
| MV40 私有worker | `PrivateSpilledLookupWaitsForEveryWorkerOnHistoryCancellationAndFailure`はnative入力と実際の私有Persisted Lookupを使う。最初のworkerだけ解放した後、producer終了・他worker保持・query未終了・私有領域保持を確認する。履歴中止/worker例外の両条件で全解放後の一時領域・CPU枠0・後続writer成功を照合する | assertionと最終Debug/Releaseの実行・現在入力の一致を確認済み |
| MV41 並列安全性 | DB境界解析と終端比較は不明な利用者callbackのtraceを通常LINQと比較し、worker上限1/3を使う。生成コメントはproducerとworkerへ渡す処理の区別を記録する | trace・worker1/3・生成コメントを確認済み。生成器は不明な比較器をallowParallel=falseとし、CodecRegistryはcustom codec登録があればCanUseParallelCodec=falseを返す。未知の参照graphはIsParallel=false・同一object保持を検査 |
| MV42 既定と同時操作 | `OptimisticRequiresAnExplicitCommonProfileAndFiniteBudgets`、従来DBの取引回帰、Coreのquery操作中の同一scope操作拒否を対応させる。worker内部の実行は公開操作の追加として数えない | 既定ExclusiveとOptimisticのprofile/有限予算必須を確認済み。Coreへ設定を省略したrootの読取り後も独立writerの開始が待ち、root終了後に80を確定する障壁試験を追加した。同一scope操作拒否は内部workerと分離して検査 |

## 実行証拠の扱い

先行段階の複数整列接続後、DB全73件・Execution全84件・共通取引全155件がDebug/Releaseで成功した。Native独立ビルド4条件を加え、計628実行を監査した。25プロジェクト・1,218入力が両構成と監査時の現在ソースに一致し、追加ファイルの有無も照合した。失敗・スキップ・ビルド警告は0。証跡はF:/CodexTemp/Seamless/mvcc-group-execution-20261104/multisort-acceptance-audit.json。比較器factoryとTakeの評価順の修正前には、4条件すべてで通常LINQとのcallback列の相違を再現した。成功数にはこれらの負の対照試験を混ぜない。

恒等キー整列の接続後、DB81・Execution84・共通取引166・Gateway365（361成功/外部比較4skip）・TPC-E41件をDebug/Releaseで強制再コンパイルして実行した。計1,474実行のうち1,466成功・外部比較8skip・失敗0・ビルド警告0。1,349入力・36プロジェクトの構成間/監査時の現在ソース/追加ファイルを照合した。証跡はF:/CodexTemp/Seamless/mvcc-group-execution-20261104/native-join-final-acceptance-audit.json。Nativeの通常Table→Join→恒等キー整列もGatewayの両構成で専用Lookup/予算付き整列を生成・実行した。53種類の終端と24種類の逃避結果は、値・例外型・callback列・寿命を通常LINQと各構成2,020組、計4,040組で比較した。

256行・64周期の現在のChurnは、Memory/Storage・両構成で計8,192変更を監査した。独立モデルと点/走査/Join/GroupBy、削除/再挿入、忘却、再open、区間時間、CPU枠/予算を照合した。証跡はF:/CodexTemp/Seamless/mvcc-final-endurance-20261104/final-endurance-audit.json。旧コンパイル済みconsumerはSHA256を維持して最新runtimeへ差し替え、6引数constructor・6要素deconstruction・Coordinatorの返却値が成功した。証跡はF:/CodexTemp/Seamless/mvcc-final-compat-20261104/compiled-consumer-current.json。

GatewayのskipはPRINT・SourceReadModes・constraint名・型境界値の外部SQL Server比較4件を各構成で実行しなかったもの。MVCCの複数DB/DDL/Native共通確定と実プロセス終了の製品試験は成功数に含む。skipは成功へ読み替えず、共有SQL Serverの設定は変更していない。整列専用経路の追加分は[追加測定報告](20261104-ordered-join-comparison.md)で4,096/16,384行・16条件を比較した。Joinはmanaged割当削減、GroupBy集計後の整列は条件により時間/割当改善、改善前の固定snapshotではgroupを返す複数整列の時間/割当増加が観測された。現在は[所有化の改善後測定](20261104-group-materialization-results.md)を参照する。同じ小規模条件で旧/新を交互実行した32条件・1,280標本を監査し、関連回帰115件がDebug/Releaseとも成功した。先行する2,560 query・960混在・24資源標本は報告に記録した固定snapshotの測定として保持する。先行する628/664実行等も、それぞれ記録された段階の証拠として扱う。
