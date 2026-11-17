using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;

namespace Seamless.Management;

public sealed record TypeCompletionItem(string Label,string InsertText,bool IsNamespace,int Start,int Length);

/// <summary>型名を実行せずに読み取り、入力中の名前空間と型引数で候補を絞る。</summary>
public sealed class CSharpTypeCompletion{
    private sealed record Entry(string FullName,string Name,string Scope,bool Namespace,bool Alias);
    private static readonly Lazy<Entry[]> platform=new(()=>Entries(ReadMetadataTypes(
        Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(),"*.dll")),true));
    private readonly Entry[] additional;
    public CSharpTypeCompletion(IEnumerable<string> additionalTypes){this.additional=Entries(additionalTypes);}
    public TypeCompletionItem[] Complete(string text,int caret,bool all=false){
        caret=Math.Clamp(caret,0,text.Length);
        var start=0;var stack=new Stack<int>();
        for(var index=0;index<caret;index++){
            var character=text[index];
            if(character is '<' or '('){stack.Push(start);start=index+1;}
            else if(character is '>' or ')'){if(stack.TryPop(out var outer)){start=outer;}}
            else if(character==','&&stack.Count>0){start=index+1;}
        }
        while(start<caret&&char.IsWhiteSpace(text[start])){start++;}
        var rooted=text.AsSpan(start,caret-start).StartsWith("global::",StringComparison.Ordinal);
        if(rooted){start+=8;}
        var input=text[start..caret];var depth=0;var dot=-1;
        for(var index=0;index<input.Length;index++){
            if(input[index]=='<'){depth++;}
            else if(input[index]=='>'){depth--;}
            else if(input[index]=='.'&&depth==0){dot=index;}
        }
        var scope="";if(dot>=0){scope=WithoutArguments(input[..dot]);}
        var prefix=input[(dot+1)..];
        start+=dot+1;
        var end=caret;
        while(end<text.Length&&(char.IsLetterOrDigit(text[end])||text[end] is '_' or '@')){end++;}
        var qualified=rooted||dot>=0;
        var entries=this.additional.Concat(platform.Value);
        if(qualified){entries=entries.Where(entry=>entry.Scope==scope&&!entry.Alias);}
        else if(prefix.Length==0||all){entries=entries.Where(entry=>entry.Scope.Length==0);}
        if(!all){entries=entries.Where(entry=>entry.Name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));}
        return entries.DistinctBy(entry=>(entry.FullName,entry.Namespace))
            .OrderBy(entry=>!entry.Namespace).ThenBy(entry=>entry.Name,StringComparer.OrdinalIgnoreCase)
            .Select(entry=>{
                var insert=entry.Name;var label=entry.Name;var length=end-start;
                if(!qualified&&!entry.Alias){insert="global::"+entry.FullName;if(entry.Scope.Length>0){label+=" — "+entry.Scope;}}
                if(entry.Namespace){
                    insert+=".";
                    if(end<text.Length&&text[end]=='.'){length++;}
                }else if(end<text.Length&&text[end]=='<'){
                    var generic=insert.IndexOf('<');if(generic>=0){insert=insert[..generic];}
                }
                return new TypeCompletionItem(label,insert,entry.Namespace,start,length);
            }).DistinctBy(entry=>entry.InsertText).Take(100).ToArray();
    }
    private static Entry[] Entries(IEnumerable<string> names,bool metadata=false){
        var entries=new Dictionary<(string,bool),Entry>();
        foreach(var raw in names.Distinct(StringComparer.Ordinal)){
            if(string.IsNullOrWhiteSpace(raw)){continue;}
            var name=raw;
            if(name.StartsWith("global::",StringComparison.Ordinal)){name=name[8..];}
            var parts=new List<string>();var start=0;var depth=0;
            for(var index=0;index<name.Length;index++){
                if(name[index]=='<'){depth++;}
                else if(name[index]=='>'){depth--;}
                else if(name[index]=='.'&&depth==0){parts.Add(name[start..index]);start=index+1;}
            }
            parts.Add(name[start..]);
            if(parts.Any(part=>part.Length==0)){continue;}
            var scope="";var fullScope="";
            for(var index=0;index<parts.Count;index++){
                var part=parts[index];var full=part;
                if(fullScope.Length>0){full=fullScope+"."+part;}
                var container=index<parts.Count-1;
                var key=WithoutArguments(full);
                if(!container||!entries.ContainsKey((key,false))){
                    var alias=!metadata&&parts.Count==1&&!raw.StartsWith("global::",StringComparison.Ordinal);
                    entries[(key,container)]=new(full,part,scope,container,alias);
                }
                if(!container){entries.Remove((key,true));}
                fullScope=full;
                scope=WithoutArguments(full);
            }
        }
        return entries.Values.ToArray();
    }
    private static string WithoutArguments(string value){
        var result=new StringBuilder();var depth=0;
        foreach(var character in value){
            if(character=='<'){depth++;}
            else if(character=='>'){depth--;}
            else if(depth==0){result.Append(character);}
        }
        return result.ToString();
    }
    public static string[] ReadMetadataTypes(IEnumerable<string> paths,AssemblyName? accessFrom=null,CancellationToken cancellationToken=default){
        var types=new HashSet<string>(StringComparer.Ordinal);
        foreach(var path in paths.Distinct(StringComparer.Ordinal)){
            cancellationToken.ThrowIfCancellationRequested();
            if(!File.Exists(path)){continue;}
            using var stream=File.OpenRead(path);
            using var pe=new PEReader(stream);
            if(!pe.HasMetadata){continue;}
            var metadata=pe.GetMetadataReader();
            var internalTypes=metadata.IsAssembly&&accessFrom is not null&&IsFriend(metadata,accessFrom);
            var names=new Dictionary<TypeDefinitionHandle,string?>();
            string? Name(TypeDefinitionHandle handle){
                if(names.TryGetValue(handle,out var cached)){return cached;}
                var type=metadata.GetTypeDefinition(handle);var visibility=type.Attributes&TypeAttributes.VisibilityMask;
                var visible=visibility is TypeAttributes.Public or TypeAttributes.NestedPublic;
                if(internalTypes&&visibility is TypeAttributes.NotPublic or TypeAttributes.NestedAssembly or TypeAttributes.NestedFamORAssem){visible=true;}
                if(!visible){names[handle]=null;return null;}
                var name=metadata.GetString(type.Name);
                if(name.StartsWith('<')){names[handle]=null;return null;}
                var tick=name.IndexOf('`');
                if(tick>=0){
                    var arity=int.Parse(name[(tick+1)..],System.Globalization.CultureInfo.InvariantCulture);
                    var parameters=type.GetGenericParameters().Select(parameter=>metadata.GetString(metadata.GetGenericParameter(parameter).Name)).TakeLast(arity);
                    name=name[..tick]+"<"+string.Join(",",parameters)+">";
                }
                var parent=type.GetDeclaringType();
                if(!parent.IsNil){
                    var containing=Name(parent);if(containing is null){names[handle]=null;return null;}
                    name=containing+"."+name;
                }else{
                    var space=metadata.GetString(type.Namespace);if(space.Length>0){name=space+"."+name;}
                }
                names[handle]=name;return name;
            }
            foreach(var handle in metadata.TypeDefinitions){cancellationToken.ThrowIfCancellationRequested();if(Name(handle) is{} name){types.Add(name);}}
        }
        return types.Order(StringComparer.Ordinal).ToArray();
    }
    private static bool IsFriend(MetadataReader metadata,AssemblyName accessFrom){
        foreach(var handle in metadata.GetAssemblyDefinition().GetCustomAttributes()){
            var attribute=metadata.GetCustomAttribute(handle);
            if(attribute.Constructor.Kind!=HandleKind.MemberReference){continue;}
            var constructor=metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if(constructor.Parent.Kind!=HandleKind.TypeReference){continue;}
            var type=metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            if(metadata.GetString(type.Namespace)!="System.Runtime.CompilerServices"||metadata.GetString(type.Name)!="InternalsVisibleToAttribute"){continue;}
            var blob=metadata.GetBlobReader(attribute.Value);
            if(blob.ReadUInt16()!=1){continue;}
            var text=blob.ReadSerializedString();if(text is null){continue;}
            var friend=new AssemblyName(text);
            if(!string.Equals(friend.Name,accessFrom.Name,StringComparison.OrdinalIgnoreCase)){continue;}
            var key=friend.GetPublicKey();
            if(key is null||key.Length==0){return true;}
            if(accessFrom.GetPublicKey() is{} targetKey&&key.SequenceEqual(targetKey)){return true;}
        }
        return false;
    }
}
