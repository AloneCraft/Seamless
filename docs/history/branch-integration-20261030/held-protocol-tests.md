# 未完成のSQLプロトコル受入テストの保留

取り込み元で追加された34件を、統合候補864d591cの18パッケージを専用フィードから復元して実行した。30件成功、4件失敗、スキップ0件だった。4件を修正済みとは扱わず、未完成の変更を保留する合意に従い、今回の完成範囲から分離した。元worktreeは変更していない。

| 保留テスト | 観測結果・未実装範囲 |
|---|---|
| PrimaryKeyUpdateMovesRowAndRejectsCollision | SQL parserが主キー更新を明示拒否する。adapterのキー移動・衝突時原子性は未実装。 |
| DuplicateRowsSurviveRollbackAndRestart | 非公開のsynthetic IDがSQL INSERTで採番されず、重複キーになる。再起動・rollbackを含む採番契約は未実装。 |
| JapaneseVarcharRpcAndDatabaseSwitch | SqlClientの明示LocaleIdが生成するtds:00000411:00をQuery側の照合順序変換が未対応。 |
| UdtColumnsJoinSysTypesOverSqlClient | fixtureに含まれるtimestampをsys.columnsの型表が未対応とし、UDT照合まで進めない。 |

主要4実装ファイルは承認元470ファイルのソースおよび元worktreeと改行正規化後に一致し、統合で処理が消えた事象ではない。sys.columnsのtimestamp型表も取り込み元から未提供だった。元設計書も新しいTDS型・照合順序の最終パッケージ検証を未完了と記録している。

`held-protocol-tests.patch` はこの4メソッドを復元するパッチで、元の内容は承認元スナップショットとGit保全参照にも残る。復元コマンドは `git apply --unidiff-zero docs/history/branch-integration-20261030/held-protocol-tests.patch`。再開するときは対応実装と必要な設計・原子性・互換性の検証を行い、パッチを戻して4件を成功させる。フィルターやSkipで成功扱いにしていない。

失敗TRX・stdout・source identity: `F:/CodexTemp/SeamlessBranchIntegration-20261030T053031Z/checks/testseamless-final-release-02/`。
