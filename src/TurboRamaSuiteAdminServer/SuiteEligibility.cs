using System.Security.Cryptography;
using System.Text;

internal sealed record SuiteEligibilityInput(
    string ProductId,string Status,string LicenseTerm,bool NoLicenseExpiry,string LicenseIdentityPolicy,
    int MaximumActiveDevices,bool ActivationConsumed,bool HasVerifier,DateTime? ActivationExpiresAt,DateTime DatabaseNow,
    string DeviceId,string ExpectedDeviceId,string BindingType,string EnrollmentPolicy,string Algorithm,
    string PublicKeySpki,string HardwareFingerprint,long ActiveDevices);

internal static class SuiteEligibility
{
    internal static bool CanIssue(SuiteEligibilityInput value) =>
        value.ProductId == "TURBORAMA_SUITE" && value.Status == "ACTIVE" && value.LicenseTerm == "LIFETIME"
        && value.NoLicenseExpiry && value.LicenseIdentityPolicy == "SOFTWARE_ONLY" && value.MaximumActiveDevices == 1
        && !value.ActivationConsumed && value.ActiveDevices == 0
        && (!value.HasVerifier || value.ActivationExpiresAt <= value.DatabaseNow)
        && value.BindingType == "SOFTWARE_BOUND_ONLINE" && value.EnrollmentPolicy == "SOFTWARE_ONLY"
        && value.Algorithm == "rsa-pss-sha256" && ValidFingerprint(value.HardwareFingerprint)
        && ValidIdentity(value.PublicKeySpki,value.DeviceId,value.ExpectedDeviceId);

    internal static bool ValidFingerprint(string value) => value.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static bool ValidIdentity(string encoded,string deviceId,string expectedDeviceId)
    {
        if(deviceId.Length != 64 || expectedDeviceId.Length != 64 || encoded.Length is < 360 or > 1024
           || !ValidFingerprint(deviceId) || !Fixed(deviceId,expectedDeviceId)) return false;
        byte[] der=[];byte[] canonical=[];byte[] reexported=[];
        try
        {
            der=Convert.FromBase64String(encoded);
            if(der.Length is < 280 or > 768)return false;
            canonical=Encoding.ASCII.GetBytes(Convert.ToBase64String(der));
            var supplied=Encoding.ASCII.GetBytes(encoded);
            try{if(canonical.Length!=supplied.Length||!CryptographicOperations.FixedTimeEquals(canonical,supplied))return false;}
            finally{CryptographicOperations.ZeroMemory(supplied);}
            using var rsa=RSA.Create();rsa.ImportSubjectPublicKeyInfo(der,out var consumed);
            if(consumed!=der.Length||rsa.KeySize is < 2048 or > 4096)return false;
            reexported=rsa.ExportSubjectPublicKeyInfo();
            if(reexported.Length!=der.Length||!CryptographicOperations.FixedTimeEquals(reexported,der))return false;
            var hash=Convert.ToHexString(SHA256.HashData(der)).ToLowerInvariant();
            return Fixed(hash,deviceId);
        }
        catch(Exception ex)when(ex is FormatException or CryptographicException){return false;}
        finally
        {
            if(der.Length>0)CryptographicOperations.ZeroMemory(der);
            if(canonical.Length>0)CryptographicOperations.ZeroMemory(canonical);
            if(reexported.Length>0)CryptographicOperations.ZeroMemory(reexported);
        }
    }
    private static bool Fixed(string left,string right)
    {
        var x=Encoding.ASCII.GetBytes(left);var y=Encoding.ASCII.GetBytes(right);
        try{return x.Length==y.Length&&CryptographicOperations.FixedTimeEquals(x,y);}
        finally{CryptographicOperations.ZeroMemory(x);CryptographicOperations.ZeroMemory(y);}
    }
}
