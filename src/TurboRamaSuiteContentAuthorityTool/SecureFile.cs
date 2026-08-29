using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text;

namespace TurboRamaSuiteContentAuthorityTool;

internal static class SecureFile
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal static byte[] ReadBoundedRegularFile(
        string path,
        int minimumBytes,
        int maximumBytes,
        bool requirePrivatePermissions)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new AuthorityArtifactException("A required input file is missing.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or
            NotSupportedException or PathTooLongException)
        {
            throw new AuthorityArtifactException("An input path is invalid.");
        }

        var information = new FileInfo(fullPath);
        if (!information.Exists ||
            information.LinkTarget is not null ||
            information.Attributes.HasFlag(FileAttributes.Directory) ||
            information.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
            information.Directory is null ||
            HasLinkedDirectoryAncestor(information.Directory) ||
            information.Length < minimumBytes ||
            information.Length > maximumBytes)
        {
            throw new AuthorityArtifactException(
                "A required input is not a bounded regular file.");
        }

        if (requirePrivatePermissions)
        {
            RequirePrivateFilePermissions(
                fullPath, "The offline issuer private key");
            if (IsInsideGitWorkTree(fullPath))
            {
                throw new AuthorityArtifactException(
                    "The offline issuer private key must not be stored in a Git work tree.");
            }
        }

        try
        {
            using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.SequentialScan);
            if (stream.Length < minimumBytes || stream.Length > maximumBytes)
            {
                throw new AuthorityArtifactException(
                    "A required input changed while it was being opened.");
            }

            var result = new byte[stream.Length];
            stream.ReadExactly(result);
            if (stream.ReadByte() != -1)
            {
                CryptographicOperations.ZeroMemory(result);
                throw new AuthorityArtifactException(
                    "A required input changed while it was being read.");
            }

            return result;
        }
        catch (AuthorityArtifactException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or
            UnauthorizedAccessException)
        {
            throw new AuthorityArtifactException(
                "A required input could not be read safely.");
        }
    }

    internal static RSA ImportPrivateRsaPem(byte[] pemBytes)
    {
        char[] characters;
        try
        {
            characters = StrictUtf8.GetChars(pemBytes);
        }
        catch (DecoderFallbackException)
        {
            throw new AuthorityArtifactException(
                "The offline issuer private key is not valid UTF-8 PEM.");
        }

        try
        {
            if (characters.AsSpan().IndexOf('\0') >= 0)
            {
                throw new AuthorityArtifactException(
                    "The offline issuer private key PEM is invalid.");
            }

            var rsa = RSA.Create();
            try
            {
                rsa.ImportFromPem(characters);
                if (rsa.KeySize is < 2048 or > 4096)
                {
                    throw new AuthorityArtifactException(
                        "The offline issuer RSA key must have 2048 to 4096 bits.");
                }

                var probe = new byte[32];
                RandomNumberGenerator.Fill(probe);
                try
                {
                    var signature = rsa.SignData(
                        probe,
                        HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pss);
                    CryptographicOperations.ZeroMemory(signature);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(probe);
                }

                return rsa;
            }
            catch
            {
                rsa.Dispose();
                throw;
            }
        }
        catch (AuthorityArtifactException)
        {
            throw;
        }
        catch (CryptographicException)
        {
            throw new AuthorityArtifactException(
                "The offline issuer file does not contain one usable RSA private key.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(
                    characters.AsSpan()));
        }
    }

    internal static void WriteNewFile(string path, ReadOnlySpan<byte> bytes)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
            SetPrivateFileMode(path);
        }
        catch (Exception exception) when (exception is IOException or
            UnauthorizedAccessException)
        {
            throw new AuthorityArtifactException(
                "An output artifact could not be written atomically.");
        }
    }

    internal static byte[] Utf8WithoutBom(string value) =>
        StrictUtf8.GetBytes(value);

    internal static void SetPrivateDirectoryMode(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            SetWindowsPrivateDirectoryAcl(path);
        }
        else
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);
        }
    }

    internal static bool IsInsideGitWorkTree(string path)
    {
        var current = File.Exists(path)
            ? new FileInfo(path).Directory
            : new DirectoryInfo(path);
        while (current is not null)
        {
            var marker = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(marker) || File.Exists(marker))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    internal static bool HasLinkedDirectoryAncestor(DirectoryInfo? directory)
    {
        var current = directory;
        while (current is not null)
        {
            if (current.LinkTarget is not null ||
                current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    internal static void RequireOwnerOnlyArtifactSet(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            if (!directory.Exists ||
                directory.LinkTarget is not null ||
                directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new AuthorityArtifactException(
                    "The content-authority output is not a regular directory.");
            }

            if (OperatingSystem.IsWindows())
            {
                RequireWindowsOwnerOnlyDirectoryAcl(
                    path, CurrentWindowsUserSid());
            }
            else
            {
                var expectedMode = UnixFileMode.UserRead |
                                   UnixFileMode.UserWrite |
                                   UnixFileMode.UserExecute;
                if (File.GetUnixFileMode(path) != expectedMode)
                {
                    throw new AuthorityArtifactException(
                        "The content-authority output directory must have mode 0700.");
                }
            }

            var expectedNames = new HashSet<string>(StringComparer.Ordinal)
            {
                AuthorityArtifactNames.Envelope,
                AuthorityArtifactNames.IssuerSpki,
                AuthorityArtifactNames.EnvelopeSha256,
                AuthorityArtifactNames.IssuerSpkiSha256
            };
            var actualNames = Directory.EnumerateFileSystemEntries(
                    path, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => name is not null)
                .Cast<string>()
                .ToArray();
            if (actualNames.Length != expectedNames.Count ||
                actualNames.Any(name => !expectedNames.Contains(name)))
            {
                throw new AuthorityArtifactException(
                    "The content-authority output directory must contain exactly four approved artifacts.");
            }

            foreach (var name in expectedNames)
            {
                RequirePrivateFilePermissions(
                    Path.Combine(path, name),
                    "A content-authority output file");
            }
        }
        catch (AuthorityArtifactException)
        {
            throw;
        }
        catch (SystemException)
        {
            throw new AuthorityArtifactException(
                "The content-authority output permissions could not be verified safely.");
        }
    }

    private static void RequirePrivateFilePermissions(string path, string label)
    {
        if (OperatingSystem.IsWindows())
        {
            RequireWindowsOwnerOnlyFileAcl(path, label);
            return;
        }

        var mode = File.GetUnixFileMode(path);
        var readOnly = UnixFileMode.UserRead;
        var readWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        if (mode != readOnly && mode != readWrite)
        {
            throw new AuthorityArtifactException(
                $"{label} must be owner-only (0400 or 0600)."
            );
        }
    }

    private static void SetPrivateFileMode(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            SetWindowsPrivateFileAcl(path);
        }
        else
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RequireWindowsOwnerOnlyFileAcl(
        string path,
        string label)
    {
        try
        {
            var currentUser = CurrentWindowsUserSid();
            var security = FileSystemAclExtensions.GetAccessControl(
                new FileInfo(path),
                AccessControlSections.Owner | AccessControlSections.Access);
            if (!security.AreAccessRulesProtected ||
                security.GetOwner(typeof(SecurityIdentifier)) is not
                    SecurityIdentifier owner ||
                !owner.Equals(currentUser))
            {
                throw new AuthorityArtifactException(
                    $"{label} must have a protected owner-only Windows ACL.");
            }

            var granted = (FileSystemRights)0;
            foreach (var rule in security.GetAccessRules(
                         includeExplicit: true,
                         includeInherited: true,
                         targetType: typeof(SecurityIdentifier)))
            {
                if (rule is not FileSystemAccessRule fileRule ||
                    fileRule.IsInherited ||
                    fileRule.AccessControlType != AccessControlType.Allow ||
                    fileRule.IdentityReference is not SecurityIdentifier identity ||
                    !identity.Equals(currentUser))
                {
                    throw new AuthorityArtifactException(
                        $"{label} Windows ACL grants another principal access.");
                }

                granted |= fileRule.FileSystemRights;
            }

            if ((granted & FileSystemRights.ReadData) == 0 ||
                (granted & FileSystemRights.ReadAttributes) == 0 ||
                (granted & FileSystemRights.ReadPermissions) == 0)
            {
                throw new AuthorityArtifactException(
                    $"{label} Windows ACL does not grant the owner required read access.");
            }
        }
        catch (AuthorityArtifactException)
        {
            throw;
        }
        catch (SystemException)
        {
            throw new AuthorityArtifactException(
                $"{label} Windows ACL could not be verified safely.");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetWindowsPrivateFileAcl(string path)
    {
        try
        {
            var currentUser = CurrentWindowsUserSid();
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true,
                preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            FileSystemAclExtensions.SetAccessControl(new FileInfo(path), security);
            RequireWindowsOwnerOnlyFileAcl(
                path, "A content-authority output file");
        }
        catch (AuthorityArtifactException)
        {
            throw;
        }
        catch (SystemException)
        {
            throw new AuthorityArtifactException(
                "An owner-only Windows ACL could not be applied to an output file.");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetWindowsPrivateDirectoryAcl(string path)
    {
        try
        {
            var currentUser = CurrentWindowsUserSid();
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true,
                preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            FileSystemAclExtensions.SetAccessControl(
                new DirectoryInfo(path), security);
            RequireWindowsOwnerOnlyDirectoryAcl(path, currentUser);
        }
        catch (AuthorityArtifactException)
        {
            throw;
        }
        catch (SystemException)
        {
            throw new AuthorityArtifactException(
                "An owner-only Windows ACL could not be applied to the output directory.");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RequireWindowsOwnerOnlyDirectoryAcl(
        string path,
        SecurityIdentifier currentUser)
    {
        var security = FileSystemAclExtensions.GetAccessControl(
            new DirectoryInfo(path),
            AccessControlSections.Owner | AccessControlSections.Access);
        if (!security.AreAccessRulesProtected ||
            security.GetOwner(typeof(SecurityIdentifier)) is not
                SecurityIdentifier owner ||
            !owner.Equals(currentUser))
        {
            throw new AuthorityArtifactException(
                "The output directory does not have a protected owner-only Windows ACL.");
        }

        var hasOwnerAllow = false;
        foreach (var rule in security.GetAccessRules(
                     includeExplicit: true,
                     includeInherited: true,
                     targetType: typeof(SecurityIdentifier)))
        {
            if (rule is not FileSystemAccessRule fileRule ||
                fileRule.IsInherited ||
                fileRule.AccessControlType != AccessControlType.Allow ||
                fileRule.IdentityReference is not SecurityIdentifier identity ||
                !identity.Equals(currentUser))
            {
                throw new AuthorityArtifactException(
                    "The output directory Windows ACL grants another principal access.");
            }

            hasOwnerAllow = true;
        }

        if (!hasOwnerAllow)
        {
            throw new AuthorityArtifactException(
                "The output directory Windows ACL does not grant owner access.");
        }
    }

    [SupportedOSPlatform("windows")]
    private static SecurityIdentifier CurrentWindowsUserSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User ?? throw new AuthorityArtifactException(
            "The current Windows user has no security identifier.");
    }
}
