using SocialeKaartRag.Ingest.Sources;

namespace SocialeKaartRag.Ingest.Tests;

public class KbChunksSourceTests
{
    private const string Line =
        """{"id": "topic-age", "chunk_id": "topic-age#00", "type": "topic", "heading_path": ["age — the encryption backend", "What it is"], "tags": ["age", "sops"], "related": ["topic-sops"], "last_verified": "2026-07-15", "text": "age is a small file-encryption tool."}""";

    [Fact]
    public async Task Maps_one_jsonl_record_to_one_chunk_with_provenance()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, Line + "\n");

        var result = await KbChunksSource.ReadWithReportAsync(path);

        var c = Assert.Single(result.Valid);
        Assert.Equal("kb", c.Corpus);
        Assert.Equal("topic-age#00", c.SourceId);
        Assert.Equal("soevereinlab-knowledge", c.Source);
        Assert.Equal("https://gitlab.com/platform-engineer9761628/soevereine-cloud/soevereinlab-knowledge/-/blob/main/topics/topic-age.md", c.SourceUrl);
        Assert.Equal("age — the encryption backend > What it is", c.HeadingPath);
        Assert.Equal(["age", "sops"], c.Tags);
        Assert.Equal("2026-07-15", c.LastVerified);
        Assert.Equal("topic", c.Category);
        Assert.Equal("age is a small file-encryption tool.", c.Text);
        Assert.Equal(SocialeKaartRag.Core.Chunks.Chunk.MakeId("kb", "topic-age#00"), c.Id);
        Assert.Equal(SocialeKaartRag.Core.Chunks.Chunk.HashContent(c.Text), c.ContentHash);
    }

    [Fact]
    public async Task Invalid_line_is_counted_not_skipped_silently()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, Line + "\n{\"id\": \"broken\"}\nnot json\n");

        var result = await KbChunksSource.ReadWithReportAsync(path);

        Assert.Single(result.Valid);
        Assert.Equal(2, result.InvalidCount);
        Assert.Contains("chunk_id", result.Errors[0]);
        Assert.Contains("JSON", result.Errors[1]);
    }

    [Theory]
    [InlineData("topic", "topic-age", "topics/topic-age.md")]
    [InlineData("runbook", "rb-2026-07-14-bootstrap-pipeline", "runbooks/rb-2026-07-14-bootstrap-pipeline.md")]
    [InlineData("decision", "dec-2026-07-14-vps-provider-selection", "decisions/dec-2026-07-14-vps-provider-selection.md")]
    [InlineData("reference", "alert-receiver-draaiboek", "reference/alert-receiver-draaiboek.md")]
    public void Source_url_maps_type_and_id_to_repo_path(string type, string id, string expectedTail)
    {
        Assert.EndsWith("/-/blob/main/" + expectedTail, KbChunksSource.SourceUrlFor(type, id));
    }

    [Fact]
    public async Task Real_export_parses_completely()
    {
        var path = FindRepoRootFile("kb-chunks.jsonl");
        if (path is null) return; // niet gevonden (bv. andere checkout-locatie) — sla stil over

        var result = await KbChunksSource.ReadWithReportAsync(path);

        Assert.Equal(0, result.InvalidCount);
        Assert.Equal(388, result.Valid.Count);
    }

    private static string? FindRepoRootFile(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
