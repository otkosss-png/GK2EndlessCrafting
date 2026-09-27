using GK2EndlessCrafting.Core;
using Xunit;

namespace GK2EndlessCrafting.Core.Tests
{
    public class CraftButtonLayoutTests
    {
        [Fact] public void Next_to_plus_when_alone()
            => Assert.Equal(46f, CraftButtonLayout.NextX(0f, 40f, null, 0f, 6f));

        [Fact] public void To_the_right_of_another_mod_button()
            => Assert.Equal(112f, CraftButtonLayout.NextX(0f, 40f, 66f, 40f, 6f));

        [Fact] public void Uses_other_button_width_not_plus_width()
            => Assert.Equal(86f, CraftButtonLayout.NextX(0f, 40f, 66f, 14f, 6f));

        [Fact] public void Negative_plus_position_is_kept()
            => Assert.Equal(-14f, CraftButtonLayout.NextX(-60f, 40f, null, 0f, 6f));
    }
}
