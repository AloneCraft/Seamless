# FLinq 生成コードの可読性改善

## 目的と合意
ユーザーの実装指示に基づく。元のラムダ引数名を可能な限り再利用し、インスタンスメンバーは this. で統一する。結果・評価回数・例外・列挙順を変えず、実際に生成した改善前後のコードで比較する。既存の需要解析と理想コード比較の仕様を引き継ぐ。

## 設計
- 断片から生成名と希望するラムダ引数名の対応を登録し、最終的な名前割当時に衝突を検査する。元のローカル・引数・メンバーと、生成コードに残ったラムダ名は保護する。
- 局所関数も役割に基づく名前へ変更する。外部メソッド・型・UnsafeAccessor 名は変更しない。
- GroupBy の要素選択子がない場合、入力要素をそのまま集約へ渡し、不要な default 初期化・object 経由キャスト・コピーを生成しない。要素選択子がある場合は Count でも実行を維持する。
- this. の後処理は構造体に加え生成クラスのインスタンスメンバーも対象にする。引数やローカルの隠蔽は維持する。
- 計算の破棄は例外・副作用を失わせない場合に限定する。Join の結果選択子など、戻り値が不要でも評価を要求される計算は消さない。

## 検証
名前衝突・局所関数・this. の単体検証、生成器再ビルド、同じクエリのソース差分・System.Linq との実行比較、関連する評価回数と SourceGenerator 回帰テストを行う。

## 確認結果
- 生成器 Release / netstandard2.0 のビルド成功。警告0・エラー0。
- SourceGenerator・需要解析の実行テスト・遅延評価・Range の引数検査を含む関連回帰404件成功（readability-final.trx）。
- 利用者の `_` を書き換えない補正後、命名・GroupBy・需要解析・遅延評価・Range の重点確認120件成功（readability-discard.trx）。両者は重複を含むため件数を合算しない。
- 実際の生成コードを実行して、Join / Join ToArray / GroupJoin / OrderBy ThenBy / GroupBy 全件・First・Take・Last / OrderBy Where Any の9ケースで System.Linq と結果一致。
- 実行用の小さな再現例でも、`Run(int[] source,bool _)` の false が GroupBy 集計後も false のままで、結果が `False:2,2` になることを確認。
- 改善前後の生ソースは `Work/readability/before.cs` と `after.cs`、読み比べは `docs/flinq-ideal/generated-readability-examples.md`。同一クエリを強制再ビルドした実出力。
- 今回は可読性と意味の維持を検証した。実行速度改善の数値は測定していない。
