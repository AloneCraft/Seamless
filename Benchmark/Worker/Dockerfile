FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Worker/Worker.csproj","Worker/"]
COPY ["DotNet.Linq.Enumerable.FLinq/DotNet.Linq.Enumerable.FLinq.csproj","DotNet.Linq.Enumerable.FLinq/"]
COPY ["DotNet.Linq.Enumerable.ZLinq/DotNet.Linq.Enumerable.ZLinq.csproj","DotNet.Linq.Enumerable.ZLinq/"]
COPY ["Tsavorite.Linq.Enumerable.Linq/Tsavorite.Linq.Enumerable.Linq.csproj","Tsavorite.Linq.Enumerable.Linq/"]
COPY ["Tsavorite.Linq.Enumerable.FLinq/Tsavorite.Linq.Enumerable.FLinq.csproj","Tsavorite.Linq.Enumerable.FLinq/"]
COPY ["Persisted.Linq.Enumerable.Linq/Persisted.Linq.Enumerable.Linq.csproj","Persisted.Linq.Enumerable.Linq/"]
COPY ["Persisted.Linq.Enumerable.FLinq/Persisted.Linq.Enumerable.FLinq.csproj","Persisted.Linq.Enumerable.FLinq/"]
COPY ["Persisted.Collections/Persisted.Collections.csproj","Persisted.Collections/"]
COPY ["Tsavorite.Collections/Tsavorite.Collections.csproj","Tsavorite.Collections/"]
COPY ["Seamless/Seamless.csproj","Seamless/"]
COPY ["Seamless.FLinq.Generator/Seamless.FLinq.Generator.csproj","Seamless.FLinq.Generator/"]
COPY ["Seamless.Linq/Seamless.Linq.csproj","Seamless.Linq/"]
COPY ["Seamless.Linq.Analyzers/Seamless.Linq.Analyzers.csproj","Seamless.Linq.Analyzers/"]
COPY ["Seamless.Collections/Seamless.Collections.csproj","Seamless.Collections/"]
COPY ["Seamless.Collections.Persisted/Seamless.Collections.Persisted.csproj","Seamless.Collections.Persisted/"]
COPY ["Seamless.Collections.Tsavorite/Seamless.Collections.Tsavorite.csproj","Seamless.Collections.Tsavorite/"]
COPY ["Seamless.Database.Generator/Seamless.Database.Generator.csproj","Seamless.Database.Generator/"]
COPY ["Tests/Seamless.TestKit/Seamless.TestKit.csproj","Tests/Seamless.TestKit/"]
COPY ["Seamless.Database/Seamless.Database.csproj","Seamless.Database/"]
COPY ["Directory.Build.props","."]
COPY ["Directory.Build.targets","."]
RUN dotnet restore "Worker/Worker.csproj"
COPY . .
WORKDIR "/src/Worker"
RUN dotnet publish "Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet","Worker.dll","--logical-cpu","0","--run-root","/tmp/seamless-benchmark"]
