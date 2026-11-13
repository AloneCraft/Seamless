# Seamless.Tsavorite.Core

Microsoft Garnet 2.2.8 の Tsavorite Core に、復元時のページの後始末を直す小さなパッチを1つ当てた配布用 project です。上流のコミットは `5cc8f3f50aa57ed694dfda928e4467593e073656`(タグ v2.2.8)です。

Core の assembly identity は `Tsavorite.core, Version=2.2.8.0, Culture=neutral, PublicKeyToken=b93f59e18c9c8d6b` のままです。PackageId は `Seamless.Tsavorite.Core`、package version は Seamless 共通の `Version` を使います。元の `Microsoft.Garnet` package は上書きしません。

## 直している不具合

開き直し(チェックポイントからの復元)では、索引を作り直すために、メモリに残す範囲(resident head)より前のページも一度読み込みます。2.2.8 はそのページを使い終わっても解放しません。解放されないページの入れ物(循環バッファの枠)は、あとでログの末尾がその枠に戻ってきたときに消されずに再利用されます。そのため、ページの末尾の空きに古いバイトが残り、次の保存(スナップショット)に混ざります。その保存を開き直すと、レコードの長さがページをはみ出していると読まれて失敗します(Release では `RecoverFromPage` の NullReferenceException)。

パッチは、索引の作り直しだけに読んだページを、取り消しの書き出しが終わってから解放します。差分は `recovery-head-pages.patch`(Recovery.cs の6行の追加)です。同じ修正は 2.2.1 向けにも当てていました。2.2.1 向けに当てていたもう1つの大きなパッチ(Core43)は、2.2.8 で復元処理が書き直されたため持ち込んでいません。

確認は `Benchmark.TpcC.Tests.TpcCLockstepRunner.Run`(保存と開き直しを繰り返す)で行いました。パッチなしでは毎回失敗し、パッチありでは通ります。

## 中身と参照のしかた

`Upstream/` には、上流の Core の C# 255ファイル、署名 key、MIT license、`.editorconfig` を byte 単位でコピーしています。上流 source の整形や日本語化はしません。元 project の条件付き定義と全 InternalsVisibleTo は `Upstream.Core.Items.props` に写しています。対象 framework は net10.0 に固定します。全ファイルの SHA256 は `UPSTREAM-PROVENANCE.json` に記録しています。

元の `Microsoft.Garnet [2.2.8]` は `PrivateAssets=all; ExcludeAssets=all` の取得専用です。取得した元の `Garnet.common.dll`、`Tsavorite.devices.AzureStorageDevice.dll` と対応する XML、native_device 10ファイルだけを選んで使います。これらの binary は repository に入れず、package の復元先から出力と nupkg にコピーします。Core 自身は Common を compile 参照しません。

source から使うときは、この project への ProjectReference と、取得専用の元 package、Common と Azure device への明示 Reference を組み合わせます。元 package を通常参照して元の Tsavorite.core が同時に有効になった場合は、`RejectOriginalTsavoriteCoreAssets` がビルドを失敗させます。

上流の MIT license は `Upstream/LICENSE`(nupkg では `licenses/LICENSE`)です。署名 key は nupkg に含めません。Linux、ARM64、musl 向け native の同梱は、それらの環境で実行確認したことを意味しません。
