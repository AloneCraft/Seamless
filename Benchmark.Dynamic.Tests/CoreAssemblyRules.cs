// 試験ランナーの補助処理は通常のLINQを使う。実MSBuildの展開契約は明示opt-inしたfixtureで確認する。
[assembly:global::Seamless.FLinq.LinqIntercept(false)]
