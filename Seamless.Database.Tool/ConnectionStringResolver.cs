namespace Seamless.Database.Tool;

internal static class ConnectionStringResolver {
    public static string Resolve(string projectFile,string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var projectDirectory=Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        string? value=null;
        value=ReadJson(Path.Combine(projectDirectory,"appsettings.json"),name)??value;
        var environment=Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if(!string.IsNullOrWhiteSpace(environment)) {
            value=ReadJson(Path.Combine(projectDirectory,$"appsettings.{environment}.json"),name)??value;
        }
        var userSecretsId=ReadUserSecretsId(projectFile);
        if(userSecretsId is not null) {
            value=ReadJson(UserSecretsPath(userSecretsId),name)??value;
        }
        value=Environment.GetEnvironmentVariable($"ConnectionStrings__{name}")??value;
        return value??throw new InvalidOperationException(
            $"Connection string 'ConnectionStrings:{name}' was not found in project configuration, user secrets, or environment variables.");
    }

    private static string? ReadJson(string path,string name) {
        if(!File.Exists(path)) {
            return null;
        }
        using var document=JsonDocument.Parse(File.ReadAllText(path));
        if(document.RootElement.TryGetProperty("ConnectionStrings",out var section)
            &&section.TryGetProperty(name,out var value)
            &&value.ValueKind==JsonValueKind.String) {
            return value.GetString();
        }
        var flatName=$"ConnectionStrings:{name}";
        return document.RootElement.TryGetProperty(flatName,out var flatValue)&&flatValue.ValueKind==JsonValueKind.String
            ?flatValue.GetString()
            :null;
    }

    private static string? ReadUserSecretsId(string projectFile) {
        var document=XDocument.Load(projectFile);
        return document.Descendants().FirstOrDefault(static element=>element.Name.LocalName=="UserSecretsId")?.Value;
    }

    private static string UserSecretsPath(string id) {
        return OperatingSystem.IsWindows()
            ?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","UserSecrets",id,"secrets.json")
            :Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".microsoft","usersecrets",id,"secrets.json");
    }
}
