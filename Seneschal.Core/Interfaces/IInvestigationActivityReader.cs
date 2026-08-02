using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IInvestigationActivityReader
{
    Task<ActivitySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
}
