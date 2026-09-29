using GK2EndlessCrafting.Core;
using Xunit;

public class EndlessTextTests
{
    [Fact]
    public void Gamepad_tip_is_localized()
    {
        Assert.Equal("Endless", EndlessText.GamepadTip(Lang.En));
        Assert.Equal("Бесконечно", EndlessText.GamepadTip(Lang.Ru));
    }
}
