using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Native;

internal sealed class NativeLambdaRenderer(IReadOnlyDictionary<string,Type> types){
    private static readonly HashSet<string> Binary=["+","-","*","/","%","==","!=","<",">","<=",">=","&&","||","&","|","^","<<",">>","??"];
    private static readonly HashSet<string> Linq=["Where","Select","SelectMany","Join","GroupJoin","GroupBy","Count","LongCount","Sum","Average","Min","Max","Any","All","Contains","ToArray","ToList","OrderBy","OrderByDescending","ThenBy","ThenByDescending","Take","Skip","Distinct","Reverse","First","FirstOrDefault","Single","SingleOrDefault","Last","LastOrDefault"];
    private static readonly HashSet<string> Calls=["Add","Remove","Replace","Contains","StartsWith","EndsWith","Substring","ToUpperInvariant","ToLowerInvariant"];
    public string Render(NativeLambdaNode node){
        var children=node.Nodes;
        switch(node.Kind){
            case "Block":return "{"+string.Concat(children.Select(child=>"__cancellation.ThrowIfCancellationRequested();"+this.Render(child)))+"}";
            case "Declarations":return string.Concat(children.Select(this.Render));
            case "Return":Count(node,1);return "return "+this.Render(children[0])+";";
            case "Expression":Count(node,1);return this.Render(children[0])+";";
            case "Local":Count(node,1);return "var "+Name(node.Text,"v")+"="+this.Render(children[0])+";";
            case "Variable":Count(node,0);return Name(node.Text,"pv");
            case "Null":Count(node,0);return "null";
            case "Literal":Count(node,0);return Literal(node.Text);
            case "Type":Count(node,0);return this.Type(node.Text);
            case "Enum":Count(node,1);return "("+this.Type(node.Text)+")("+this.Render(children[0])+")";
            case "Member":Count(node,1);return "("+this.Render(children[0])+").@"+Member(node.Text);
            case "New":return "new "+this.Type(node.Text)+"("+string.Join(",",children.Select(this.Render))+")";
            case "Convert":Count(node,1);return "("+this.Type(node.Text)+")("+this.Render(children[0])+")";
            case "Binary":Count(node,2);if(!Binary.Contains(node.Text!)){throw Invalid();}return "("+this.Render(children[0])+node.Text+this.Render(children[1])+")";
            case "Unary":Count(node,1);if(node.Text is not("!" or "~" or "+" or "-" or "++" or "--")){throw Invalid();}return "("+node.Text+this.Render(children[0])+")";
            case "Postfix":Count(node,1);if(node.Text is not("++" or "--")){throw Invalid();}return "("+this.Render(children[0])+node.Text+")";
            case "Assign":Count(node,2);if(node.Text is not("=" or "+=" or "-=" or "*=" or "/=")){throw Invalid();}return this.Render(children[0])+node.Text+this.Render(children[1]);
            case "Conditional":Count(node,3);return "("+this.Render(children[0])+"?"+this.Render(children[1])+":"+this.Render(children[2])+")";
            case "Index":if(children.Length<2){throw Invalid();}return this.Render(children[0])+"["+string.Join(",",children.Skip(1).Select(this.Render))+"]";
            case "Linq":if(!Linq.Contains(node.Text!)){throw Invalid();}return "global::System.Linq.Enumerable."+node.Text+"("+string.Join(",",children.Select(this.Render))+")";
            case "Call":if(children.Length==0||!Calls.Contains(node.Text!)){throw Invalid();}return "("+this.Render(children[0])+")."+node.Text+"("+string.Join(",",children.Skip(1).Select(this.Render))+")";
            case "Lambda":Count(node,1);var names=node.Text?.Split(',')??[];if(names.Length is 0 or >4){throw Invalid();}return "("+string.Join(",",names.Select(name=>Name(name,"p")))+")=>"+this.Render(children[0]);
            case "If":if(children.Length is <2 or >3){throw Invalid();}var result="if("+this.Render(children[0])+")"+Braces(this.Render(children[1]));if(children.Length==3){result+="else"+Braces(this.Render(children[2]));}return result;
            case "ForEach":Count(node,2);return "foreach(var "+Name(node.Text,"v")+" in "+this.Render(children[0])+"){__cancellation.ThrowIfCancellationRequested();"+this.Render(children[1])+"}";
            case "Throw":Count(node,1);return "throw new global::System.InvalidOperationException("+this.Render(children[0])+");";
            default:throw Invalid();
        }
    }
    private string Type(string? name)=>CSharp(this.ResolveType(name,0));
    private Type ResolveType(string? name,int depth){if(depth>=32){throw new NativeLambdaException("Contract","The native array type exceeds the supported depth.",true);}if(name is not null&&types.TryGetValue(name,out var type)){return type;}if(name?.EndsWith("[]",StringComparison.Ordinal)==true){return this.ResolveType(name[..^2],depth+1).MakeArrayType();}throw new NativeLambdaException("Contract","The lambda refers to an unregistered type.",true);}
    internal static string CSharp(Type type){
        if(type.IsArray){return CSharp(type.GetElementType()!)+"[]";}
        if(type.IsGenericType){return "global::"+type.GetGenericTypeDefinition().FullName!.Split('`')[0].Replace('+','.')+"<"+string.Join(",",type.GenericTypeArguments.Select(CSharp))+">";}
        return "global::"+type.FullName!.Replace('+','.');
    }
    private static string Literal(string? value){
        if(value is null){throw Invalid();}
        var expression=SyntaxFactory.ParseExpression(value);
        if(expression.ContainsDiagnostics||expression.FullSpan.Length!=value.Length){throw Invalid();}
        if(expression is LiteralExpressionSyntax){return expression.ToString();}
        if(expression is PrefixUnaryExpressionSyntax{RawKind:(int)SyntaxKind.UnaryMinusExpression or (int)SyntaxKind.UnaryPlusExpression,Operand:LiteralExpressionSyntax}){return expression.ToString();}
        throw Invalid();
    }
    private static string Name(string? name,string prefixes){if(name is null||name.Length is <2 or >6||!prefixes.Contains(name[0])||name.Skip(1).Any(static character=>character is <'0' or >'9')){throw Invalid();}return name;}
    private static string Member(string? name){if(name is null||name.Length>256||!SyntaxFacts.IsValidIdentifier(name)&&SyntaxFacts.GetKeywordKind(name)==SyntaxKind.None){throw Invalid();}return name;}
    private static void Count(NativeLambdaNode node,int count){if(node.Nodes.Length!=count){throw Invalid();}}
    private static string Braces(string value)=>"{"+value+"}";
    private static NativeLambdaException Invalid()=>new("Structure","Unsupported or malformed lambda structure.",true);
}
