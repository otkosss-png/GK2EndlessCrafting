using GK2EndlessCrafting.Core;
using Xunit;

public class StationRulesTests
{
    [Theory]
    [InlineData("firewood_shed_1")]
    [InlineData("firewood_shed_2")]
    [InlineData("Firewood_Shed_kitchen_2")]
    public void Firewood_sheds_are_excluded(string id)
        => Assert.True(StationRules.IsExcluded(id));

    [Theory]
    [InlineData("sawmill_1")]
    [InlineData("kitchen_table_1")]
    [InlineData("firewood")]
    [InlineData("")]
    [InlineData(null)]
    public void Other_stations_are_allowed(string id)
        => Assert.False(StationRules.IsExcluded(id));
}
