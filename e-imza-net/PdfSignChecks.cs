using System;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.X509Certificates;
using iText.Kernel.Pdf;
using iText.Signatures;

namespace EimzaSignerService
{
    public static class PdfSignChecks
    {
        /// <summary>
        /// PDF daha önce aynı sertifika (aynı token) ile imzalanmış mı?
        /// Karşılaştırma: sertifika Thumbprint (parmak izi)
        /// </summary>
        public static bool IsAlreadySignedByThisCertificate(string pdfPath, X509Certificate2 cert)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || cert == null)
                return false;

            try
            {
                using var reader = new PdfReader(pdfPath);
                using var pdfDoc = new PdfDocument(reader);
                var sigUtil = new SignatureUtil(pdfDoc);
                var names = sigUtil.GetSignatureNames();

                foreach (var name in names)
                {
                    var pkcs7 = sigUtil.ReadSignatureData(name);
                    if (pkcs7 == null) continue;

                    var bcCert = pkcs7.GetSigningCertificate();
                    if (bcCert == null) continue;

                    // iText/BC -> .NET sertifikasına dönüştür
                    var existing = new X509Certificate2(bcCert.GetEncoded());

                    // En güvenlisi: Thumbprint
                    if (existing.Thumbprint.Equals(cert.Thumbprint, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch
            {
                // PDF okunamadıysa kontrolü engelleme; imzalamaya izin ver
                return false;
            }

            return false;
        }
    }
}
