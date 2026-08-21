# İmzaKasa Backend

İmzaKasa'nın Spring Boot tabanlı API katmanıdır. Kullanıcı kimlik doğrulaması, kullanıcıya özel belge yetkilendirmesi, PostgreSQL veri erişimi, MinIO depolaması ve yerel imzalama servisiyle iletişim bu bileşende yürütülür.

Normal kullanımda servisleri ayrı ayrı başlatmak yerine proje kökündeki `start-all.ps1` betiğini kullanın.

## Yerel geliştirme

Gerekli sırlar `e-imza-backend/.env` içinde tutulur. Dosya yoksa proje kökündeki güvenli başlatma betiği dosyayı otomatik oluşturur.

```powershell
cd ..
.\start-all.ps1
```

Servis durumunu kontrol etmek için:

```powershell
cd .\e-imza-backend
docker compose ps
docker compose logs -f backend
```

## Güvenlik

- `.env` dosyasını veya gerçek ortam değerlerini commit etmeyin.
- Yüklenen belgeleri, veritabanı dökümlerini ve MinIO verilerini kaynak kontrolüne eklemeyin.
- Swagger ve Adminer varsayılan çalışma akışında dış erişime açılmamalıdır.
- Uygulamayı internete doğrudan açmayın; üretimde HTTPS ve güvenilir ters proxy kullanın.
- Kullanıcı belge yetkilendirmesi yalnızca UI tarafına bırakılmamalı, backend ve veri sorgusu katmanında uygulanmalıdır.

Ortam değişkenlerinin isimleri ve örnek biçimleri `.env.example` dosyasında yer alır; gerçek değer içermez.
