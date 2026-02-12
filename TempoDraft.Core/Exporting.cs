using System.Collections.Generic;
using System.Threading.Tasks;

namespace TempoDraft.Core;

public interface IFingerprintStore
{
    Task EnsureCreatedAsync();
    Task<bool> ExistsAsync(string fingerprint);
    Task AddAsync(string fingerprint, string date);
}

public sealed class ExportResult
{
    public required IReadOnlyList<WorkLogDraft> Exported { get; init; }
    public required IReadOnlyList<WorkLogDraft> Skipped { get; init; }
}
