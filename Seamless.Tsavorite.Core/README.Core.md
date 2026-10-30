# Seamless.Tsavorite.Core

Microsoft Garnet 2.2.1 の Tsavorite Core に復旧ページの寿命を修正する局所パッチを適用した配布 project です。上流 commit は `b0255a31de95b1e903ee187feb59ceb04819a639` です。

Core の assembly identity は `Tsavorite.core, Version=2.2.1.0, Culture=neutral, PublicKeyToken=b93f59e18c9c8d6b` を維持します。PackageId は `Seamless.Tsavorite.Core`、package version は Seamless 共通の `Version` を使います。元の `Microsoft.Garnet` package を上書きしません。

復旧時に resident head より前のページを index 再構築のために一時的に読み込む経路で、undo flush の完了後にそのページを解放します。これにより、回復した tail より先のページへ循環 buffer のスロットを再利用するとき、旧 record が残存することを防ぎます。復旧ページの寿命修正・回復I/O所有権（Local/RandomAccess/Native）・初期走査frame/Object寿命管理（Core43、C# 259ファイル）を含みます。差分は `recovery-head-pages.patch` および `core43-recovery-and-scan.patch`、全ファイルの SHA256 は `UPSTREAM-PROVENANCE.json` に記録しています。

`Upstream/` の C# 259ファイル（上流255 + Core43内部補助4）、署名 key、MIT license、`.editorconfig` は固定入力から byte 単位でコピーしています。上流 source の整形や日本語化は行いません。元 project の条件付き定義と全 InternalsVisibleTo は `Upstream.Core.Items.props` に保持しています。上流の project 全体の props は import せず、対象 framework は net10.0 に固定します。

元の `Microsoft.Garnet [2.2.1]` は `PrivateAssets=all; ExcludeAssets=all` の取得専用です。取得した元 `Garnet.common.dll`、`Tsavorite.devices.AzureStorageDevice.dll` と対応する XML、native_device 10ファイルだけを明示選択します。これらの binary は repository に vendor せず、package restore 先から出力と nupkg にコピーします。Core 自身は Common を compile 参照しません。通常の公開 package 依存は `Microsoft.Extensions.Logging [10.0.5]` と `Azure.Storage.Blobs [12.27.0]` です。

NuGet 利用時はこの package、またはこの package に依存する `Seamless.Collections.Tsavorite` を参照します。nupkg の `lib/net10.0/` には Core、Common、Azure device の3 assembly を同梱します。native は `runtimes/<rid>/native/` に保持し、buildTransitive targets が固定 resolver に必要な相対 tree を build と publish 出力にもコピーします。

source 利用時は修正済み Core の ProjectReference と取得専用の元 package、Common と Azure device への明示 Reference を組み合わせます。元 package を通常参照して旧 Tsavorite.core の compile/runtime asset が同時に有効になった場合、`RejectOriginalTsavoriteCoreAssets` が明示的に失敗させます。取得専用参照の metadata node 自体は禁止しません。

上流 MIT license は `Upstream/LICENSE`（nupkg では `licenses/LICENSE`）です。署名 key は nupkg へ含めません。固定 source・support/native の hash 照合と、正式 wrapper の build/pack/runtime の検証結果は別々に扱います。Linux、ARM64、musl 向け native の同梱は、それらの環境で実行確認したことを意味しません。
