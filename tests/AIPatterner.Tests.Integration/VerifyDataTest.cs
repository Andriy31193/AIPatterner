// Test to verify data persistence
namespace AIPatterner.Tests.Integration;

using Xunit;

public class VerifyDataTest
{
    [Fact]
    public async Task VerifyTestDataPersistence()
    {
        await VerifyData.Run();
    }
}

