# İmzaKasa

İmzaKasa; PDF belgelerini kullanıcı bazında saklamak, yönetmek ve PKCS#11 uyumlu USB e-imza tokenı ile imzalamak için geliştirilmiş, kendi altyapınızda çalıştırılabilen bir belge yönetim uygulamasıdır.

> Bu proje geliştirme ve yerel ağ kullanımı için hazırlanmıştır. İnternete doğrudan açılmamalıdır. Üretim ortamında HTTPS, güvenilir bir ters proxy, erişim kısıtlamaları ve düzenli güvenlik güncellemeleri kullanılmalıdır.

## Özellikler

- Kullanıcı kaydı ve güvenli oturum yönetimi
- Kullanıcıya özel belge erişimi ve MinIO depolama alanı
- PDF dosya türü, boyut ve içerik doğrulaması
- PKCS#11 destekli USB token ile görünür PDF imzası
- PIN bilgisini kaydetmeden yerel imzalama
- Docker Compose ile frontend, backend, PostgreSQL ve MinIO kurulumu
- Servis anahtarı, istek sınırlandırma ve aynı kaynak kontrolleri
- Tek komutla başlatma ve durdurma

## Mimari

| Bileşen | Teknoloji | Çalışma ortamı |
| --- | --- | --- |
| Kullanıcı arayüzü | HTML, CSS, JavaScript, Nginx | Docker |
| Uygulama API'si | Java, Spring Boot | Docker |
| Veritabanı | PostgreSQL | Docker |
| Belge depolama | MinIO | Docker |
| İmzalama servisi | .NET, iText, Pkcs11Interop | Windows host |

.NET imzalama servisi USB token sürücüsüne erişebilmek için Windows üzerinde çalışır. Token üreticisinin PKCS#11 kütüphanesinin sistemde kurulu olması gerekir.

## Gereksinimler

- Windows 10 veya 11
- Docker Desktop
- .NET 8 SDK
- PowerShell 5.1 veya daha yeni bir sürüm
- Gerçek imza için PKCS#11 uyumlu USB token ve üretici sürücüsü

## Kurulum

Depoyu klonladıktan sonra proje kökünde çalıştırın:

```powershell
.\start-all.ps1
```

İlk çalıştırmada gerekli servis sırları `e-imza-backend/.env` dosyasında kriptografik olarak güvenli ve rastgele değerlerle oluşturulur. Bu dosya Git tarafından dışlanır; paylaşılmamalı, ekran görüntülerine eklenmemeli ve kaynak kontrolüne alınmamalıdır.

Uygulama hazır olduğunda kullanıcı arayüzü şu adrestedir:

```text
http://localhost:3000
```

Servisleri durdurmak için:

```powershell
.\stop-all.ps1
```

Veritabanı ve belge verileri Docker volume'larında korunur.

## Ortam değişkenleri

Gerçek değerler yalnızca yerel `.env` dosyasında bulunmalıdır. Güvenli değişken adları ve örnek biçim için `e-imza-backend/.env.example` kullanılabilir.

| Değişken | Amaç |
| --- | --- |
| `POSTGRES_PASSWORD` | PostgreSQL servis parolası |
| `MINIO_ROOT_USER` | MinIO yönetici kullanıcı adı |
| `MINIO_ROOT_PASSWORD` | MinIO yönetici parolası |
| `JWT_SECRET` | Oturum belirteci imzalama anahtarı |
| `SIGNER_API_KEY` | Backend ile yerel imzalama servisi arasındaki anahtar |

README, kaynak kod veya Docker Compose dosyasına gerçek parola yazmayın.

## PowerShell izin hatası

`running scripts is disabled on this system` hatası alınırsa yalnızca mevcut kullanıcı için yerel betiklere izin verilebilir:

```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
Unblock-File .\start-all.ps1
Unblock-File .\stop-all.ps1
```

Kalıcı ayar değiştirmeden tek çalıştırma için:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\start-all.ps1
```

## Güvenlik notları

- USB token PIN'i uygulama tarafından saklanmaz; yalnızca imza işlemi sırasında yerel servise iletilir.
- `.env`, kullanıcı belgeleri, sertifikalar, özel anahtarlar, loglar ve çalışma dosyaları Git ve Docker build context'lerinden dışlanmıştır.
- Backend, PostgreSQL, MinIO ve yönetim portları varsayılan olarak yalnızca yerel bilgisayara bağlanır.
- Kullanıcı arayüzünün `3000` portu yerel ağ erişimine açıktır. Güvenilmeyen ağlarda güvenlik duvarıyla sınırlandırılmalıdır.
- Token PIN'i sohbet, issue, log veya ekran görüntüsü üzerinden paylaşılmamalıdır.
- USB token olmadan gerçek imza işlemi uçtan uca doğrulanamaz.

## Proje yapısı

```text
e-imza/
├── e-imza-ui/       Kullanıcı arayüzü ve Nginx yapılandırması
├── e-imza-backend/  Spring Boot API ve Docker Compose
├── e-imza-net/      Windows üzerinde çalışan imzalama servisi
├── start-all.ps1    Tüm servisleri başlatır
└── stop-all.ps1     Tüm servisleri durdurur
```

## Katkı

Değişiklik göndermeden önce gerçek sırların ve kullanıcı belgelerinin commit'e dahil olmadığını kontrol edin. Güvenlik açıklarını herkese açık issue yerine depo sahibine özel bir kanaldan bildirin.
