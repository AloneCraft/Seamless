# 機能の棚卸しを続けるためのプロンプト

別のチャットに、下の枠の中をそのまま貼り付けて使う。

---

```text
G:\GitHub\Seamless で、「指示に基づかない機能の棚卸し」を続けてください。返答はすべて平易な日本語で、パスはドライブ文字から始まるフルパスで書いてください。

## まず読むもの
1. G:\GitHub\Seamless\AGENTS.md（とくに「機能を追加するときの規則 (2026-11-12)」）
2. G:\GitHub\Seamless\docs\refactoring-designs\20261111-2330-feature-inventory.md（棚卸しの背景・進め方・Seamless.Database の結果・判定の記録）
3. G:\GitHub\Seamless\docs\refactoring-designs\20261112-0017-database-access-model.md（今回の作り直しで決めた設計と不変条件 I1〜I12）
4. 根拠となる利用者の指示：G:\GitHub\Seamless\docs\refactoring-designs\20260702-fable5-seamless-database-flinq-benchmark-overhaul-prompt.md と、同じフォルダーのほかの設計書・プロンプト

## 背景
ビューの遅延評価のように、利用者の指示にない機能をAIが細部まで勝手に作っていたことが分かりました。Seamless.Database の公開型は棚卸し済みです。残りのプロジェクトについても、同じ方法で確認します。

## 対象（この順で）
Seamless.Management、Seamless.Management.Server、Seamless.Management.Components、Seamless.Transactions、Seamless.Database.Gateway、Seamless.Linq、Seamless.FLinq.Generator、Seamless.Execution、Seamless.Collections*
DatabaseContainer のように、1つの型に多くの機能が集まっているものは、公開メンバー単位で分けて確認してください。

## 進め方
1. プロジェクトごとに、公開型（必要なら公開メンバー）を機械的に集める。導入したコミットは `git log --follow --diff-filter=A` で調べる。利用箇所も数える。一時的な出力は F:\Seamless\ の下に置く。
2. それぞれの機能について、導入したときの利用者の指示に根拠があるかを、設計書・プロンプト・コミット本文で確認する。「型や名前はイメージにあるが、細かい動きは根拠がない」のように、根拠のある部分とない部分を分けて書く。
3. 結果は G:\GitHub\Seamless\docs\refactoring-designs\20261111-2330-feature-inventory.md に、プロジェクトごとの表として追記する。列は「型｜機能｜導入｜指示の根拠｜判定」で、判定は「未」にする。
4. 判定は利用者が1件ずつ行う。根拠のないものは、残す・作り直す・削除するの選択肢と、それぞれの影響を短く示して、質問は1つずつしてください。

## 守ること
- 判定が済むまでは、コードの動作を変えない。棚卸しは調査と文書の更新だけにする。
- 推測と、確認できた事実を分けて書く。過去のAIの説明やコメントは、根拠として扱わない。
- 指示が曖昧なときは、仕様を勝手に決めずに確認する。
- 未コミットの変更を失わせない（stash・reset・ブランチの切替をしない）。コミットとpushは、頼まれるまでしない。
- 自分が起動したプロセスだけを止める。起動リンク・Cookie・トークンは出力しない。
- 重い処理は AGENTS.md の Comparison Execution Policy（最大8論理CPU、TestSeamless\Invoke-LocalProcess.ps1）に従う。
```
