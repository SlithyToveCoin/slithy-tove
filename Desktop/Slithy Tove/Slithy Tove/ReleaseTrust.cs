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
    // Release update key. Replace this with the offline production
    // release key before wide public distribution.
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

