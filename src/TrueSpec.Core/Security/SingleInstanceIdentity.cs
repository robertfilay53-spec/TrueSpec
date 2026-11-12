namespace TrueSpec.Core.Security;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// The per-user part of the single-instance mutex name. The mutex name is a
/// fixed, well-known string, so any other process on the machine can create
/// it first and block TrueSpec from ever launching (a local denial of
/// service: the second launch sees the mutex taken and exits). Scoping the
/// name to the current user with a hash of the logon identity stops another
/// user in the same session from squatting it, while the "Local\" session
/// scope still keeps two RDP users from blocking each other. Only the hash
/// is ever in the name: the plain username never appears in it.
/// </summary>
public static class SingleInstanceIdentity
{
    /// <summary>
    /// 16 lowercase hex characters (64 bits) of the SHA-256 of the current
    /// logon identity. Stable for a user, different for different users.
    /// </summary>
    public static string PerUserSuffix()
    {
        var identity = $"{Environment.UserDomainName}\\{Environment.UserName}".ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
