using System.Text;
using System.Text.RegularExpressions;

namespace Seamless.Database.Query;

public sealed record SqlScriptBatch(string Text,int StartOffset,int StartLine);

/// <summary>SQL Server クライアント用 GO 区切り。SQL 構文解析の代替ではない。</summary>
public static class SqlScriptBatches{
    private const int MaxUtf8Bytes=1024*1024;
    private enum Context{Normal,String,Bracket,Identifier,LineComment,BlockComment}
    private static readonly Regex Separator=new(@"\A[ \t]*GO[ \t]*(?:--[^\r\n]*)?\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private static readonly Regex Repeat=new(@"\A[ \t]*GO[ \t]+[0-9]+[ \t]*(?:--[^\r\n]*)?\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);

    /// <summary>Text は区切り行を除く原文部分、offset は UTF-16、line は 1 始まり。</summary>
    public static IReadOnlyList<SqlScriptBatch>SplitSqlServer(string sql){
        ArgumentNullException.ThrowIfNull(sql);
        if(sql.Length>MaxUtf8Bytes||Encoding.UTF8.GetByteCount(sql)>MaxUtf8Bytes){throw new SqlQueryException("SQL script exceeds 1 MiB UTF-8 limit.");}
        var batches=new List<SqlScriptBatch>();
        var context=Context.Normal;
        var depth=0;
        var line=1;
        var batchStart=0;
        var batchLine=1;
        var lineStart=0;
        while(lineStart<sql.Length){
            var lineEnd=lineStart;
            while(lineEnd<sql.Length&&sql[lineEnd]!='\r'&&sql[lineEnd]!='\n'){lineEnd++;}
            var next=lineEnd;
            if(next<sql.Length){
                if(sql[next]=='\r'&&next+1<sql.Length&&sql[next+1]=='\n'){next+=2;}else{next++;}
            }
            if(context==Context.Normal){
                var text=sql.Substring(lineStart,lineEnd-lineStart);
                if(Repeat.IsMatch(text)){throw new SqlQueryException("GO repeat count is not supported.",line:line);}
                if(Separator.IsMatch(text)){
                    AddBatch(batches,sql,batchStart,lineStart,batchLine);
                    batchStart=next;
                    batchLine=line+1;
                    lineStart=next;
                    line++;
                    continue;
                }
            }
            for(var i=lineStart;i<lineEnd;i++){
                var c=sql[i];
                var following='\0';
                if(i+1<lineEnd){following=sql[i+1];}
                switch(context){
                    case Context.Normal:
                        if(c=='\''){context=Context.String;}
                        else if(c=='['){context=Context.Bracket;}
                        else if(c=='"'){context=Context.Identifier;}
                        else if(c=='-'&&following=='-'){context=Context.LineComment;i++;}
                        else if(c=='/'&&following=='*'){context=Context.BlockComment;depth=1;i++;}
                        break;
                    case Context.String:
                        if(c=='\''){if(following=='\''){i++;}else{context=Context.Normal;}}
                        break;
                    case Context.Bracket:
                        if(c==']'){if(following==']'){i++;}else{context=Context.Normal;}}
                        break;
                    case Context.Identifier:
                        if(c=='"'){if(following=='"'){i++;}else{context=Context.Normal;}}
                        break;
                    case Context.BlockComment:
                        if(c=='/'&&following=='*'){depth++;i++;}
                        else if(c=='*'&&following=='/'){depth--;i++;if(depth==0){context=Context.Normal;}}
                        break;
                    case Context.LineComment:
                        i=lineEnd;
                        break;
                }
            }
            if(context==Context.LineComment){context=Context.Normal;}
            lineStart=next;
            if(next>lineEnd){line++;}
        }
        if(context!=Context.Normal){throw new SqlQueryException("Unterminated SQL quote or block comment.",line:line);}
        AddBatch(batches,sql,batchStart,sql.Length,batchLine);
        return batches;
    }
    private static void AddBatch(List<SqlScriptBatch>batches,string sql,int start,int end,int line){
        var text=sql.Substring(start,end-start);
        if(!string.IsNullOrWhiteSpace(text)){batches.Add(new(text,start,line));}
    }
}
