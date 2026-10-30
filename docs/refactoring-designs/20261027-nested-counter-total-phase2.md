# 入れ子counter総数のphase2境界と現行定義

現行定義は `visible-query-lambdas-entry-total-v2`。throwした可視query lambdaも入口で1回、未到達/null本体は0とする。計測契約・追加API・型・検証手順は [現行定義](20261027-nested-counter-total.md) を参照する。

この段階で導入した可視query範囲、同テキスト別scopeのbinding、匿名型/block、任意callback内部の可視queryとopaque境界の扱いは維持する。既存の正常値・例外・trace・Dispose期待と最適化の選択を変えない。例外時のscope除外はv2で解除し、独立入口oracleで検査する。

## 履歴

v1当時の設計と観測は [20261027-nested-counter-total-phase2-v1.md](../history/refactoring-designs/20261027-nested-counter-total-phase2-v1.md) に保存した。そこにある例外timing未統一/API不変更/未決の記述は当時の記録であり、現在のv2を制約しない。旧artifactをv2実測へ再ラベルしない。
