# AGENTS.md

GPT/Codex 系 AI エージェントは、作業前に [AI_SYNTAX_RULES.md](AI_SYNTAX_RULES.md) を参照してください。

このリポジトリでは、通常の C# フォーマットよりも空白を詰める構文規則を採用しています。`.editorconfig` と `AI_SYNTAX_RULES.md` の両方に従ってください。
## Coding Rule
インデントは全てスペースにする
全ての識別子と記号間、記号と記号間、") {","> {"といった括弧の間のスペースは無し、カンマ前後のスペースは無し
- K&Rをsベースに
- 波括弧位置を同一行
- if elseで片方にしか処理がなくても{}は省略しない
    if(condition){
        処理
    }else{
    }
    if(condition){
    }else{
        処理
    }
- 三項演算子
    三項演算子は片方しか通過しなくてもカバレッジできないのでif{}else{}を必ず使うこと
- 制御文は省略して
    if(condition)
        continue;
- 記号の前後のスペースはなくして
    a + b
    a+b
- コロンの前後の間のスペースはなくして
    class A : IDispose
    class A:IDispose
- カンマの前後の間のスペースはなくして
    int a ,int b, int c
    int a,int b,int c
- 記号と識別子の間のスペースはなくして
    List<int> a
    List<int>a
- 括弧の間のスペースはなくして
    ) {
    ){
    > {
    >{
- 改行の前にスペースがあったら削除して
    break;
    break;
