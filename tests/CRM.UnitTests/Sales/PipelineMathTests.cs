using CRM.Modules.Sales.Features.Deals;

namespace CRM.UnitTests.Sales;

public class PipelineMathTests
{
    [Theory]
    [InlineData(1000, 25, 250)]
    [InlineData(0, 50, 0)]
    [InlineData(99.99, 0, 0)]
    [InlineData(99.99, 100, 99.99)]
    [InlineData(10.01, 50, 5.01)] // 5.005 rounds away from zero
    [InlineData(33.33, 33, 11)]   // 10.9989 → 11.00
    public void Weighted_IsTheExpectedValue_RoundedToCents(decimal amount, int probability, decimal expected)
    {
        Assert.Equal(expected, PipelineMath.Weighted(amount, probability));
    }

    [Fact]
    public void Round_OnlyRoundsTheWeightedAmount()
    {
        var total = PipelineMath.Round(new CurrencyTotal("TRY", 3, 100.005m, 12.345m));

        Assert.Equal(100.005m, total.Amount);
        Assert.Equal(12.35m, total.WeightedAmount);
    }
}
