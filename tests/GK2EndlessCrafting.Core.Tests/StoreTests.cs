using GK2EndlessCrafting.Core;
using Xunit;

public class StoreTests
{
    [Fact]
    public void Round_trip_keeps_entries()
    {
        var a = new EndlessRegistry();
        a.Set("w1", "recipe_a");
        a.Set("w2", "recipe_b");

        var text = EndlessStore.Serialize(a);
        var b = new EndlessRegistry();
        EndlessStore.Load(b, text);

        Assert.True(b.IsOn("w1"));
        Assert.Equal("recipe_b", b.RecipeFor("w2"));
        Assert.Equal(2, b.Count);
    }

    [Fact]
    public void Load_of_empty_text_clears()
    {
        var r = new EndlessRegistry();
        r.Set("w1", "a");
        EndlessStore.Load(r, "");
        Assert.Equal(0, r.Count);
    }

    [Fact]
    public void Bad_lines_are_ignored()
    {
        var r = new EndlessRegistry();
        EndlessStore.Load(r, "# comment\nno-separator\nw1|ok\n\nw2|\n|recipe");
        Assert.True(r.IsOn("w1"));
        Assert.Equal("ok", r.RecipeFor("w1"));
        Assert.Equal(1, r.Count);
    }

    [Fact]
    public void Serialize_is_stable_for_same_content()
    {
        var r = new EndlessRegistry();
        r.Set("w1", "a");
        Assert.Equal(EndlessStore.Serialize(r), EndlessStore.Serialize(r));
    }
}
