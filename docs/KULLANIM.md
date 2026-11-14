# Kullanım Kılavuzu ve Sunum Akışı

## Çalıştırma
```
dotnet run --project RandevuSistemi            # etkileşimli menü
dotnet run --project RandevuSistemi -- --demo   # otomatik senaryo (kuralları sırayla gösterir)
```

## Giriş ekranı
Açılışta kullanıcı seçilir. Örnek veriyle gelen kullanıcılar:

| No | Kullanıcı | Rol |
|---|---|---|
| 1 | Sistem Yöneticisi | Tüm kurumları yönetir, yeni hastane/klinik ekleyebilir |
| 2 | Hastane Yöneticisi | Yalnızca Sakarya Şehir Hastanesi'ni yönetir |
| 3 | Klinik Yöneticisi | Yalnızca Mavi Diş Kliniği'ni yönetir |
| 4-8 | Ayşe Yılmaz, Mehmet Kara, Zeynep Demir, Can Öztürk, Elif Şahin | Doktor / sağlık personeli |
| 9-10 | Ali Veli, Fatma Gül | Hasta |
| K | Yeni hasta kaydı | |

## Önerilen sunum akışı (yaklaşık 5 dakika)

1. **Otomatik senaryo:** `--demo` ile başarılı randevu, çakışma, uygun olmayan saat, sunulmayan hizmet,
   yetkisiz onay, başka kurum yöneticisinin reddedilmesi, onay ve iptal sonrası yeniden randevu gösterilir.
2. **Hasta olarak randevu alma:** `9` (Ali Veli) -> `1` Randevu al -> şube -> çalışan -> hizmet -> tarih
   (`gg.aa.yyyy`) -> boş saatlerden biri. Randevu "Beklemede" oluşur.
3. **Doktor onayı:** Çıkış yap, `4` (Ayşe Yılmaz) -> `2` Randevu onayla. Hasta bildirim alır.
4. **Çakışma:** Aynı doktor ve saat için `10` (Fatma Gül) ile randevu dene. Saat boş listesinde görünmez.
5. **Yönetici tanımlamaları:** `1` (Sistem Yöneticisi) -> `4` yeni hastane, `5` şube, `7` çalışma saati,
   `8` hizmet (süre/ücret), `9` doktor, `10` hizmet atama, `11` uygunluk saati.
6. **Yetki sınırı:** `2` (Hastane Yöneticisi) -> `4` yeni kurum eklemeyi dener, reddedilir.
7. **Kayıt dosyası:** Çalışma klasöründeki `randevular.csv` dosyasını aç (işlem, süre, ücret, çalışan,
   tarih/saat, durum).

## Sunumda anlatılacak OOP noktaları
- **Kalıtım:** `Kullanici` -> `Hasta`, `Yonetici`, `Calisan` (-> `Doktor`, `SaglikPersoneli`); `Kurum` -> `Hastane`, `Klinik`.
- **Çok biçimlilik:** Aynı `Kullanici` referansı farklı menü başlığı ve onay yetkisi döndürür.
- **Interface:** `IRandevuDeposu` ve `IBildirimServisi`; `RandevuServisi` somut sınıfları bilmez.
  Depo `BellekRandevuDeposu` ya da `DosyaRandevuDeposu` olabilir.
- **Kapsülleme:** Listeler `private`, dışarıya `IReadOnlyList`; randevu durumu yalnızca servis üzerinden değişir.
- **Çakışma kontrolü:** `RandevuServisi.Olustur` ve `Randevu.Cakisir`.

## Bilinen sınırlar
- Kurum, kullanıcı ve çalışan verileri bellekte tutulur ve her açılışta örnek veriyle başlar.
- `randevular.csv` yalnızca kayıt amaçlıdır, açılışta geri okunmaz.
