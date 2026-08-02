using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class ActivityStoreInvestigationActivityReader(
    IActivityStore activityStore) : IInvestigationActivityReader
{
    public Task<ActivitySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default) =>
        activityStore.GetSnapshotAsync(cancellationToken);
}
