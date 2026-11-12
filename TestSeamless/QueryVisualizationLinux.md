# クエリ可視化のLinux候補を固定する手順

## 固定前

保存基盤を含む担当変更を保存し、各担当から編集停止の通知を受けてから候補を固定する。現在の作業ツリーを直接参照したLinuxビルドは行わない。通常利用のPID90899と、そのDB・ブラウザープロファイルは継続利用する。

既存`Invoke-MvccValidation.ps1`が作る`source`と`source-manifest.json`の組を再利用する。対象プロジェクトの参照閉包、Shared、ルートのビルド設定をコピーし、各ファイルのSHA-256を持つ形式を使う。保存基盤の変更後に作った最終候補の組を指定し、旧受入のソースを再利用しない。スナップショット作成工程と、その工程で実行する試験は親の実行枠で調整する。

スナップショットを作成してからは、その`source`に対する編集・ビルド出力・追加ファイル作成を行わない。通常のG作業ツリーでの後続編集と、この固定候補は分離できる。

## 準備

下記の変数には実在する最終候補の保存先を入れる。`probe/Program.cs`と`.csproj`は親の実Windows受入で使った版を維持する。

```powershell
$runRoot='F:/Seamless/visualization-20261109-0e63c1c7'
$sourceSnapshot='<最終候補のF:/Seamless配下/source>'
$sourceManifest='<同じ候補のsource-manifest.json>'
& ./TestSeamless/Prepare-QueryVisualizationLinux.ps1 `
    -WindowsRunRoot $runRoot `
    -SourceSnapshot $sourceSnapshot `
    -SourceManifest $sourceManifest `
    -Configuration Release
```

準備処理は一意な`linux-<nonce>`を作り、プローブ・設定・検証スクリプトとハッシュを記録する。ProjectReferenceは固定したFスナップショットの`WindowsServer.csproj`へ接続する。準備処理はビルド・WSL起動・サーバー起動を行わない。

## 実行枠の承認後

準備結果の`LinuxRun`を使い、既存Ubuntuのkazuhとして順に実行する。

```bash
bash <LinuxRun>/QueryVisualizationLinux.sh prepare
bash <LinuxRun>/QueryVisualizationLinux.sh build
bash <LinuxRun>/QueryVisualizationLinux.sh create
# 別の所有CLIから同じrunを指定して正常停止する
bash <LinuxRun>/QueryVisualizationLinux.sh stop create
bash <LinuxRun>/QueryVisualizationLinux.sh open
bash <LinuxRun>/QueryVisualizationLinux.sh stop open
```

`create`と`open`はホスト終了まで待つ。停止要求は同じnonceとphaseの`ready.json`、PID、UID、cgroupを照合してから送る。所有者の秘密起動URLはホーム内0700領域に保存し、公開ログへ表示しない。

ビルドは共通CPU0–7、user scopeのMemoryHigh1400M・MemoryMax1700M、MSBuild単一ワーカーを使う。既存SDKとNuGetキャッシュを再利用し、出力はnonce固有のartifacts/runtimeへ分離する。ホストは別の所有scopeでMemoryHigh850M・MemoryMax1200Mとする。

## 合格に必要な証拠

- 固定ソースの全ファイル集合とSHA-256を、準備時・ビルド直前・直後で照合する。差異があれば候補を採用しない。
- プローブ・プロジェクト・global.json・検証スクリプトのハッシュ、SDK版、出力パスを保持する。
- ネイティブ5資産とdepsの階層、公開静的資産、WindowsのContentRoots混入がないことを確認する。実HTTPのCSS・JS取得とDOM操作も別途確認する。
- 通常利用PID90899の開始時刻・UID・実行ファイル・cgroupが各工程前後で一致する。
- 通常APIの実結果と実計画の版・ソース対応を確認し、同じ所有ブラウザーで表示・追加操作・IME合成イベントを確認する。物理IMEは別の受入とする。
- create後の停止、openでの再起動、定義と行・履歴の保持、所有scopeの正常終了を確認する。

この手順の準備スクリプトは構文確認済み。保存基盤を含む最終候補の固定・Linuxビルド・実行受入は未実施。
