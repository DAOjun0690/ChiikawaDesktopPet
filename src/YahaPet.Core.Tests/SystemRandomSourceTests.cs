using YahaPet.Core;
using Xunit;

public class SystemRandomSourceTests
{
    [Fact]
    public void Next_ReturnsValueWithinRequestedRange()
    {
        var source = new SystemRandomSource();
        for (int i = 0; i < 1000; i++)
        {
            int value = source.Next(5, 10);
            Assert.InRange(value, 5, 9);
        }
    }
}
