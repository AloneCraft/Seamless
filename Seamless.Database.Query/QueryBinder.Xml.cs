using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Xml;
using System.Xml.XPath;
using System.Text.RegularExpressions;
using Seamless.Linq;
namespace Seamless.Database.Query;
internal sealed record SqlXmlNode(string Document,string Path);
internal sealed partial class QueryBinder{
    private BoundScalar BindXmlValue(BoundScalar source,FunctionCall function){
        if(function.Parameters.Count!=2||function.Parameters[0] is not StringLiteral path||function.Parameters[1] is not StringLiteral type){throw Unsupported(function);}
        var statement=SqlQueryEngine.Parse("SELECT CAST(NULL AS "+type.Value+")");
        var cast=(CastCall)((SelectScalarExpression)((QuerySpecification)((SelectStatement)statement).QueryExpression).SelectElements[0]).Expression;
        var sqlType=this.DefaultStringType(BindType(cast.DataType));var namespaces=new Dictionary<string,string>(this.xmlNamespaces);
        return new(sqlType,true,f=>{var value=source.Value(f);if(value is null){return null;}var(navigator,expression,manager)=XmlContext(value,path.Value,namespaces);if(Regex.IsMatch(expression,@"^string\(\s*\.\s*\)\s*\[1\]$",RegexOptions.CultureInvariant)){return SqlValues.ConvertTo(navigator.Value,sqlType,true);}var nodes=navigator.Select(expression,manager);if(!nodes.MoveNext()){return null;}var text=nodes.Current!.Value;return SqlValues.ConvertTo(text,sqlType,true);});
    }
    private BoundSource BindXmlNodes(SchemaObjectFunctionTableReference nodes,IReadOnlyList<BindingColumn>outer){
        if(nodes.Columns.Count!=1||nodes.Parameters.Count!=1||nodes.Parameters[0] is not StringLiteral path){throw Unsupported(nodes);}
        var parts=new MultiPartIdentifier();foreach(var part in nodes.SchemaObject.Identifiers.Take(nodes.SchemaObject.Identifiers.Count-1)){parts.Identifiers.Add(part);}
        var source=this.BindScalar(new ColumnReferenceExpression{MultiPartIdentifier=parts},[],outer,null);
        var column=new SqlColumn(nodes.Columns[0].Value,new("xml",typeof(string)),true);var namespaces=new Dictionary<string,string>(this.xmlNamespaces);
        return new([new(column,nodes.Alias.Value)],(execution,outerRow)=>Rows(execution,outerRow));
        IEnumerable<object?[]>Rows(QueryExecution execution,object?[]?outerRow){
            var value=source.Value(new([],outerRow,execution));if(value is null){yield break;}
            var(navigator,expression,manager)=XmlContext(value,path.Value,namespaces);var selected=navigator.Select(expression,manager);var document=XmlDocumentText(value);
            while(selected.MoveNext()){execution.Scope.ThrowIfUnavailable();yield return[new SqlXmlNode(document,XmlPath(selected.Current!))];}
        }
    }
    private static string XmlDocumentText(object value){if(value is SqlXmlNode node){return node.Document;}if(value is System.Data.SqlTypes.SqlXml xml){return xml.Value;}return Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)??"";}
    private static (XPathNavigator Navigator,string Expression,XmlNamespaceManager Namespaces)XmlContext(object value,string query,IReadOnlyDictionary<string,string>namespaces){
        var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,IgnoreWhitespace=false};using var reader=XmlReader.Create(new StringReader(XmlDocumentText(value)),settings);var document=new XPathDocument(reader,XmlSpace.Preserve);var navigator=document.CreateNavigator();
        if(value is SqlXmlNode node&&node.Path.Length>0){navigator=navigator.SelectSingleNode(node.Path)??throw new SqlQueryException("XML context is no longer available.");}
        var manager=new XmlNamespaceManager(navigator.NameTable);var defaults="";
        foreach(var entry in namespaces){if(entry.Key.Length==0){defaults=entry.Value;manager.AddNamespace("_sql_default",entry.Value);}else{manager.AddNamespace(entry.Key,entry.Value);}}
        var declaration=new Regex("^\\s*declare\\s+(?:(default)\\s+element\\s+namespace|namespace\\s+([\\w.-]+)\\s*=)\\s*['\"]([^'\"]+)['\"]\\s*;",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        while(true){var match=declaration.Match(query);if(!match.Success){break;}if(match.Groups[1].Success){defaults=match.Groups[3].Value;manager.AddNamespace("_sql_default",defaults);}else{manager.AddNamespace(match.Groups[2].Value,match.Groups[3].Value);}query=query[match.Length..];}
        query=query.Trim();if(defaults.Length>0){query=Regex.Replace(query,@"(?:^|(?<=[/(]))([A-Za-z_][\w.-]*)(?![\w.:-]|\s*\()",match=>"_sql_default:"+match.Value);}
        return(navigator,query.Trim(),manager);
    }
    private static string XmlPath(XPathNavigator original){var current=original.Clone();var parts=new Stack<int>();while(current.NodeType!=XPathNodeType.Root){var index=1;var sibling=current.Clone();while(sibling.MoveToPrevious()){if(sibling.NodeType==XPathNodeType.Element){index++;}}parts.Push(index);if(!current.MoveToParent()){break;}}return string.Concat(parts.Select(x=>"/*["+x+"]"));}
    private BoundSource BindPivot(PivotedTableReference pivot,IReadOnlyList<BindingColumn>outer){
        if(pivot.ValueColumns.Count!=1||pivot.AggregateFunctionIdentifier.Identifiers.Count!=1){throw Unsupported(pivot);}
        var name=pivot.AggregateFunctionIdentifier.Identifiers[0].Value.ToUpperInvariant();if(!IsAggregate(name)){throw Unsupported(pivot);}
        var source=this.BindTable(pivot.TableReference,outer);var value=this.BindScalar(pivot.ValueColumns[0],source.Columns,outer,null);var key=this.BindScalar(pivot.PivotColumn,source.Columns,outer,null);
        var consumed=new HashSet<string>(StringComparer.OrdinalIgnoreCase){pivot.ValueColumns[0].MultiPartIdentifier.Identifiers[^1].Value,pivot.PivotColumn.MultiPartIdentifier.Identifiers[^1].Value};
        var groups=source.Columns.Select((column,index)=>(column,index)).Where(x=>!consumed.Contains(x.column.Column.Name)).ToArray();
        var values=pivot.InColumns.Select(x=>SqlValues.ConvertTo(x.Value,key.Type)).ToArray();var aggregateType=AggregateType(name,value.Type);var comparer=new SqlRowComparer(types:groups.Select(x=>this.GetColumnType(x.column.Column)).ToArray());
        var columns=groups.Select(x=>new BindingColumn(x.column.Column,pivot.Alias.Value)).Concat(pivot.InColumns.Select(x=>new BindingColumn(new(x.Value,aggregateType,true),pivot.Alias.Value))).ToArray();
        return new(columns,(execution,outerRow)=>Rows(execution,outerRow));
        IEnumerable<object?[]>Rows(QueryExecution execution,object?[]?outerRow){
            object?[]?previous=null;AggregateState[]?states=null;
            IEnumerable<object?[]>ordered=source.Rows(execution,outerRow);
            if(groups.Length>0){ordered=SqlBoundedOperators.Sort(execution,ordered,row=>groups.Select(x=>row[x.index]).ToArray(),comparer);}
            foreach(var row in ordered){var group=groups.Select(x=>row[x.index]).ToArray();if(states is not null&&comparer.Compare(previous,group)!=0){yield return Finish();states=null;}if(states is null){previous=group;states=values.Select(_=>new AggregateState(new(name,value,false,aggregateType),execution.Scope,execution.Options)).ToArray();}var frame=new EvalFrame(row,outerRow,execution);var bucket=key.Value(frame);for(var index=0;index<values.Length;index++){if(SqlValues.CompareValue(bucket,values[index],key.Type)==0){states[index].Add(frame);}}}
            if(states is not null){yield return Finish();}
            object?[]Finish(){var result=previous!.Concat(states!.Select(x=>x.Finish())).ToArray();foreach(var state in states!){state.Dispose();}return result;}
        }
    }
}

