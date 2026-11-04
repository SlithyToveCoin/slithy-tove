//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Release Trust Key
//===============================================
namespace Slithy_Tove;

// Public key used to verify update manifests before the app trusts them.
internal static class ReleaseTrust
{
    // Public key used to verify signed release manifests. The matching private
    // key is not part of this project. Changing this key needs a migration plan
    // so installed apps can still verify their next update.
    public const string UpdatePublicKeyPem =
        """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEuV9LGMpczQyxx7fsY5VKbgxNTgY2
        yQ4qRGzsEDZNclMYbtDJzzKRVXkuwpkXB3oIfLq7vCFg4NBlUC0LXcT7KA==
        -----END PUBLIC KEY-----
        """;

    public static bool IsConfigured =>
        UpdatePublicKeyPem.Contains("BEGIN PUBLIC KEY", StringComparison.Ordinal);
}

