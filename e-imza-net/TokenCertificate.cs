using Net.Pkcs11Interop.HighLevelAPI;
using System.Security.Cryptography.X509Certificates;

namespace EimzaSignerService;

public class TokenCertificate
{
    public string Label { get; set; } = "";
    public byte[]? Id { get; set; }
    public ISlot Slot { get; set; } = default!;
    public X509Certificate2 X509 { get; set; } = default!;

    public override string ToString()
        => $"{X509?.GetNameInfo(X509NameType.SimpleName, false)} — {Label}";
}