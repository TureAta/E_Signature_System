# E-imza uygulamasini calistirma

Uygulama iki farkli ortamda birlikte calisir:

- Kullanici arayuzu, Spring Boot, PostgreSQL, MinIO ve Adminer Docker Compose icinde
- C# imzalama servisi Windows host uzerinde

C# servisinin Windows'ta calismasi gerekir; akilli kart surucusunun PKCS#11 DLL dosyasini ve USB tokeni burada kullanir. Linux container icinde Windows DLL dosyalari yuklenemez.

Kullanicilar uygulamaya su adresten erisir:

```text
http://localhost:3000
```

Ayni agdaki baska bilgisayarlar `localhost` yerine uygulamanin calistigi bilgisayarin IP adresini kullanir: `http://BILGISAYAR-IP:3000`. Backend, PostgreSQL, MinIO ve Adminer portlari guvenlik icin yalnizca uygulamanin calistigi bilgisayardan erisilebilir.

> Guvenlik: `3000` adresi varsayilan olarak HTTP kullanir. Uygulamayi internet uzerinden yayinlamayin. Guvenilmeyen veya ortak bir agda kullanim icin onune HTTPS sonlandiran bir ters proxy ve guvenilir TLS sertifikasi koyun.

Ilk baslatmada PostgreSQL, MinIO, JWT ve imzalama servisi icin rastgele guvenli anahtarlar `e-imza-backend/.env` dosyasinda otomatik olusturulur. Bu dosyayi paylasmayin veya Git'e eklemeyin. Anahtarlar kaybolursa mevcut oturumlar ve servis baglantilari gecersiz olur.

Tum servisleri tek komutla baslatmak icin PowerShell acip bu klasorde sunu calistirin:

```powershell
.\start-all.ps1
```

Tum servisleri durdurmak icin:

```powershell
.\stop-all.ps1
```

## PowerShell script izni

Komutlari `e-imza` ana klasorunde calistirin. `e-imza-backend` klasorundeyseniz once bir ust klasore donun:

```powershell
cd ..
```

`running scripts is disabled on this system` veya `PSSecurityException` hatasi alirsaniz PowerShell script calistirma politikasi devre disidir.

Onerilen kalici cozum, yalnizca kendi Windows kullaniciniz icin yerel scriptlere izin vermektir. Bu islem yonetici yetkisi istemez:

```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned -Force
Unblock-File .\start-all.ps1
Unblock-File .\stop-all.ps1
```

Bundan sonra scriptleri normal sekilde calistirabilirsiniz:

```powershell
.\start-all.ps1
.\stop-all.ps1
```

PowerShell politikasini kalici olarak degistirmek istemiyorsaniz mevcut terminal oturumu icin gecici izin verebilirsiniz. Bu izin terminal kapatildiginda sifirlanir:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\start-all.ps1
```

Tek bir komut icin izin vermek de mumkundur:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\start-all.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\stop-all.ps1
```

Etkin PowerShell politikalarini kontrol etmek icin:

```powershell
Get-ExecutionPolicy -List
```

Ilk baslatma Docker imajlari ve NuGet/Maven paketleri indirilecegi icin daha uzun surebilir.

Adminer ve Swagger varsayilan olarak kapatilmistir. Gecici veritabani yonetimi gerektiginde yalnizca yerel bilgisayarda su komutla Adminer baslatilabilir:

```powershell
cd .\e-imza-backend
docker compose --profile tools up -d adminer
```
