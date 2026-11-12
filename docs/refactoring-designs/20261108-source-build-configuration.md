# 開発ソースのビルド構成選択

## 背景と観察

SourceProjectServiceはSDKを既定のDebug構成で評価し、Compile対象と条件定義を取得している。ReleaseのDLLにはRELEASEが追加され、下書きと条件定義が異なるため成果物検査が拒否することを実行確認した。構成を含む評価の入口が必要である。

## 引き継ぐ契約

共有ソースAPIと所有者接続を使い、ブラウザーはサーバー側のファイルを開く。ファイル同期、個人下書きへの反映、成果物取り込み、プレビュー、明示適用を個別の操作として提供する。比較後の変更や版違いは既存の照合で拒否する。

## 設計と影響範囲

SourceProjectOpenRequestへConfigurationを追加し、既定はDebugとする。SDKの評価へ構成を渡し、同じ構成でCompile対象と条件定義を継続照合する。評価したConfigurationsと現在のConfigurationをSourceProjectDocumentへ返す。現在構成はSourceProjectBindingにも記録し、既存の同期ジャーナルで確定する。

Browser Managerの開発ソースへビルド構成のドロップダウンを追加する。変更前に現在の編集を同期し、切替中はコード入力を読み取り専用にする。同期時の追加入力は保持して切替を保留する。切替後のCompile対象から外れたファイルはディスクに保持する。返された構成・条件定義・対象ソースを表示し、成果物の検査結果を再取得する。

構成名は長さと文字を検査して単独のMSBuildプロパティとして渡す。操作の公開範囲は既存の所有者ソースAPIに従う。対応可否をOwnerSourceConfigurationsV1で通知する。プレビューと明示適用は選択構成の下書き・成果物を既存のハッシュで照合する。

## 検証

通常のSDK設定でDebug・Releaseの条件定義、条件付きCompile対象、マーカーの名前空間表示、構成を戻した場合の編集保持を確認する。無効な構成名は評価前に拒否する。構成切替での未保存編集の保持と同期失敗時の保留を近接試験で確認する。SDK取り込み試験から条件定義固定の回避を外し、両構成のビルド・検査・公開・再起動を通す。ブラウザーで選択・表示とRelease成果物の検査を確認する。

## 合意状態と残る範囲

実施順3・4のソース同期とビルド成果物取り込みの実装として進める。ターゲットは既存のnet10.0契約を使用する。実IDEからの編集・保存・ビルドを含む受入は継続する。

## 確認結果

Debugは関連46件と追加した所有者IPC1件、Releaseは関連47件が成功した。構成別の名前空間・Compile対象・条件定義、Debug・Release・空白を含むQA Test、無効な構成の拒否、旧構成の同期要求の拒否を確認した。共通UIでは切替前の保存、同期中の追加入力による保留、同期失敗時の入力保持、切替から外れたファイルへの追加入力を再評価で復帰させる条件も確認した。

SDKの取り込み試験からDisableImplicitConfigurationDefinesと固定DefineConstantsを外した。通常のDebug・Release設定で評価した下書きへ、それぞれの構成でSDKビルドしたDLLを検査・取り込み・明示適用し、private依存DLL更新と再起動後の値保持まで成功した。既存の言語版・条件定義・元ソース・共有DLL・BundleHashの検査を使う。

WindowsのRelease候補を起動し、ブラウザーで次を確認した。

- .slnxを開き、DebugのBuildModes.DebugとDebug.csを表示する。
- Debug.csを編集してReleaseへ変更し、BuildModes.ReleaseとRelease.csへ切り替わる。
- QA TestでBuildModes.QaとQa.csへ切り替わる。
- Debugへ戻すと変更したValue=99が保持される。
- SDKプロジェクトでReleaseを選び、DB下書きr4・基準v3へ反映する。
- Release DLL・PDB・元ソース・依存20 DLLを検査して取り込む。
- 変更確認と明示適用で公開版v4へ進み、既存行の値7・13が一致する。

APIの証跡は `F:\Seamless\manager-configuration-20261108\green3-results\config-green3.trx`、`owner-results\owner-config.trx`、`release-results\config-release.trx`。ブラウザーの文字列照合は `Work/source-configuration-browser-20261108.json`、画面は `Work/source-configuration-20261108.png`、`Work/source-release-inspected-20261108.png`、`Work/source-release-published-20261108.png`。

## 保存先の制約と残る受入

初回のSDK試験は隔離保存先が深く、Tsavoriteのsnapshot.datのパス長上限に当たって失敗した。DB用の隔離保存先をF:\Seamless\cfg-1108へ短縮し、同じ候補で再実行した。長い保存先への製品側の対応・作成前の診断は保存基盤の残件として追跡する。

今回のブラウザーはWindowsのRelease候補で確認した。実VS／VS Codeの保存・ビルド操作、Linux、配布物全体の受入は継続する。
