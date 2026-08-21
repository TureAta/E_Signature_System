using iText.Bouncycastle.X509;
using iText.Commons.Bouncycastle.Cert;
using iText.Kernel.Crypto;
using iText.Kernel.Pdf;
using iText.Signatures;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PdfRect = iText.Kernel.Geom.Rectangle;
using Pkcs11Session = Net.Pkcs11Interop.HighLevelAPI.ISession;

namespace EimzaSignerService
{
    public class EimzaPdfSigner
    {
        public string DigestAlgorithm { get; set; } = DigestAlgorithms.SHA256;
        public string? TsaUrl { get; set; } = "http://timestamp.digicert.com";
        public NetworkCredential? TsaCredential { get; set; } = null;
        public bool DisableTsaOnFailure { get; set; } = true;

        public void SignPdf(
            string inputPdfPath,
            string outputPdfPath,
            TokenCertificate tokenCertificate,
            string pin,
            Pkcs11Service pkcs11Service,
            PdfRect visibleRectPt,
            int pageNumber)
        {
            if (string.IsNullOrWhiteSpace(inputPdfPath) || !File.Exists(inputPdfPath))
                throw new FileNotFoundException("Giriş PDF dosyası bulunamadı.", inputPdfPath);
            if (string.IsNullOrWhiteSpace(outputPdfPath))
                throw new ArgumentException("Çıkış yolu boş olamaz.", nameof(outputPdfPath));
            if (tokenCertificate?.X509 is null)
                throw new Exception("Geçerli bir kart sertifikası bulunamadı.");
            if (pageNumber <= 0) throw new ArgumentOutOfRangeException(nameof(pageNumber));
            if (visibleRectPt == null || visibleRectPt.GetWidth() <= 0 || visibleRectPt.GetHeight() <= 0)
                throw new ArgumentException("Görünür imza alanı geçersiz.", nameof(visibleRectPt));

            var tempPath = Path.Combine(
                Path.GetDirectoryName(outputPdfPath)!,
                Path.GetFileNameWithoutExtension(outputPdfPath) + $".tmp_{Guid.NewGuid():N}.pdf");

            try
            {
                foreach (var est in new[] { 128 * 1024, 512 * 1024, 2 * 1024 * 1024, 8 * 1024 * 1024 })
                {
                    try
                    {
                        InternalSign(inputPdfPath, tempPath, tokenCertificate, pin, pkcs11Service,
                                     visibleRectPt, pageNumber, est);
                        break;
                    }
                    catch (Exception ex) when (
                           ex.Message.Contains("Not enough space", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("No enough space", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("Insufficient", StringComparison.OrdinalIgnoreCase))
                    {
                        if (est == 8 * 1024 * 1024) throw;
                        continue;
                    }
                }

                if (File.Exists(outputPdfPath)) File.Delete(outputPdfPath);
                File.Move(tempPath, outputPdfPath);
            }
            catch
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                throw;
            }
        }

        private void InternalSign(
            string inputPdfPath,
            string tempPath,
            TokenCertificate tokenCertificate,
            string pin,
            Pkcs11Service pkcs11Service,
            PdfRect visibleRectPt,
            int pageNumber,
            int estimatedSize)
        {
            using var reader = new PdfReader(inputPdfPath);
            using var fos = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var signer = new PdfSigner(reader, fos, new StampingProperties());

            var props = new SignerProperties()
                .SetFieldName("Signature_" + DateTime.UtcNow.Ticks)
                .SetPageRect(visibleRectPt)
                .SetPageNumber(pageNumber)
                .SetReason("Onay")
                .SetLocation("Türkiye");
            signer.SetSignerProperties(props);

            var dotNetChain = BuildDotNetChain(tokenCertificate.X509);
            var bcChain = dotNetChain
                .Select(c2 => new X509CertificateBC(DotNetUtilities.FromX509Certificate(c2)))
                .Cast<IX509Certificate>()
                .ToArray();

            var externalSig = new PkcsTokenExternalSignature(tokenCertificate, pin, pkcs11Service, DigestAlgorithm);
            IExternalDigest externalDigest = new BouncyCastleDigest();
            IOcspClient ocsp = new OcspClientBouncyCastle();
            var crlClient = new CrlClientOnline(bcChain);
            var crlList = new List<ICrlClient> { crlClient };
            ITSAClient? tsaClient = BuildTsaClientSafe();

            signer.SignDetached(
                externalDigest,
                externalSig,
                bcChain,
                crlList, ocsp, tsaClient,
                estimatedSize,
                PdfSigner.CryptoStandard.CADES
            );

            signer.GetDocument().Close();
        }

        private ITSAClient? BuildTsaClientSafe()
        {
            if (string.IsNullOrWhiteSpace(TsaUrl)) return null;
            try
            {
                return TsaCredential is null
                    ? new TSAClientBouncyCastle(TsaUrl)
                    : new TSAClientBouncyCastle(TsaUrl, TsaCredential.UserName, TsaCredential.Password);
            }
            catch
            {
                if (DisableTsaOnFailure) return null;
                throw;
            }
        }

        private static X509Certificate2[] BuildDotNetChain(X509Certificate2 leaf)
        {
            using var chain = new X509Chain
            {
                ChainPolicy =
                {
                    RevocationMode = X509RevocationMode.Online,
                    RevocationFlag = X509RevocationFlag.ExcludeRoot,
                    VerificationFlags = X509VerificationFlags.NoFlag,
                    UrlRetrievalTimeout = TimeSpan.FromSeconds(10)
                }
            };
            chain.Build(leaf);
            return chain.ChainElements.Cast<X509ChainElement>().Select(e => e.Certificate).ToArray();
        }

        private sealed class PkcsTokenExternalSignature : IExternalSignature
        {
            private readonly TokenCertificate _tokenCert;
            private readonly string _pin;
            private readonly Pkcs11Service _service;
            private readonly string _digestAlg;

            public PkcsTokenExternalSignature(TokenCertificate tokenCert, string pin,
                                              Pkcs11Service service, string digestAlg)
            {
                _tokenCert = tokenCert ?? throw new ArgumentNullException(nameof(tokenCert));
                _pin = pin ?? "";
                _service = service ?? throw new ArgumentNullException(nameof(service));
                _digestAlg = string.IsNullOrWhiteSpace(digestAlg) ? DigestAlgorithms.SHA256 : digestAlg;
            }

            public string GetHashAlgorithm() => _digestAlg;
            public string GetEncryptionAlgorithm() => "RSA";
            public string GetDigestAlgorithmName() => _digestAlg;
            public string GetSignatureAlgorithmName() => "RSA";
            public ISignatureMechanismParams? GetSignatureMechanismParameters() => null;

            public byte[] Sign(byte[] message)
            {
                if (message == null || message.Length == 0)
                    throw new ArgumentException("Boş veri imzalanamaz.", nameof(message));

                var expected = ExpectedDigestLength(_digestAlg);
                byte[] digest = message;
                if (expected > 0 && message.Length != expected)
                {
                    digest = ComputeHash(message, _digestAlg);
                }

                var rsa = _tokenCert.X509?.GetRSAPrivateKey();
                if (rsa != null)
                {
                    return rsa.SignHash(digest, ToHashAlg(_digestAlg), RSASignaturePadding.Pkcs1);
                }

                var digestInfo = BuildDigestInfo(_digestAlg, digest);
                return SignWithPkcs11(digestInfo);
            }

            // DEĞİŞİKLİK: Bu metod artık yeniden kütüphane yüklemiyor.
            // Mevcut Pkcs11Service ve TokenCertificate bilgilerini kullanıyor.
            private byte[] SignWithPkcs11(byte[] digestInfo)
            {
                if (_tokenCert.Slot == null)
                    throw new Exception("İmzalama için token slot bilgisi bulunamadı.");

                // Gerekli RSA anahtar boyutu (fallback için)
                int modulusBytes = _tokenCert.X509?.GetRSAPublicKey()?.KeySize / 8 ?? 0;

                // İmzalama işlemi için kısa ömürlü bir oturum açılır.
                using var session = _tokenCert.Slot.OpenSession(SessionType.ReadWrite);
                try
                {
                    _service.LoginSmart( session, _pin);

                    // Sertifikayla eşleşen özel anahtarı bul
                    var privateKey = FindPrivateKeyForCert(session, _tokenCert);
                    if (privateKey == null)
                        throw new Exception("Sertifikaya karşılık gelen özel anahtar token üzerinde bulunamadı.");

                    try
                    {
                        var mech = session.Factories.MechanismFactory.Create(CKM.CKM_RSA_PKCS);
                        return session.Sign(mech, privateKey, digestInfo);
                    }
                    catch (Pkcs11Exception ex) when (
                           ex.RV == CKR.CKR_DATA_LEN_RANGE || ex.RV == CKR.CKR_MECHANISM_INVALID || ex.RV == CKR.CKR_FUNCTION_FAILED)
                    {
                        // Fallback mekanizması
                        if (modulusBytes <= 0)
                            throw new Exception("RSA anahtar boyu alınamadı; CKM_RSA_X_509 fallback çalıştırılamıyor.", ex);

                        var fullBlock = BuildPkcs1V15Block(modulusBytes, digestInfo);
                        var rawMech = session.Factories.MechanismFactory.Create(CKM.CKM_RSA_X_509);
                        return session.Sign(rawMech, privateKey, fullBlock);
                    }
                }
                finally
                {
                    // Oturum kapatma işlemini garanti altına al
                    try { session.Logout(); } catch { }
                }
            }

            // YENİ YARDIMCI METOD: Sertifikanın ID'sine göre özel anahtarı bulur.
            private IObjectHandle? FindPrivateKeyForCert(Pkcs11Session session, TokenCertificate cert)
            {
                if (cert.Id == null || cert.Id.Length == 0) return null;

                var searchTemplate = new List<IObjectAttribute>
                {
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY),
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, cert.Id)
                };

                return session.FindAllObjects(searchTemplate).FirstOrDefault();
            }

