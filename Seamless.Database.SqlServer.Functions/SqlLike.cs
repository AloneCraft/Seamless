using System.Globalization;
namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// LIKE の型合わせ。% は任意の長さ、_ は1文字、[abc] [a-c] [^abc] は文字の集まり、ESCAPE の文字の直後は普通の文字。
/// 文字の一致は照合順序に従う。LIKE 演算子と PATINDEX が使う。
/// </summary>
public static class SqlLike{
    private readonly record struct Token(char Kind,string Value,bool Negated);
    /// <summary>value 全体が pattern に合うか。どちらかが NULL なら NULL。末尾の空白は value 側だけ無視する。</summary>
    public static bool? IsMatch(string? value,string? pattern,SqlCollation collation,char? escape=null){
        if(value is null||pattern is null){
            return null;
        }
        var tokens=Parse(pattern,escape);
        return Matches(TextElements(value.TrimEnd(' ')),0,tokens,collation);
    }
    /// <summary>pattern が value の中で最初に合う位置(1始まり)。合わなければ 0。PATINDEX の本体。</summary>
    internal static int FirstMatch(string value,string pattern,SqlCollation collation){
        var tokens=Parse(pattern,null);
        var elements=TextElements(value);
        // 先頭の % は「どこから始まってもよい」の意味なので外し、始まりの位置を1つずつ試す。
        var leading=0;
        while(leading<tokens.Count&&tokens[leading].Kind=='%'){
            leading++;
        }
        if(leading==0){
            if(Matches(elements,0,tokens,collation)){
                return 1;
            }
            return 0;
        }
        var rest=tokens.GetRange(leading,tokens.Count-leading);
        if(rest.Count==0){
            return 1;
        }
        var position=1;
        for(var start=0;start<elements.Count;start++){
            if(MatchesPrefix(elements,start,rest,collation)){
                return position;
            }
            position+=elements[start].Length;
        }
        return 0;
    }
    private static List<Token> Parse(string pattern,char? escape){
        var tokens=new List<Token>();
        for(var i=0;i<pattern.Length;i++){
            var c=pattern[i];
            if(escape is char escaped&&c==escaped){
                if(i+1>=pattern.Length){
                    throw new SqlFunctionException("The escape character in a LIKE pattern must be followed by a character.",8179);
                }
                tokens.Add(new('L',pattern[++i].ToString(),false));
                continue;
            }
            if(c=='%'){
                tokens.Add(new('%',"",false));
                continue;
            }
            if(c=='_'){
                tokens.Add(new('_',"",false));
                continue;
            }
            if(c=='['){
                var searchFrom=i+2;
                if(searchFrom>pattern.Length){
                    searchFrom=pattern.Length;
                }
                var close=pattern.IndexOf(']',searchFrom);
                if(close<0){
                    tokens.Add(new('L',"[",false));
                    continue;
                }
                var body=pattern.Substring(i+1,close-i-1);
                var negated=body.StartsWith('^');
                if(negated){
                    body=body[1..];
                }
                tokens.Add(new('S',body,negated));
                i=close;
                continue;
            }
            tokens.Add(new('L',c.ToString(),false));
        }
        return tokens;
    }
    private static List<string> TextElements(string text){
        var list=new List<string>();
        var enumerator=StringInfo.GetTextElementEnumerator(text);
        while(enumerator.MoveNext()){
            list.Add((string)enumerator.Current);
        }
        return list;
    }
    private static bool InSet(Token token,string character,SqlCollation collation){
        var body=token.Value;
        var matched=false;
        for(var i=0;i<body.Length&&!matched;i++){
            if(i+2<body.Length&&body[i+1]=='-'){
                matched=collation.Compare(character,body[i].ToString())>=0&&collation.Compare(character,body[i+2].ToString())<=0;
                i+=2;
            }else{
                matched=collation.Compare(character,body[i].ToString())==0;
            }
        }
        return matched!=token.Negated;
    }
    private static bool Step(Token token,string element,SqlCollation collation)=>token.Kind switch{
        '_'=>true,
        'L'=>collation.Compare(element,token.Value)==0,
        'S'=>InSet(token,element,collation),
        _=>false
    };
    // 位置ごとに「型のどこまで合ったか」の集合を進める(後戻りしない)。
    private static HashSet<int> Advance(HashSet<int> states,List<Token> tokens,string element,SqlCollation collation){
        var next=new HashSet<int>();
        foreach(var state in states){
            if(state>=tokens.Count){
                continue;
            }
            var token=tokens[state];
            if(token.Kind=='%'){
                next.Add(state);
                continue;
            }
            if(Step(token,element,collation)){
                next.Add(state+1);
            }
        }
        Expand(next,tokens);
        return next;
    }
    private static void Expand(HashSet<int> states,List<Token> tokens){
        var queue=new Queue<int>(states);
        while(queue.Count>0){
            var state=queue.Dequeue();
            if(state<tokens.Count&&tokens[state].Kind=='%'&&states.Add(state+1)){
                queue.Enqueue(state+1);
            }
        }
    }
    private static bool Matches(List<string> elements,int start,List<Token> tokens,SqlCollation collation){
        var states=new HashSet<int>{0};
        Expand(states,tokens);
        for(var i=start;i<elements.Count;i++){
            states=Advance(states,tokens,elements[i],collation);
            if(states.Count==0){
                return false;
            }
        }
        return states.Contains(tokens.Count);
    }
    // start から始まる部分が型の全体に合う区間を持つか(末尾は自由)。
    private static bool MatchesPrefix(List<string> elements,int start,List<Token> tokens,SqlCollation collation){
        var states=new HashSet<int>{0};
        Expand(states,tokens);
        if(states.Contains(tokens.Count)){
            return true;
        }
        for(var i=start;i<elements.Count;i++){
            states=Advance(states,tokens,elements[i],collation);
            if(states.Count==0){
                return false;
            }
            if(states.Contains(tokens.Count)){
                return true;
            }
        }
        return false;
    }
}
