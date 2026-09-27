using GK2EndlessCrafting.Core;
using Xunit;

public class RegistryTests
{
    [Fact]
    public void Set_then_IsOn_and_RecipeFor()
    {
        var r = new EndlessRegistry();
        r.Set("chest_1", "recipe_nail");
        Assert.True(r.IsOn("chest_1"));
        Assert.Equal("recipe_nail", r.RecipeFor("chest_1"));
    }

    [Fact]
    public void Set_again_replaces_recipe()
    {
        var r = new EndlessRegistry();
        r.Set("w1", "a");
        r.Set("w1", "b");
        Assert.Equal("b", r.RecipeFor("w1"));
        Assert.Equal(1, r.Count);
    }

    [Fact]
    public void Clear_turns_off()
    {
        var r = new EndlessRegistry();
        r.Set("w1", "a");
        r.Clear("w1");
        Assert.False(r.IsOn("w1"));
        Assert.Null(r.RecipeFor("w1"));
        Assert.Equal(0, r.Count);
    }

    [Fact]
    public void Empty_ids_are_ignored()
    {
        var r = new EndlessRegistry();
        r.Set("", "a");
        r.Set("w1", "");
        Assert.Equal(0, r.Count);
        Assert.False(r.IsOn(""));
        Assert.Null(r.RecipeFor("w1"));
    }

    [Fact]
    public void All_lists_entries()
    {
        var r = new EndlessRegistry();
        r.Set("w1", "a");
        r.Set("w2", "b");
        Assert.Equal(2, System.Linq.Enumerable.Count(r.All()));
    }
}
