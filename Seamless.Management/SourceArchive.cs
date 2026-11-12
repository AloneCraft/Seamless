using System.Text.Json;

namespace Seamless.Management;

public sealed record DefinitionExport(Definition Definition,long BaseVersion,long DraftRevision);
/// <summary>C#ファイルを編集してから取り込める交換形式。インポートは保存・実行・適用を行わない。</summary>
public static class SourceArchive{
    public static void Export(string directory,DefinitionExport export){
        SourceEditor.Validate(export.Definition.Source);directory=Path.GetFullPath(directory);
        if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any()){throw new IOException("空の出力先を指定してください。");}Directory.CreateDirectory(directory);
        foreach(var file in export.Definition.Source.Files){var path=Path.Combine(directory,file.Path.Replace('\\',Path.DirectorySeparatorChar).Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,file.Text);}
        File.WriteAllText(Path.Combine(directory,"definition.seamless.json"),JsonSerializer.Serialize(export,ManagementJson.Options));
    }
    public static DefinitionExport Import(string manifest){
        manifest=Path.GetFullPath(manifest);var directory=Path.GetDirectoryName(manifest)!;var export=JsonSerializer.Deserialize<DefinitionExport>(File.ReadAllText(manifest),ManagementJson.Options)??throw new InvalidDataException("定義がありません。");SourceEditor.Validate(export.Definition.Source);
        var files=new List<SourceFile>();foreach(var file in export.Definition.Source.Files){var path=Path.Combine(directory,file.Path.Replace('\\',Path.DirectorySeparatorChar).Replace('/',Path.DirectorySeparatorChar));var info=new FileInfo(path);if(info.Length>8*1024*1024){throw new InvalidDataException("ソースファイルが大きすぎます。");}
            FileSystemInfo? current=info;while(current is not null&&current.FullName!=directory){if((current.Attributes&FileAttributes.ReparsePoint)!=0){throw new InvalidDataException("ソースのリンクは明示的に解決してください。");}if(current is FileInfo fileInfo){current=fileInfo.Directory;}else{current=((DirectoryInfo)current).Parent;}}
            files.Add(new(file.Path,File.ReadAllText(path)));
        }
        var source=export.Definition.Source with{Files=files.ToArray()};SourceEditor.Validate(source);return export with{Definition=export.Definition with{Source=source}};
    }
}
