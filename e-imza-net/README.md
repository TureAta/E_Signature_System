# İmzaKasa .NET İmzalama Servisi

Windows üzerinde çalışan yerel imzalama servisidir. PKCS#11 uyumlu USB tokenı algılar, token sertifikasını kullanır ve seçilen konuma görünür PDF imzası ekler.

Bu servis USB token üreticisinin Windows sürücüsüne ve PKCS#11 kütüphanesine ihtiyaç duyduğu için Docker container'ı içinde çalıştırılmaz.

## Çalıştırma

Önerilen yöntem proje kökündeki betiği kullanmaktır:

```powershell
cd ..
.\start-all.ps1
```

Yalnızca geliştirme amacıyla derlemek için:

```powershell
dotnet build .\EimzaSignerService.csproj -c Release
```

## Güvenlik sınırları

- Servis yalnızca yerel ağ arayüzünde dinlemelidir.
- İstekler backend ile paylaşılan, rastgele oluşturulmuş servis anahtarıyla doğrulanır.
- PIN kalıcı depolamaya veya loglara yazılmaz.
- Geçici PDF dosyaları işlem sonrasında silinir.
- Özel anahtar token dışına çıkarılmaz.
- Yanlış PIN denemeleri tokenı kilitleyebileceğinden otomatik veya rastgele PIN testi yapılmamalıdır.
- Gerçek imza testi yalnızca token sahibi PIN'i doğrudan yerel arayüze girdiğinde yapılmalıdır.

Token üreticisine özel kütüphane dosyaları, sertifikalar, loglar ve imzalanmış test belgeleri kaynak kontrolüne eklenmemelidir.
