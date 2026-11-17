using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Saxon.Api;
namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// xml 型の nodes() が返す1つの節。同じ文書の中の位置を保つので、ここから親や兄弟もたどれる。
/// 等しさは同じ節かどうか(参照)で決める。値が同じでも別の節なら別の行になる(本物と同じ)。
/// </summary>
public sealed class SqlXmlNode{
    internal SqlXmlNode(XdmNode node){
        this.Node=node;
    }
    internal XdmNode Node{get;}
    /// <inheritdoc/>
    public override string ToString()=>this.Node.OuterXml;
}
/// <summary>
/// xml 型のメソッド(value・query・exist・nodes)。XQuery は SaxonCS-HE で評価する。
/// XQuery の先頭の名前空間の宣言(declare namespace・declare default element namespace)はそのまま使える。
/// WITH XMLNAMESPACES は、翻訳器が同じ宣言を XQuery の先頭へ足して渡す。
/// </summary>
/// <remarks>
/// 型付きの XML(XML スキーマの集まりに結び付いた列)も、型の無い XML として読む。値の文字列は文書に書かれたとおり。
/// SQL Server だけの関数(sql:column・sql:variable)と、modify() は扱わない。
/// </remarks>
public static class XmlFunctions{
    private static readonly Processor processor=new(false);
    private static readonly ConcurrentDictionary<string,XQueryExecutable> queries=new(StringComparer.Ordinal);
    // 同じ行の同じ列に何度も value() を呼ぶので、読んだ文書を文字列ごとに使い回す。
    private static readonly ConditionalWeakTable<string,XdmNode> documents=new();
    /// <summary>value()。XQuery の結果が空なら NULL、1つならその文字列。2つ以上なら 2389(本物はコンパイル時の誤り)。</summary>
    public static string? Value(string? xml,string query){
        if(xml is null){
            return null;
        }
        return Single(Evaluate(Document(xml),query),query);
    }
    /// <summary>nodes() が返した節に対する value()。</summary>
    public static string? Value(SqlXmlNode? node,string query){
        if(node is null){
            return null;
        }
        return Single(Evaluate(node.Node,query),query);
    }
    /// <summary>query()。結果の節をつないだ XML の文字列。xml が NULL なら NULL。</summary>
    public static string? Query(string? xml,string query){
        if(xml is null){
            return null;
        }
        return Serialize(Evaluate(Document(xml),query));
    }
    /// <summary>nodes() が返した節に対する query()。</summary>
    public static string? Query(SqlXmlNode? node,string query){
        if(node is null){
            return null;
        }
        return Serialize(Evaluate(node.Node,query));
    }
    /// <summary>exist()。結果が空でなければ true。xml が NULL なら NULL。</summary>
    public static bool? Exist(string? xml,string query){
        if(xml is null){
            return null;
        }
        return Evaluate(Document(xml),query).Count>0;
    }
    /// <summary>nodes() が返した節に対する exist()。</summary>
    public static bool? Exist(SqlXmlNode? node,string query){
        if(node is null){
            return null;
        }
        return Evaluate(node.Node,query).Count>0;
    }
    /// <summary>nodes()。XQuery が選んだ節の並び。xml が NULL なら空。</summary>
    public static IEnumerable<SqlXmlNode> Nodes(string? xml,string query){
        if(xml is null){
            return [];
        }
        return NodesOf(Evaluate(Document(xml),query),query);
    }
    /// <summary>nodes() が返した節に対する nodes()。</summary>
    public static IEnumerable<SqlXmlNode> Nodes(SqlXmlNode? node,string query){
        if(node is null){
            return [];
        }
        return NodesOf(Evaluate(node.Node,query),query);
    }
    private static List<SqlXmlNode> NodesOf(XdmValue value,string query){
        var nodes=new List<SqlXmlNode>(value.Count);
        foreach(var item in value){
            if(item is not XdmNode node){
                throw new SqlFunctionException($"XQuery [nodes()]: '{query}' の結果が節ではない。",2374);
            }
            nodes.Add(new SqlXmlNode(node));
        }
        return nodes;
    }
    private static XdmNode Document(string xml){
        return documents.GetValue(xml,static text=>{
            try{
                return processor.NewDocumentBuilder().Build(new StringReader(text));
            }catch(SaxonApiException error){
                throw new SqlFunctionException("XML parsing: "+error.Message,9400);
            }
        });
    }
    private static XdmValue Evaluate(XdmNode context,string query){
        XQueryExecutable executable;
        try{
            executable=queries.GetOrAdd(query,static text=>processor.NewXQueryCompiler().Compile(text));
        }catch(SaxonApiException error){
            throw new NotSupportedException($"XQuery を評価できない(SQL Server だけの書き方の可能性): {error.Message}",error);
        }
        var evaluator=executable.Load();
        evaluator.ContextItem=context;
        return evaluator.Evaluate();
    }
    private static string? Single(XdmValue value,string query){
        if(value.Count==0){
            return null;
        }
        if(value.Count>1){
            throw new SqlFunctionException($"XQuery [value()]: 'value()' requires a singleton (or empty sequence), found operand of type '{query}'",2389);
        }
        return value[0].StringValue;
    }
    // 本物の query() は、取り出した節やその中で使う名前空間だけを宣言する。祖先から受け継いだ使わない宣言は書かない。
    private static string WithoutUnusedNamespaces(string xml){
        global::System.Xml.Linq.XElement element;
        try{
            element=global::System.Xml.Linq.XElement.Parse(xml,global::System.Xml.Linq.LoadOptions.PreserveWhitespace);
        }catch(global::System.Xml.XmlException){
            return xml;
        }
        foreach(var current in element.DescendantsAndSelf().ToList()){
            foreach(var declaration in current.Attributes().Where(static attribute=>attribute.IsNamespaceDeclaration).ToList()){
                var uri=declaration.Value;
                var used=current.DescendantsAndSelf().Any(descendant=>descendant.Name.NamespaceName==uri||descendant.Attributes().Any(attribute=>!attribute.IsNamespaceDeclaration&&attribute.Name.NamespaceName==uri));
                if(!used){
                    declaration.Remove();
                }
            }
        }
        return element.ToString(global::System.Xml.Linq.SaveOptions.DisableFormatting);
    }
    private static string Serialize(XdmValue value){
        var builder=new global::System.Text.StringBuilder();
        foreach(var item in value){
            if(item is XdmNode node){
                builder.Append(WithoutUnusedNamespaces(node.OuterXml));
            }else{
                builder.Append(item.StringValue);
            }
        }
        return builder.ToString();
    }
}
