namespace TrueSpec.Core.Diagnostics;

/// <summary>
/// Decodes the Windows Security Center <c>productState</c> bitmask from
/// <c>root\SecurityCenter2 AntiVirusProduct</c> into plain English.
///
/// The value packs two nibbles (Microsoft documents them as
/// WSC_SECURITY_PRODUCT_STATE):
///   bits 12-15: product state — 0 = off, 1 = on, 2 = snoozed.
///   bits 4-7:   definition status — 0 = up to date, 1 = out of date.
///
/// The previous decode checked bit 16 for "enabled" and bit 8 for
/// "up to date", which are the wrong bits: a real enabled, up-to-date
/// product (for example 0x41000 or 0x61100, the values Windows reports
/// for a healthy Defender/ESET install) decoded as "Off" / "may be out
/// of date". Verified against the documented nibble layout and the
/// community-verified masks (on-access enabled = 0x1000 set,
/// definitions stale = 0x10 set).
/// </summary>
public static class WindowsSecurityProductState
{
    public const string NotReported = "Not reported by hardware";

    public static string DecodeProductState(uint? state)
    {
        if (state is null)
        {
            return NotReported;
        }

        var value = state.Value;
        var productState = (value >> 12) & 0xF;
        var definitionState = (value >> 4) & 0xF;

        return productState switch
        {
            0 => "Off",
            2 => "Snoozed",
            1 => definitionState switch
            {
                0 => "On, up to date",
                1 => "On, may be out of date",
                _ => "On, update status unknown",
            },
            _ => NotReported,
        };
    }
}
