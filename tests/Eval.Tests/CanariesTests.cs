using SocialeKaartRag.Eval;

namespace SocialeKaartRag.Eval.Tests;

public class CanariesTests
{
    [Fact]
    public void Canaries_become_social_map_chunks_with_eval_source_and_forbidden_words()
    {
        var chunks = Canaries.ToChunks(Canaries.Load(Path.Combine(AppContext.BaseDirectory, "eval", "canaries.yaml")));
        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, c => { Assert.Equal("social-map", c.Corpus); Assert.Equal("eval", c.Source); Assert.StartsWith("eval:canary-", c.SourceId); Assert.Contains("bron:eval", c.Tags); Assert.NotNull(c.Category); Assert.NotNull(c.Lat); });
        Assert.Contains(chunks, c => c.Text.Contains("GEHEIMWOORD-ALFA"));
    }

    [Fact]
    public void Unknown_municipality_throws_a_clear_error()
    {
        var canary = new Canary { Id = "canary-x", Name = "Onbekend", Municipality = "Nergenshuizen", Category = "welzijn", Text = "tekst" };
        var ex = Assert.Throws<InvalidOperationException>(() => Canaries.ToChunks([canary]));
        Assert.Contains("Nergenshuizen", ex.Message);
        Assert.Contains("canaries.yaml", ex.Message);
    }
}
