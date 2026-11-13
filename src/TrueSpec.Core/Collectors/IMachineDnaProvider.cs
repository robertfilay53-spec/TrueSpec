namespace TrueSpec.Core.Collectors;

using TrueSpec.Core.Comparison;

/// <summary>
/// Reads the machine fingerprint (Blueprint 4, U7): the serials of the
/// board, BIOS, disks, memory modules and processor, hashed in Core. The
/// Windows implementation reads them through CIM; anywhere else the
/// provider is absent and the scan reports "DNA not available". A provider
/// never throws for missing hardware: it returns null when it could not run
/// at all, or an unavailable <see cref="MachineDna"/> (no hash) when it ran
/// but no usable serial was reported. The only exception is
/// <see cref="OperationCanceledException"/> on cancellation.
/// </summary>
public interface IMachineDnaProvider
{
    Task<MachineDna?> CollectAsync(CancellationToken cancellationToken = default);
}
