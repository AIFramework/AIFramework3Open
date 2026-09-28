using AI.DataStructs.Algebraic;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using Xunit;

namespace AiFramework.Tools.Atlas.UnitTests;

public sealed class ProjectTests : IDisposable
{
    private const string LongBody = """
        {
            var builder = new System.Text.StringBuilder();
            foreach (char symbol in text.Trim().ToLowerInvariant())
                builder.Append(char.IsLetterOrDigit(symbol) ? symbol : '-');
            return builder.ToString().Trim('-');
        }
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("atlas-project-").FullName;

    [Fact]
    public void SourceLinkIsFoundThroughProperty()
    {
        string library = Repository("lib");
        Write(library, "src/AI/AI.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        string project = Repository("app");
        Write(project, "src/App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <Core>$(MSBuildThisFileDirectory)..\..\..\lib\src\AI\AI.csproj</Core>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\Model\Model.csproj" />
                <ProjectReference Include="$(Core)" Condition="Exists('$(Core)')" />
              </ItemGroup>
            </Project>
            """);
        Write(project, "src/Model/Model.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        LibraryLink link = LibraryLinks.Detect(project, Path.Combine(_folder, "nuget"))!;

        Assert.True(link.Sources);
        Assert.Equal(Path.GetFullPath(library), Path.GetFullPath(link.Root), ignoreCase: true);
        Assert.Equal(["App"], link.Projects);
        Assert.Null(LibraryLinks.Detect(library, Path.Combine(_folder, "nuget")));
    }

    [Fact]
    public void PackageLinkIsFoundInNuGetCache()
    {
        string nuget = Path.Combine(_folder, "nuget");
        Write(nuget, "ai.ml/4.0.0/lib/net9.0/AI.ML.dll", "");

        string project = Repository("pkg");
        Write(project, "App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="AI.ML" Version="4.0.0" />
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);

        LibraryLink link = LibraryLinks.Detect(project, nuget)!;

        Assert.False(link.Sources);
        Assert.Equal("4.0.0", link.Version);
        Assert.EndsWith("AI.ML.dll", link.Assemblies["AI.ML"]);
    }

    [Fact]
    public async Task PackageSnapshotIsBuiltOncePerVersion()
    {
        var link = new LibraryLink(false, Path.Combine(_folder, "snapshot"), "4.0.0",
            new Dictionary<string, string> { ["AI"] = typeof(Vector).Assembly.Location }, ["App"]);

        string first = await LibrarySnapshot.EnsureAsync(link);
        string second = await LibrarySnapshot.EnsureAsync(link with { Projects = ["Other"] });

        Assert.Contains("построен", first);
        Assert.Contains("актуален", second);
        using var store = new UnitStore(LibrarySnapshot.IndexPath(link));
        Assert.Contains(store.PublicLibraryUnits(), unit => unit.Id == "T:AI.DataStructs.Algebraic.Vector");
    }

    [Fact]
    public void DisclosureIsPerProjectAndNoneByDefault()
    {
        string path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, """{ "projects": { "KnowledgeGraph": "names", "C:\\Work\\Secret": "all" } }""");

        AtlasSettings settings = AtlasSettings.Load(path);

        Assert.Equal((Disclosure.Names, true), settings.DisclosureFor(@"D:\repos\knowledgegraph"));
        Assert.Equal((Disclosure.All, true), settings.DisclosureFor(@"c:\work\secret\"));
        Assert.Equal((Disclosure.None, false), settings.DisclosureFor(@"C:\Other"));
    }

    [Theory]
    [InlineData(Disclosure.None, 0, false)]
    [InlineData(Disclosure.Names, 1, false)]
    [InlineData(Disclosure.All, 1, true)]
    public async Task ProjectCodeLeavesOnlyAsAllowed(Disclosure level, int requests, bool bodySent)
    {
        var client = new RecordingClient();
        string journal = Path.Combine(_folder, "outbound.jsonl");
        var judge = new DupJudge(client, journal);
        CodeUnit project = Unit("App", "M:App.Text.Slug(System.String)", "{ return ТАЙНАЯ_СТРОКА_ТЕЛА(text); }");
        CodeUnit library = Unit("AI.NLP", "M:AI.NLP.Text.Slug(System.String)", LongBody);
        var finding = new DupFinding(project, library, new DupFeatures(0.2, 0, 1, 0, 0, 1, true, false), "один предмет, разный код", "", library, "имя");

        await judge.JudgeAsync(finding, disclose: unit => unit.Project == "App" ? level : Disclosure.All);

        string sent = File.Exists(journal) ? File.ReadAllText(journal) : "";
        Assert.Equal(requests, client.Prompts.Count);
        Assert.Equal(requests, sent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(bodySent, client.Prompts.Any(prompt => prompt.Contains("ТАЙНАЯ_СТРОКА_ТЕЛА")));
        Assert.Equal(bodySent, sent.Contains("ТАЙНАЯ_СТРОКА_ТЕЛА"));
    }

    [Fact]
    public void DirectionTellsWhereToFix()
    {
        CodeUnit app = Unit("App", "M:App.A.Hash(System.String)", LongBody);
        CodeUnit other = Unit("App", "M:App.B.Hash(System.String)", LongBody);
        CodeUnit open = Unit("AI.Script", "M:AI.Script.ValueDigest.Hash(System.String)", LongBody);
        CodeUnit closed = open with { Access = "private" };
        bool Owns(string assembly) => assembly == "App";

        Assert.Equal(ProjectReport.Internal, ProjectReport.Direction(Pair(app, other, name: 1), Owns));
        Assert.Equal(ProjectReport.Repeats, ProjectReport.Direction(Pair(open, app, name: 1), Owns));
        Assert.Null(ProjectReport.Direction(Pair(app, closed, name: 1), Owns));
        Assert.Null(ProjectReport.Direction(Pair(app, open, name: 0, code: 0.85), Owns));
        Assert.Null(ProjectReport.Direction(Pair(open, closed, name: 1), Owns));
    }

    [Fact]
    public void CandidatesAreSimpleSelfContainedCode()
    {
        CodeUnit slug = Unit("App", "M:App.Text.Slug(System.String)", LongBody, inputs: "string", returns: "string");
        CodeUnit usesSlug = Unit("App", "M:App.Text.Key(System.String)", LongBody, inputs: "string", returns: "string");
        CodeUnit readsFile = Unit("App", "M:App.Io.Load(System.String)", LongBody, inputs: "string", returns: "string");
        CodeUnit projectType = Unit("App", "M:App.Graph.Label(App.Model.Entity)", LongBody, inputs: "App.Model.Entity", returns: "string");
        CodeUnit instance = Unit("App", "M:App.Text.Clean(System.String)", LongBody, inputs: "string", returns: "string", signature: "public string Text.Clean(string text)");
        CodeUnit callsInstance = Unit("App", "M:App.Text.Twice(System.String)", LongBody, inputs: "string", returns: "string");

        var callees = new Dictionary<(string, string), HashSet<string>>
        {
            [("App", usesSlug.Id)] = ["App|" + slug.Id, "System.Runtime|M:System.String.Trim"],
            [("App", readsFile.Id)] = ["System.Runtime|M:System.IO.File.ReadAllText(System.String)"],
            [("App", callsInstance.Id)] = ["App|" + instance.Id],
        };

        IReadOnlyList<CodeUnit> candidates = ProjectReport.Candidates([slug, usesSlug, readsFile, projectType, instance, callsInstance], callees, assembly => assembly == "App");

        Assert.Equal([usesSlug.Id, slug.Id], candidates.Select(unit => unit.Id));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (string file in Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_folder, recursive: true);
    }

    private static DupFinding Pair(CodeUnit a, CodeUnit b, double name, double code = 0.3) =>
        new(a, b, new DupFeatures(code, 0, name, 0, 0, 1, true, false), "один предмет, разный код", "", b, "имя");

    private static CodeUnit Unit(string project, string id, string body, string inputs = "string", string returns = "string", string? signature = null) =>
        new(project, id, "method", "public", "src/x.cs", 1, signature ?? $"public static {returns} {id[2..id.IndexOf('(')]}(…)", "", body, null, SymbolUnits.Hash(body), returns, inputs);

    private string Repository(string name)
    {
        string root = Path.Combine(_folder, name);
        Directory.CreateDirectory(root);

        using var git = Process.Start(new ProcessStartInfo("git", "init -q") { WorkingDirectory = root, RedirectStandardInput = true, CreateNoWindow = true })!;
        git.StandardInput.Close();
        git.WaitForExit();
        return root;
    }

    private static void Write(string root, string relative, string text)
    {
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private sealed class RecordingClient : ILLMClient
    {
        public List<string> Prompts { get; } = [];

        public Task<string> SendAsync(string text, GenerateSettings generateSettings = null!, CancellationToken cancellationToken = default)
        {
            Prompts.Add(text);
            return Task.FromResult("""{"verdict":"перекрытие","why":"тест"}""");
        }

        public Task<string> SendAsync(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null!, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ChatCompletionsResponse> SendFullAsync(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null!, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> TokenizeAsync(IEnumerable<LLMMessage> messages, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
