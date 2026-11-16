# Hastane / Klinik Randevu Portalı & Yönetim Sistemi

Nesne Yönelimli Programlama (OOP) prensiplerini uygulayan, çok şubeli, modern web arayüzüne ve JSON tabanlı kalıcı veri setine sahip Hastane ve Klinik Randevu Sistemi.

---

## 🌟 Öne Çıkan Özellikler

1. **Modern Web Arayüzü (Frontend):**
   - **Gerçek Zamanlı Randevu Sihirbazı:** Kurum -> Şube -> Poliklinik/Hekim -> Hizmet -> Tarih -> Boş Saat Slotu seçimi ve anında çakışma kontrolü.
   - **Rol Tabanlı Portallar:** Hasta, Doktor, Sağlık Personeli ve Yönetici için özel tasarlanmış ekranlar.
   - **Rol Tabanlı Oturumlar:** Hastalar hesap oluşturup giriş yapabilir; hekim/personel ve yönetici hesapları yetkili yönetici tarafından tanımlanır.
   - **Yönetici Paneli:** Canlı ciro, toplam/onaylanan/bekleyen randevu istatistikleri ve tüm randevuları yönetme.
   - **Doktor Paneli:** Gelen talepleri tek tıkla onaylama/reddetme, çalışma saatlerini ve randevu geçmişini görme.

2. **Backend & REST API (ASP.NET Core Web API):**
   - `GET /api/kurumlar` — Kurum, şube, hizmet tarifesi ve kadro ağacı
   - `GET /api/bos-saatler` — Hekim ve şubenin mesai saatlerine göre boş randevu slotları
   - `GET /api/randevular` — Kullanıcı bazlı veya tüm randevuların listesi
   - `POST /api/randevular` — Çakışma, mesai ve yetki kurallarına göre yeni randevu oluşturma
   - `POST /api/randevular/{id}/onayla` & `reddet` & `iptal` — Randevu yaşam döngüsü
   - `GET /api/istatistikler` — Canlı KPI ve ciro metrikleri

3. **Veritabanı Desteği (Microsoft SQL Server & SSMS 20):**
   - **SQL Server Entegrasyonu:** `Microsoft.Data.SqlClient` ile MSSQL veritabanı desteği eklenmiştir.
   - **SQL ve JSON:** SQL Server kurum, kullanıcı, çalışan ve hizmet ana verilerini kalıcı tutar; yerel JSON dosyası yedek olarak korunur. Randevular SQL tablosunda saklanır.
   - **Güvenli T-SQL Şeması:** `veritabani.sql` tabloları mevcut değilse oluşturur; mevcut tabloları veya kayıtları silmez ve örnek parola eklemez.
   - **Geri Dönüş:** SQL Server kullanılamadığında randevular JSON dosyasına yazılır.

4. **Konsol Modu Desteği:**
   - İstenirse `dotnet run --project RandevuSistemi -- --console` komutuyla klasik konsol menüsü olarak da çalıştırılabilir.

---

## 🚀 Hızlı Başlangıç

### Yöntem 1: Çift Tıklama (Windows)
Proje klasöründeki **`calistir.bat`** dosyasına çift tıklayın. Tarayıcınız otomatik olarak `http://localhost:5000` adresinde açılacaktır.

### Yöntem 2: Terminal / Komut Satırı
```bash
dotnet run --project RandevuSistemi
```
Ardından tarayıcınızdan **`http://localhost:5000`** adresine gidin.

*(Konsol modunu çalıştırmak için: `dotnet run --project RandevuSistemi -- --console`)*

---

## 🔑 Kurulumda Tanımlı Hesaplar

| Rol | Kimlik | Şifre | Tanım |
|---|---|---|---|
| **Hasta** | `h1` | `1234` | Ali Demir |
| **Hasta** | `h2` | `1234` | Ayşe Kaya |
| **Doktor** | `d1` | `doktor1` | Dr. Mehmet Öztürk (Kardiyoloji Uzmanı) |
| **Doktor** | `d2` | `doktor2` | Dr. Fatma Arslan (Nöroloji Uzmanı) |
| **Doktor** | `d3` | `doktor3` | Dr. Hasan Çelik (Ortopedi Uzmanı) |
| **Personel** | `p1` | `pers1` | Hemşire Zeynep Kıran |
| **Yönetici** | `y1` | `admin123` | Ahmet Yönetici (Tüm Yetkiler) |

---

## 🏛️ Nesne Yönelimli Programlama (OOP) Mimarisi

| Prensip | Kod Tabanındaki Uygulama |
|---|---|
| **Kalıtım (Inheritance)** | `Kullanici` soyut sınıfından `Hasta`, `Calisan` ve `Yonetici` türer. `Calisan`'dan `Doktor` ve `SaglikPersoneli`; `Kurum`'dan `Hastane` ve `Klinik` türer. |
| **Çok Biçimlilik (Polymorphism)** | `MenuBasligi()`, `RandevuOnaylayabilir` ve `Rol` özellikleri her alt sınıfta `override` edilerek kendi rol kurallarını işletir. |
| **Kapsülleme (Encapsulation)** | Liste alanları dış dünyaya `IReadOnlyList<T>` ile açılır; randevu durum geçişleri `Onayla()`, `Reddet()`, `Iptal()` metodları ile kapsüllenir. |
| **Soyutlama & Arayüzler (Interfaces & DI)** | `IRandevuDeposu` ve `IBildirimServisi` arayüzleri sayesinde veri depolama ve bildirim mekanizmaları birbirinden izole edilmiştir. |

---

## 📁 Proje Dosya Yapısı

```
RandevuSistemi/
├── data/
│   ├── veri.json               ← Kurum, şube, hekim, kullanıcı veri seti
│   └── randevular.json         ← Kalıcı randevu kayıtları
├── Models/
│   ├── Enums.cs                ← Rol, RandevuDurumu
│   ├── Kullanici.cs            ← Soyut taban sınıf
│   ├── Hasta.cs
│   ├── Calisan.cs              ← Doktor + SaglikPersoneli
│   ├── Yonetici.cs
│   ├── Kurum.cs                ← Hastane + Klinik
│   ├── Sube.cs
│   ├── Bolum.cs
│   ├── Hizmet.cs
│   ├── CalismaSaati.cs
│   └── Randevu.cs
├── Services/
│   ├── Arayuzler.cs            ← IRandevuDeposu, IBildirimServisi
│   ├── Depolar.cs              ← BellekRandevuDeposu, KonsolBildirim
│   ├── RandevuHatasi.cs        ← İş kuralı exception sınıfı
│   ├── RandevuServisi.cs       ← Çakışma, yetki ve slot hesaplama motoru
│   ├── VeriYoneticisi.cs       ← JSON veri yöneticisi
│   ├── RandevuWebApi.cs        ← REST API uç noktaları
│   └── KonsolArayuz.cs         ← Konsol arayüzü desteği
├── wwwroot/                    ← Web Frontend (SPA)
│   ├── index.html              ← Responsive web sitesi
│   ├── css/style.css           ← Özel stil ve animasyonlar
│   └── js/app.js               ← Dinamik UI etkileşimleri ve API entegrasyonu
├── Program.cs                  ← Web sunucusu başlatıcı
└── RandevuSistemi.csproj       ← Microsoft.NET.Sdk.Web yapılandırması
```
