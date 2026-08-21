using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using Pkcs11Session = Net.Pkcs11Interop.HighLevelAPI.ISession;


namespace EimzaSignerService
{
   
    public class Pkcs11Service : IDisposable
    {
        private readonly Pkcs11InteropFactories _factories = new Pkcs11InteropFactories();
        private IPkcs11Library? _lib;
        private bool _disposed;

        public bool IsReady => _lib != null;
        public string LastLoadError { get; private set; } = "";

        // DÜZELTME: Yapıcı (constructor) metodu daha esnek yükelem yapacak şekilde güncellendi.
        public Pkcs11Service(string libraryPath)
        {
            if (string.IsNullOrWhiteSpace(libraryPath) || !File.Exists(libraryPath))
            {
                LastLoadError = $"PKCS#11 DLL bulunamadı: {libraryPath}";
                _lib = null;
                return;
            }

            // 1) Önce MultiThreaded modunu dene. Çoğu modern sürücü bunu destekler.
            if (TryLoadLibrary(libraryPath, AppType.MultiThreaded, out _lib, out string err1))
            {
                LastLoadError = "";
                return;
            }

            // 2) Eğer ilk deneme başarısız olursa, SingleThreaded modunu dene.
            // Bazı eski veya katı sürücüler bu moda ihtiyaç duyar.
            if (TryLoadLibrary(libraryPath, AppType.SingleThreaded, out _lib, out string err2))
            {
                LastLoadError = "";
                return;
            }

            // 3) İkisi de başarısız olursa, detaylı hata mesajı oluştur.
            LastLoadError = $"Multi-Threaded Hata: {err1} | Single-Threaded Hata: {err2}";
            _lib = null;
        }

        // YENİ METOT: Kütüphaneyi belirli bir modda yüklemeyi dener ve sonucu bildirir.
        private bool TryLoadLibrary(string path, AppType appType, out IPkcs11Library? lib, out string error)
        {
            lib = null;
            error = "";
            try
            {
                // Pkcs11LibraryFactory.LoadPkcs11Library metodu kütüphaneyi yükler ve C_Initialize fonksiyonunu çağırır.
                lib = _factories.Pkcs11LibraryFactory.LoadPkcs11Library(_factories, path, appType);
                return true;
            }
            catch (Pkcs11Exception ex)
            {
                // Hata kodunu string olarak çevirerek daha anlaşılır hale getiriyoruz.
                error = $"PKCS11 Hatası: {ex.RV} ({(CKR)ex.RV})";
                lib?.Dispose(); // Başarısız yükleme sonrası kaynakları temizle.
                return false;
            }
            catch (Exception ex)
            {
                error = $"Genel Hata: {ex.GetType().Name} - {ex.Message}";
                lib?.Dispose();
                return false;
            }
        }

        public List<TokenCertificate> GetCertificates()
        {
            var result = new List<TokenCertificate>();
            if (_lib == null) return result;

            // Bu satır hata veriyordu. Artık vermemesi gerekiyor.
            var slots = _lib.GetSlotList(SlotsType.WithTokenPresent);

            foreach (var slot in slots)
            {
                using var session = slot.OpenSession(SessionType.ReadOnly);

                var template = new List<IObjectAttribute>
                {
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_CERTIFICATE),
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CERTIFICATE_TYPE, CKC.CKC_X_509)
                };

                foreach (var h in session.FindAllObjects(template))
                {
                    try
                    {
                        var attrs = session.GetAttributeValue(h, new List<CKA> { CKA.CKA_LABEL, CKA.CKA_ID, CKA.CKA_VALUE });

                        string label = attrs[0].GetValueAsString() ?? "";
                        byte[]? id = attrs[1].GetValueAsByteArray();
                        byte[]? raw = attrs[2].GetValueAsByteArray();
                        if (raw == null || raw.Length == 0) continue;

                        var cert = new X509Certificate2(raw);

                        // Sadece geçerli (süresi dolmamış) sertifikaları ekle
                        if (cert.NotAfter > DateTime.Now && cert.NotBefore < DateTime.Now)
                        {
                            result.Add(new TokenCertificate
                            {
                                Label = string.IsNullOrWhiteSpace(label) ? "(Etiketsiz)" : label,
                                Id = id,
                                Slot = slot,
                                X509 = cert
                            });
                        }
                    }
                    catch
                    {
                        // Belirli bir sertifika okunamadıysa atla ve devam et.
                    }
                }
            }

            return result.OrderBy(c => c.X509?.GetNameInfo(X509NameType.SimpleName, false)).ToList();
        }

        public void LoginSmart(Pkcs11Session session, string pin)
        {
            if (_lib == null)
                throw new Exception("E-imza kütüphanesi yüklenemedi. "
                  + "Doğru PKCS#11 DLL yolunu seçin. Ayrıntı: " + LastLoadError);

            if (session == null) throw new ArgumentNullException(nameof(session));

            string cleanPin = NormalizePin(pin);
            try
            {
                session.Login(CKU.CKU_USER, cleanPin);
            }
            catch (Pkcs11Exception ex) when (ex.RV == CKR.CKR_USER_ALREADY_LOGGED_IN)
            {
                // Zaten giriş yapılmış, sorun yok.
            }
            catch (Pkcs11Exception ex) when (ex.RV == CKR.CKR_PIN_INCORRECT)
            {
                throw new Exception("PIN hatalı. Lütfen kontrol edip tekrar deneyin.");
            }
            catch (Pkcs11Exception ex) when (ex.RV == CKR.CKR_PIN_LOCKED)
            {
                throw new Exception("PIN bloke edilmiş. Kart sağlayıcınızla görüşerek kilidi açtırmanız gerekmektedir.");
            }
        }

        private static string NormalizePin(string pin)
        {
            if (string.IsNullOrWhiteSpace(pin)) return string.Empty;
            var sb = new StringBuilder(pin.Length);
            foreach (var ch in pin.Trim())
            {
                if (ch >= '0' && ch <= '9') sb.Append(ch);
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            if (_disposed) return;
            try { _lib?.Dispose(); } catch { }
            _lib = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}