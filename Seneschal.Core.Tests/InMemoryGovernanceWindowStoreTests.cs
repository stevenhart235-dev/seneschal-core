using Seneschal.Core.Enums;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Core.Tests;

public sealed class InMemoryGovernanceWindowStoreTests
{
    [Fact]
    public void ProductionFreeze_IsBuiltInAndDisabledByDefault()
    {
        var window = new InMemoryGovernanceWindowStore().GetWindow();

        Assert.Equal("Production Freeze", window.Name);
        Assert.Equal("Weekend production freeze.", window.Reason);
        Assert.False(window.Enabled);
        Assert.Equal(GovernanceWindowMode.Observe, window.Mode);
        Assert.Equal(
            [
                "production.deployment.execute",
                "infrastructure.production.apply",
                "infrastructure.production.destroy"
            ],
            window.AffectedCapabilities);
    }

    [Fact]
    public void SetState_ChangesOnlyInMemoryRuntimeState()
    {
        var store = new InMemoryGovernanceWindowStore();

        store.SetState(true, GovernanceWindowMode.Enforce);

        var window = store.GetWindow();
        Assert.True(window.Enabled);
        Assert.Equal(GovernanceWindowMode.Enforce, window.Mode);
    }
}
