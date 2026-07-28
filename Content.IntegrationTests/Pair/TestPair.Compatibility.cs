using System.Threading.Tasks;

namespace Content.IntegrationTests.Pair;

public sealed partial class TestPair
{
    /// <summary>
    /// Compatibility implementation for the RobustToolbox revision pinned by this branch.
    /// Keeps the older pair synchronized for content tests that use the newer helper name.
    /// </summary>
    public async Task RunUntilSynced()
    {
        if (Client.Session is null)
        {
            await Server.WaitRunTicks(1);
            return;
        }

        await SyncTicks();
    }
}