            private static int ExpectedDigestLength(string d) => d switch
            {
                DigestAlgorithms.SHA1 => 20,
                DigestAlgorithms.SHA256 => 32,
                DigestAlgorithms.SHA384 => 48,
                DigestAlgorithms.SHA512 => 64,
                _ => 32
            };

            private static byte[] ComputeHash(byte[] data, string d) => d switch
            {
                DigestAlgorithms.SHA1 => SHA1.Create().ComputeHash(data),
                DigestAlgorithms.SHA384 => SHA384.Create().ComputeHash(data),
                DigestAlgorithms.SHA512 => SHA512.Create().ComputeHash(data),
                _ => SHA256.Create().ComputeHash(data),
            };

            private static HashAlgorithmName ToHashAlg(string d) => d switch
            {
                DigestAlgorithms.SHA1 => HashAlgorithmName.SHA1,
                DigestAlgorithms.SHA256 => HashAlgorithmName.SHA256,
                DigestAlgorithms.SHA384 => HashAlgorithmName.SHA384,
                DigestAlgorithms.SHA512 => HashAlgorithmName.SHA512,
                _ => HashAlgorithmName.SHA256
            };

            private static byte[] BuildDigestInfo(string digest, byte[] hash)
            {
                var oid = digest switch
                {
                    DigestAlgorithms.SHA1 => X509ObjectIdentifiers.IdSha1,
                    DigestAlgorithms.SHA256 => NistObjectIdentifiers.IdSha256,
                    DigestAlgorithms.SHA384 => NistObjectIdentifiers.IdSha384,
                    DigestAlgorithms.SHA512 => NistObjectIdentifiers.IdSha512,
                    _ => NistObjectIdentifiers.IdSha256
                };
                var algId = new AlgorithmIdentifier(oid, DerNull.Instance);
                var di = new Org.BouncyCastle.Asn1.X509.DigestInfo(algId, hash);
                return di.GetDerEncoded();
            }

            private static byte[] BuildPkcs1V15Block(int k, byte[] digestInfo)
            {
                if (k < digestInfo.Length + 11)
                    throw new Exception($"DigestInfo ({digestInfo.Length}B) ve PKCS#1 v1.5 dolgusu, anahtar boyutu ({k}B) için çok uzun.");

                var block = new byte[k];
                block[0] = 0x00;
                block[1] = 0x01;
                int psLen = k - digestInfo.Length - 3;
                for (int i = 0; i < psLen; i++) block[2 + i] = 0xFF;
                block[2 + psLen] = 0x00;
                Buffer.BlockCopy(digestInfo, 0, block, 3 + psLen, digestInfo.Length);
                return block;
            }
        }
    }
}