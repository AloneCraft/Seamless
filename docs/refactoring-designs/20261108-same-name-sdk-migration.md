# 同名SDK DLLの世代間移行

## 確認した問題

SchemaUiDemoのSDK DLLを通常の同名ビルドで更新すると、公開版v2からの移行がInvalidCastExceptionで失敗した。旧DLLと新DLLはそれぞれ異なるcollectibleなManagedLoadContextに読み込まれるが、移行ヘルパーは両方を同じAssemblyNameで参照し、MigrationLoadContextは旧DLLを先に返していた。型名が一致してもロード元が異なれば別の型となる。1つのロードコンテキストの解決キーは単純アセンブリ名である。[.NETのロードコンテキスト](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)

## 修正方針

- 同名DLLでは、旧DLLだけを参照する読み出しヘルパーを生成する。旧行に対する変換式を評価し、共有型で構成した一行分の値をobject配列として順に返す。
- 新DLLの実Type・ConstructorInfoで構築した型付きデリゲートを用い、新しい行を作成する。既存のTable追加またはUnpublishedSnapshotBuilderへ渡す。デリゲートの生成は移行する表ごとに一回行う。
- 全行を蓄積せず、表ごとのストリームと一行分の配列で接続する。取消・進捗・制約検査・非公開世代の確定は既存の経路を共用する。
- DLL名が異なり列値が共有型で完結する既存の静的移行ヘルパーは再利用する。列値の変換が必要なヘルパーは旧DLLと旧依存DLLへ束縛する。
- 入力SDK DLL、PDB、保管済みのバイト列、保存形式を変更しない。アセンブリ名を利用者に変更させる運用も導入しない。

DB定義DLL固有の型と入れ子のジェネリック列は、保存フィールドを照合して新しい世代の値へ変換する。private/readonlyフィールドと一行内の参照共有・循環を保持する。現在の実装と検証範囲は [DLL世代間の列値移行](20261108-migration-record-values.md) に集約する。

## 検証

同じ単純名で異なる内容を持つ2 DLLを最小のC#から作り、異なるコンテキストへ読み込んだ。既存行とnullable追加列、値変換を通常取引・canonicalスナップショットで確認した。キー衝突・取消で旧行を維持する条件も含め、追加8件と既存移行8件のDebug16件が成功した。初期の再現試験ではcanonical用の取引スキーマが不足していた2条件があり、その結果を移行不具合の証拠へ数えていない。通常取引の2条件で旧ヘルパーのMissingMethodExceptionを観測し、正しいスキーマに修正後の全8条件を確認した。

実GUIで保存済みの同名SDK成果物を再取り込みし、公開版v3への移行が成功した。SDK DLLと公開module.dllのSHA256が一致し、既存12行と変更後の関数結果45、他の所属先の84・124を照合した。再起動後の全12問い合わせも一致した。証跡は `F:/Seamless/schema-20261108/same-name-green2-results/migration.trx` と `schema-manager-repeat-results-20261108.json`。

最終候補ではDebug・Release各38件に同名移行8条件と既存移行8条件を含めて成功した。続く実GUIのv4適用では画面再読込なしで全12問い合わせが一致した。正常停止・再起動後も、直下関数46、dbo0関数84、dbo1関数124と既存行が一致した。証跡は `schema-manager-v4-results-20261108.json`、`schema-manager-v4-restart-results-20261108.json`、`v4-artifact-identity.json`。

移行ヘルパーと新旧DBのコンテキストは既存のUnload／Disposeへ接続する。GC後の弱参照で実際の回収を測る試験は未実行。列型の保存フィールドが変わり、現行の列変換式で対応できない場合はMigrationRequiredとして明示変換を要求する。キーなし表のcanonical非公開スナップショット構築も未対応である。
