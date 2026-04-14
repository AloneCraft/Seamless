# Copilot Instructions

## Coding Rule
if(condition){
	// code
}
の時
if(condition){
	// code
}else{
}
とelseを入れること。理由はカバレッジのため。elseがないと、ifの条件がfalseのときのコードが存在しないことになり、カバレッジが100%にならないため。

## User Preferences
- Listに追加してはいけない。Tsavoriteを使っている理由はメモリに収まらないサイズのコレクションを扱うためであり、Iterator/Enumeratorで全件をList等のメモリ上コレクションに蓄積するのはNG。チャンク単位（固定長配列）でバッファリングし、少しずつ返す方式にすること。
- C# のジェネリック関連コンパイルエラーでは型変換だけでなく where 制約の不一致を優先して確認すること。
- ベースとなる機能は最上位クラスに実装し特定のクラスのインスタンス化はfluent factoryパターンで実装する
- 制約でオーバーロードしたいので拡張メソッドで行う
- 静的ストラテジーでunmanaged,非unmanaged動作を切り替える
- 切り替えるのは
	unmanaged(Fixed) TKey,unmanaged(Fixed) TValue
	unmanaged(Fixed) TKey,managed(Variable) TValue
	managed(Variable) TKey,unmanaged(Fixed) TValue
	managed(Variable) TKey,managed(Variable)TValue
- CreateUnmanaged 系の新規経路では識別子に `Unmanaged` を付ける命名方針を優先する。
