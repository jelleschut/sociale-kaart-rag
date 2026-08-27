using SocialeKaartRag.Core.Chunks;

namespace SocialeKaartRag.Core.Tests;

public class ChunkIdTests
{
    [Fact]
    public void Id_is_deterministic_hash_of_corpus_and_source_id()
    {
        var a = Chunk.MakeId("kb", "topic-age#00");
        var b = Chunk.MakeId("kb", "topic-age#00");
        var c = Chunk.MakeId("social-map", "topic-age#00");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Matches("^[a-f0-9]{40}$", a);
    }

    [Fact]
    public void ContentHash_changes_when_text_changes()
    {
        Assert.NotEqual(Chunk.HashContent("a"), Chunk.HashContent("b"));
    }
}
