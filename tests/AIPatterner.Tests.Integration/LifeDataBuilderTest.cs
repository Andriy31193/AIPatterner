// Test to run the life data builder (does NOT clean up data)
namespace AIPatterner.Tests.Integration;

using Xunit;

public class LifeDataBuilderTest
{
    [Fact]
    public async Task BuildLifeData_4Months_ShouldCreateRealisticData()
    {
        // This test builds realistic data and DOES NOT CLEAN UP
        // Run this test manually when you want to populate the database
        // with 3-5 months of simulated life data for inspection
        
        using var builder = new LifeDataBuilder();
        await builder.BuildLifeDataAsync(monthsToSimulate: 4);
        
        // Data remains in database - no cleanup!
    }
    
    [Fact(Skip = "Run manually when you want to build data - does not clean up")]
    public async Task BuildLifeData_5Months_ShouldCreateRealisticData()
    {
        using var builder = new LifeDataBuilder();
        await builder.BuildLifeDataAsync(monthsToSimulate: 5);
        
        // Data remains in database - no cleanup!
    }
}

