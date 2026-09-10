//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Update Security Test
//===============================================
using System.Security.Cryptography;
using System.Text;

namespace Slithy_Tove;

// Self-test for update security rules: signature verification, URL rules, and hash checks.
internal static class UpdateSecuritySelfTest
{
    public static bool Run()
    {
        try
        {
            using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            string publicKey = signer.ExportSubjectPublicKeyInfoPem();
            byte[] payload = Encoding.UTF8.GetBytes(
                """
                {
                  "version": "0.2.0",
                  "packageUrl": "https://slithy.io/updates/windows/Slithy-Tove-Setup-0.2.0.exe",
                  "sha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                  "releaseNotes": "Security self-test.",
                  "publishedAt": "2026-06-24T16:00:00Z",
                  "updateLevel": "required"
                }
                """);
            byte[] signature = signer.SignData(
                payload,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

            UpdateManifest verified = UpdateService.VerifySignedPayload(payload, signature, publicKey);
            if (verified.Version != "0.2.0" ||
                UpdateService.GetEffectiveUpdateLevel(verified) != "required")
            {
                return false;
            }
            if (!UpdateService.IsAllowedUpdateUri(
                    new Uri(AppSettings.DefaultUpdateManifestUrl)) ||
                UpdateService.IsAllowedUpdateUri(
                    new Uri("http://example.com/updates/windows/stable.json")))
            {
                return false;
            }

            byte[] tampered = payload.ToArray();
            tampered[10] ^= 1;
            try
            {
                UpdateService.VerifySignedPayload(tampered, signature, publicKey);
                return false;
            }
            catch (CryptographicException)
            {
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}

