using System.Globalization;
using RandevuSistemi.Models;

namespace RandevuSistemi.Services
{
    public static class RandevuWebApi
    {
        public static void MapEndpoints(WebApplication app, VeriYoneticisi veri, OturumServisi oturum)
        {
            var servis = veri.Servis!;

            // 1. Sistem Durumu
            app.MapGet("/api/durum", () => Results.Ok(new
            {
                durum = "calisiyor",
                sistem = "Hastane / Klinik Randevu Sistemi",
                zaman = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                veritabani = veri.VeritabaniBilgisi,
                sqlServerBagli = veri.SqlServerBagli
            }));

            // 1.1 Veritabanı Durumu (SSMS 20 Entegrasyonu)
            app.MapGet("/api/veritabani-durum", () => Results.Ok(new
            {
                tur = veri.SqlServerBagli ? "Microsoft SQL Server" : "JSON",
                bagli = veri.SqlServerBagli,
                aciklama = veri.VeritabaniBilgisi,
                scriptBilgisi = "veritabani.sql mevcut tabloları ve kayıtları silmeden eksik şemayı oluşturur."
            }));

            // 2. Giriş Yap
            app.MapPost("/api/auth/giris", (GirisIstegi istek) =>
            {
                if (string.IsNullOrWhiteSpace(istek.Kimlik) || string.IsNullOrWhiteSpace(istek.Sifre))
                    return Results.BadRequest(new { basarili = false, mesaj = "Kimlik ve şifre zorunludur." });

                var k = veri.GirisYap(istek.Kimlik.Trim(), istek.Sifre.Trim());
                if (k == null)
                    return Results.BadRequest(new { basarili = false, mesaj = "Hatalı kimlik veya şifre!" });

                var kimlik = veri.KimlikBulKullanici(k) ?? istek.Kimlik.Trim();
                return Results.Ok(new
                {
                    basarili = true,
                    token = oturum.Ac(kimlik),
                    kullanici = KullaniciDto(k, veri)
                });
            });

            // 3. Yeni Hasta Kaydı
            app.MapPost("/api/auth/kayit", (KayitIstegi istek) =>
            {
                if (string.IsNullOrWhiteSpace(istek.Ad) || string.IsNullOrWhiteSpace(istek.Soyad) ||
                    string.IsNullOrWhiteSpace(istek.TcNo) || string.IsNullOrWhiteSpace(istek.Sifre))
                {
                    return Results.BadRequest(new { basarili = false, mesaj = "Tüm alanlar zorunludur." });
                }

                if (istek.TcNo.Length != 11 || !istek.TcNo.All(char.IsDigit))
                    return Results.BadRequest(new { basarili = false, mesaj = "TC Kimlik No 11 haneli rakam olmalıdır." });

                var hasta = veri.HastaKaydet(istek.Ad.Trim(), istek.Soyad.Trim(), istek.TcNo.Trim(), istek.Sifre.Trim());
                if (hasta == null)
                    return Results.BadRequest(new { basarili = false, mesaj = "Bu TC Kimlik No ile kayıtlı hasta zaten var." });

                var kimlik = veri.KimlikBulKullanici(hasta) ?? "";
                return Results.Ok(new
                {
                    basarili = true,
                    mesaj = "Kayıt başarılı!",
                    token = oturum.Ac(kimlik),
                    kullanici = KullaniciDto(hasta, veri)
                });
            });

            app.MapPost("/api/auth/cikis", (HttpRequest req) =>
            {
                oturum.Kapat(req);
                return Results.Ok(new { basarili = true });
            });

            // 4. Kurumlar, Şubeler ve Kadro Listesi
            app.MapGet("/api/kurumlar", () =>
            {
                var sonuc = veri.Kurumlar.Select(k => new
                {
                    ad = k.Ad,
                    tur = k.Tur,
                    subeler = k.Subeler.Select(s => new
                    {
                        ad = s.Ad,
                        adres = s.Adres,
                        bolumler = s.Bolumler.Select(b => b.Ad).ToList(),
                        calismaSaatleri = s.CalismaSaatleri.Select(cs => cs.ToString()).ToList(),
                        hizmetler = s.Hizmetler.Select(h => new
                        {
                            ad = h.Ad,
                            sureDakika = h.SureDakika,
                            ucret = h.Ucret
                        }).ToList(),
                        calisanlar = s.Calisanlar.Select(c => new
                        {
                            kimlik = veri.KimlikBulCalisan(c),
                            adSoyad = c.AdSoyad,
                            rol = c.Rol.ToString(),
                            unvan = c is Doktor d ? $"Dr. ({d.Uzmanlik})" : (c is SaglikPersoneli sp ? sp.Unvan : "Personel"),
                            uzmanlik = c is Doktor doc ? doc.Uzmanlik : "",
                            hizmetler = c.Hizmetler.Select(h => h.Ad).ToList(),
                            uygunluklar = c.Uygunluklar.Select(u => u.ToString()).ToList()
                        }).ToList()
                    }).ToList()
                }).ToList();

                return Results.Ok(sonuc);
            });

            // 5. Boş Saat Slotlarını Sorgula
            app.MapGet("/api/bos-saatler", (string calisanKimlik, string hizmetAdi, string tarih) =>
            {
                if (string.IsNullOrWhiteSpace(calisanKimlik) || string.IsNullOrWhiteSpace(hizmetAdi) || string.IsNullOrWhiteSpace(tarih))
                    return Results.BadRequest(new { basarili = false, mesaj = "Eksik parametreler." });

                var calisan = veri.CalisanBul(calisanKimlik);
                if (calisan == null)
                    return Results.NotFound(new { basarili = false, mesaj = "Çalışan bulunamadı." });

                var hizmet = calisan.Hizmetler.FirstOrDefault(h => h.Ad.Equals(hizmetAdi, StringComparison.OrdinalIgnoreCase));
                if (hizmet == null)
                    return Results.NotFound(new { basarili = false, mesaj = "Bu çalışan belirtilen hizmeti sunmuyor." });

                DateTime gun;
                if (!DateTime.TryParseExact(tarih, new[] { "yyyy-MM-dd", "dd.MM.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out gun))
                {
                    if (!DateTime.TryParse(tarih, out gun))
                        return Results.BadRequest(new { basarili = false, mesaj = "Geçersiz tarih formatı (yyyy-MM-dd)." });
                }

                var boslar = servis.BosSaatler(calisan, hizmet, gun);
                return Results.Ok(new
                {
                    basarili = true,
                    tarih = gun.ToString("yyyy-MM-dd"),
                    calisan = calisan.AdSoyad,
                    hizmet = hizmet.Ad,
                    saatler = boslar.Select(b => b.ToString("HH:mm")).ToList()
                });
            });

            // 6. Randevuları Listele (Kullanıcıya göre filtrelenebilir)
            app.MapGet("/api/randevular", (HttpRequest req) =>
            {
                var k = oturum.KullaniciGetir(req, veri);
                if (k == null) return GirisGerekli();

                IEnumerable<Randevu> randevular = k switch
                {
                    Hasta h => servis.HastaRandevulari(h),
                    Calisan c => servis.CalisanRandevulari(c),
                    _ => servis.Tumu()
                };

                var liste = randevular.OrderByDescending(r => r.Baslangic).Select(r => RandevuDto(r, veri)).ToList();
                return Results.Ok(liste);
            });

            // 7. Randevu Oluştur
            app.MapPost("/api/randevular", (RandevuOlusturIstegi istek, HttpRequest req) =>
            {
                var oturan = oturum.KullaniciGetir(req, veri);
                if (oturan == null) return GirisGerekli();
                if (oturan is Hasta hastaOturum)
                {
                    var kendi = veri.KimlikBulHasta(hastaOturum);
                    if (!string.Equals(kendi, istek.HastaKimlik, StringComparison.Ordinal))
                        return Yasak();
                }
                else if (oturan is not Yonetici)
                {
                    return Yasak();
                }

                var hasta = veri.HastaBul(istek.HastaKimlik);
                if (hasta == null)
                    return Results.BadRequest(new { basarili = false, mesaj = "Hasta bulunamadı." });

                var calisan = veri.CalisanBul(istek.CalisanKimlik);
                if (calisan == null)
                    return Results.BadRequest(new { basarili = false, mesaj = "Çalışan bulunamadı." });

                var hizmet = calisan.Hizmetler.FirstOrDefault(h => h.Ad.Equals(istek.HizmetAdi, StringComparison.OrdinalIgnoreCase));
                if (hizmet == null)
                    return Results.BadRequest(new { basarili = false, mesaj = "Çalışanın böyle bir hizmeti bulunmuyor." });

                DateTime baslangic;
                if (!DateTime.TryParse(istek.Baslangic, CultureInfo.InvariantCulture, DateTimeStyles.None, out baslangic))
                {
                    if (!DateTime.TryParse(istek.Baslangic, out baslangic))
                        return Results.BadRequest(new { basarili = false, mesaj = "Geçersiz randevu başlangıç zamanı." });
                }

                try
                {
                    var randevu = servis.Olustur(hasta, calisan, hizmet, baslangic);
                    veri.RandevulariKaydet(randevu, yeniKayit: true);

                    return Results.Created($"/api/randevular/{randevu.Id}", new
                    {
                        basarili = true,
                        mesaj = "Randevu başarıyla oluşturuldu.",
                        randevu = RandevuDto(randevu, veri)
                    });
                }
                catch (RandevuHatasi ex)
                {
                    return Results.BadRequest(new { basarili = false, mesaj = ex.Message });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { basarili = false, mesaj = "Randevu oluşturulamadı: " + ex.Message });
                }
            });

            // 8. Randevu Onayla
            app.MapPost("/api/randevular/{id:int}/onayla", (int id, HttpRequest req) =>
            {
                var k = oturum.KullaniciGetir(req, veri);
                if (k == null) return GirisGerekli();
                var r = servis.RandevuBul(id);
                if (r == null) return Results.NotFound(new { basarili = false, mesaj = "Randevu bulunamadı." });

                try
                {
                    servis.Onayla(r, k);
                    veri.RandevulariKaydet(r);
                    return Results.Ok(new { basarili = true, mesaj = "Randevu onaylandı.", randevu = RandevuDto(r, veri) });
                }
                catch (RandevuHatasi ex)
                {
                    return Results.BadRequest(new { basarili = false, mesaj = ex.Message });
                }
            });

            // 9. Randevu Reddet
            app.MapPost("/api/randevular/{id:int}/reddet", (int id, HttpRequest req) =>
            {
                var k = oturum.KullaniciGetir(req, veri);
                if (k == null) return GirisGerekli();
                var r = servis.RandevuBul(id);
                if (r == null) return Results.NotFound(new { basarili = false, mesaj = "Randevu bulunamadı." });

                try
                {
                    servis.Reddet(r, k);
                    veri.RandevulariKaydet(r);
                    return Results.Ok(new { basarili = true, mesaj = "Randevu reddedildi.", randevu = RandevuDto(r, veri) });
                }
                catch (RandevuHatasi ex)
                {
                    return Results.BadRequest(new { basarili = false, mesaj = ex.Message });
                }
            });

            // 10. Randevu İptal Et
            app.MapPost("/api/randevular/{id:int}/iptal", (int id, HttpRequest req) =>
            {
                var k = oturum.KullaniciGetir(req, veri);
                if (k == null) return GirisGerekli();
                var r = servis.RandevuBul(id);
                if (r == null) return Results.NotFound(new { basarili = false, mesaj = "Randevu bulunamadı." });

                try
                {
                    servis.Iptal(r, k);
                    veri.RandevulariKaydet(r);
                    return Results.Ok(new { basarili = true, mesaj = "Randevu iptal edildi.", randevu = RandevuDto(r, veri) });
                }
                catch (RandevuHatasi ex)
                {
                    return Results.BadRequest(new { basarili = false, mesaj = ex.Message });
                }
            });

            // 11. Genel İstatistikler
            app.MapGet("/api/istatistikler", (HttpRequest req) =>
            {
                var yetki = YoneticiKontrol(oturum, req, veri);
                if (yetki != null) return yetki;

                var tum = servis.Tumu().ToList();
                var ciro = tum.Where(r => r.Durum == RandevuDurumu.Onaylandi).Sum(r => r.Ucret);

                return Results.Ok(new
                {
                    toplamRandevu = tum.Count,
                    beklemede = tum.Count(r => r.Durum == RandevuDurumu.Beklemede),
                    onaylandi = tum.Count(r => r.Durum == RandevuDurumu.Onaylandi),
                    reddedildi = tum.Count(r => r.Durum == RandevuDurumu.Reddedildi),
                    iptalEdildi = tum.Count(r => r.Durum == RandevuDurumu.IptalEdildi),
                    onaylananCiro = ciro,
                    kurumSayisi = veri.Kurumlar.Count,
                    subeSayisi = veri.Kurumlar.Sum(k => k.Subeler.Count),
                    calisanSayisi = veri.Calisanlar.Count,
                    hastaSayisi = veri.Hastalar.Count
                });
            });

            // 12. Yönetici: Yeni Tıbbi İşlem / Hizmet Ekleme
            app.MapPost("/api/yonetim/hizmet-ekle", (YeniHizmetIstegi istek, HttpRequest req) =>
            {
                var yetki = YoneticiKontrol(oturum, req, veri);
                if (yetki != null) return yetki;
                if (string.IsNullOrWhiteSpace(istek.KurumAdi) || string.IsNullOrWhiteSpace(istek.SubeAdi) ||
                    string.IsNullOrWhiteSpace(istek.HizmetAdi) || istek.SureDakika <= 0 || istek.Ucret < 0)
                {
                    return Results.BadRequest(new { basarili = false, mesaj = "Lütfen tüm alanları geçerli şekilde doldurun." });
                }

                try
                {
                    var h = veri.HizmetEkle(istek.KurumAdi.Trim(), istek.SubeAdi.Trim(), istek.HizmetAdi.Trim(), istek.SureDakika, istek.Ucret, istek.BolumAdi?.Trim());
                    return Results.Ok(new
                    {
                        basarili = true,
                        mesaj = $"'{h.Ad}' hizmeti başarıyla eklendi.",
                        hizmet = new { ad = h.Ad, sureDakika = h.SureDakika, ucret = h.Ucret }
                    });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { basarili = false, mesaj = ex.Message });
                }
            });

            // 13. Yönetici: Yeni Doktor / Sağlık Personeli Ekleme
            app.MapPost("/api/yonetim/doktor-ekle", (YeniDoktorIstegi istek, HttpRequest req) =>
            {
                var yetki = YoneticiKontrol(oturum, req, veri);
                if (yetki != null) return yetki;
                if (string.IsNullOrWhiteSpace(istek.KurumAdi) || string.IsNullOrWhiteSpace(istek.SubeAdi) ||
                    string.IsNullOrWhiteSpace(istek.Ad) || string.IsNullOrWhiteSpace(istek.Soyad) ||
                    string.IsNullOrWhiteSpace(istek.UzmanlikVeyaUnvan) || string.IsNullOrWhiteSpace(istek.Sifre))
                {
                    return Results.BadRequest(new { basarili = false, mesaj = "Lütfen gerekli alanları eksiksiz doldurun." });
                }

                try
                {
                    var uygunluklar = new List<(DayOfWeek Gun, TimeSpan Bas, TimeSpan Bit)>();
                    var bas = TimeSpan.Parse(istek.BaslangicSaati ?? "09:00");
                    var bit = TimeSpan.Parse(istek.BitisSaati ?? "17:00");

                    foreach (var gunStr in istek.Gunler ?? new List<string> { "Monday", "Wednesday", "Friday" })
                    {
                        if (Enum.TryParse<DayOfWeek>(gunStr, true, out var gun))
                        {
                            uygunluklar.Add((gun, bas, bit));
                        }
                    }

                    var rol = string.Equals(istek.Rol, "Personel", StringComparison.OrdinalIgnoreCase) ? "Personel" : "Doktor";
                    var calisan = veri.DoktorEkle(
                        istek.KurumAdi.Trim(),
                        istek.SubeAdi.Trim(),
                        istek.Ad.Trim(),
                        istek.Soyad.Trim(),
                        rol,
                        istek.UzmanlikVeyaUnvan.Trim(),
                        istek.Sifre.Trim(),
                        istek.HizmetAdlari ?? new List<string>(),
                        uygunluklar
                    );

                    return Results.Ok(new
                    {
                        basarili = true,
                        mesaj = $"{calisan.AdSoyad} sisteme ve veritabanına başarıyla kaydedildi.",
                        kimlik = veri.KimlikBulCalisan(calisan),
                        adSoyad = calisan.AdSoyad
                    });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { basarili = false, mesaj = ex.Message });
                }
            });

            // 14. Yönetici: Yeni Poliklinik / Bölüm Ekleme
            app.MapPost("/api/yonetim/bolum-ekle", (YeniBolumIstegi istek, HttpRequest req) =>
            {
                var yetki = YoneticiKontrol(oturum, req, veri);
                if (yetki != null) return yetki;
                if (string.IsNullOrWhiteSpace(istek.KurumAdi) || string.IsNullOrWhiteSpace(istek.SubeAdi) ||
                    string.IsNullOrWhiteSpace(istek.BolumAdi))
                {
                    return Results.BadRequest(new { basarili = false, mesaj = "Kurum, şube ve bölüm adı zorunludur." });
                }

                try
                {
                    var b = veri.BolumEkle(istek.KurumAdi.Trim(), istek.SubeAdi.Trim(), istek.BolumAdi.Trim());
                    return Results.Ok(new
                    {
                        basarili = true,
                        mesaj = $"'{b.Ad}' polikliniği başarıyla eklendi.",
                        bolumAdi = b.Ad
                    });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { basarili = false, mesaj = ex.Message });
                }
            });

            // 15. Yönetici: Yeni Kurum ve Şube Ekleme
            app.MapPost("/api/yonetim/kurum-ekle", (YeniKurumIstegi istek, HttpRequest req) =>
            {
                var yetki = YoneticiKontrol(oturum, req, veri);
                if (yetki != null) return yetki;
                if (string.IsNullOrWhiteSpace(istek.KurumAdi) || string.IsNullOrWhiteSpace(istek.SubeAdi))
                {
                    return Results.BadRequest(new { basarili = false, mesaj = "Kurum adı ve şube adı zorunludur." });
                }
                if (!string.Equals(istek.Tur, "Hastane", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(istek.Tur, "Klinik", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new { basarili = false, mesaj = "Kurum türü Hastane veya Klinik olmalıdır." });
                }

                try
                {
                    var (kurum, sube) = veri.KurumVeSubeGetirVeyaOlustur(
                        istek.KurumAdi.Trim(), istek.SubeAdi.Trim(), istek.Adres?.Trim(), istek.Tur);
                    return Results.Ok(new
                    {
                        basarili = true,
                        mesaj = $"'{kurum.Ad}' ({sube.Ad}) başarıyla kaydedildi.",
                        kurumAdi = kurum.Ad,
                        subeAdi = sube.Ad
                    });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { basarili = false, mesaj = ex.Message });
                }
            });

            app.MapPost("/api/yonetim/sube-calisma-saatleri", (SubeCalismaSaatleriIstegi istek, HttpRequest req) =>
            {
                var yetki = YoneticiKontrol(oturum, req, veri);
                if (yetki != null) return yetki;
                if (string.IsNullOrWhiteSpace(istek.KurumAdi) || string.IsNullOrWhiteSpace(istek.SubeAdi) ||
                    istek.Saatler == null || istek.Saatler.Count == 0)
                {
                    return Results.BadRequest(new { basarili = false, mesaj = "Kurum, şube ve en az bir çalışma günü seçin." });
                }

                try
                {
                    var saatler = new List<(DayOfWeek Gun, TimeSpan Baslangic, TimeSpan Bitis)>();
                    foreach (var saat in istek.Saatler)
                    {
                        if (!Enum.TryParse<DayOfWeek>(saat.Gun, true, out var gun) ||
                            !TimeSpan.TryParseExact(saat.BaslangicSaati, @"hh\:mm", CultureInfo.InvariantCulture, out var bas) ||
                            !TimeSpan.TryParseExact(saat.BitisSaati, @"hh\:mm", CultureInfo.InvariantCulture, out var bit) ||
                            bit <= bas)
                        {
                            return Results.BadRequest(new { basarili = false, mesaj = "Çalışma günü veya saat aralığı geçersiz." });
                        }
                        if (saatler.Any(x => x.Gun == gun))
                            return Results.BadRequest(new { basarili = false, mesaj = "Aynı gün için birden fazla saat aralığı girilemez." });
                        saatler.Add((gun, bas, bit));
                    }

                    veri.SubeCalismaSaatleriniGuncelle(istek.KurumAdi.Trim(), istek.SubeAdi.Trim(), saatler);
                    return Results.Ok(new { basarili = true, mesaj = "Şube çalışma saatleri kaydedildi." });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { basarili = false, mesaj = ex.Message });
                }
            });
        }

        private static IResult? YoneticiKontrol(OturumServisi oturum, HttpRequest req, VeriYoneticisi veri)
        {
            var k = oturum.KullaniciGetir(req, veri);
            if (k == null) return GirisGerekli();
            return k is Yonetici ? null : Yasak();
        }

        private static IResult GirisGerekli() =>
            Results.Json(new { basarili = false, mesaj = "Giriş yapmanız gerekiyor." }, statusCode: StatusCodes.Status401Unauthorized);

        private static IResult Yasak() =>
            Results.Json(new { basarili = false, mesaj = "Bu işlem için yetkiniz yok." }, statusCode: StatusCodes.Status403Forbidden);

        // DTO Dönüştürücüler
        private static object KullaniciDto(Kullanici k, VeriYoneticisi veri)
        {
            var kimlik = veri.KimlikBulKullanici(k) ?? "";
            string unvan = "";
            string? tcNo = null;

            if (k is Doktor d) unvan = $"Dr. ({d.Uzmanlik})";
            else if (k is SaglikPersoneli sp) unvan = sp.Unvan;
            else if (k is Yonetici) unvan = "Yönetici";
            else if (k is Hasta h) { unvan = "Hasta"; tcNo = h.TcNo; }

            return new
            {
                kimlik,
                ad = k.Ad,
                soyad = k.Soyad,
                adSoyad = k.AdSoyad,
                rol = k.Rol.ToString(),
                unvan,
                tcNo,
                randevuOnaylayabilir = k.RandevuOnaylayabilir
            };
        }

        private static object RandevuDto(Randevu r, VeriYoneticisi veri)
        {
            var hastaKimlik = veri.KimlikBulHasta(r.Hasta) ?? "";
            var calisanKimlik = veri.KimlikBulCalisan(r.Calisan) ?? "";

            return new
            {
                id = r.Id,
                hastaKimlik,
                hastaAdSoyad = r.Hasta.AdSoyad,
                hastaTcNo = r.Hasta.TcNo,
                calisanKimlik,
                calisanAdSoyad = r.Calisan.AdSoyad,
                calisanUnvan = r.Calisan is Doktor d ? d.Uzmanlik : (r.Calisan is SaglikPersoneli sp ? sp.Unvan : "Personel"),
                subeAdi = r.Calisan.Sube?.Ad ?? "Bilinmiyor",
                kurumAdi = r.Calisan.Sube?.Kurum.Ad ?? "Bilinmiyor",
                hizmetAdi = r.Hizmet.Ad,
                sureDakika = r.Hizmet.SureDakika,
                ucret = r.Ucret,
                baslangic = r.Baslangic.ToString("yyyy-MM-ddTHH:mm:ss"),
                bitis = r.Bitis.ToString("yyyy-MM-ddTHH:mm:ss"),
                tarihFormatli = r.Baslangic.ToString("dd.MM.yyyy HH:mm"),
                bitisFormatli = r.Bitis.ToString("HH:mm"),
                durum = r.Durum.ToString(),
                aktif = r.Aktif
            };
        }
    }

    // İstek Modelleri
    public record GirisIstegi(string Kimlik, string Sifre);
    public record KayitIstegi(string Ad, string Soyad, string TcNo, string Sifre);
    public record RandevuOlusturIstegi(string HastaKimlik, string CalisanKimlik, string HizmetAdi, string Baslangic);
    public record IslemIstegi(string YapanKimlik);
    public record YeniHizmetIstegi(string KurumAdi, string SubeAdi, string HizmetAdi, int SureDakika, decimal Ucret, string? BolumAdi);
    public record YeniDoktorIstegi(string KurumAdi, string SubeAdi, string Ad, string Soyad, string Rol, string UzmanlikVeyaUnvan, string Sifre, List<string>? HizmetAdlari, List<string>? Gunler, string? BaslangicSaati, string? BitisSaati);
    public record YeniBolumIstegi(string KurumAdi, string SubeAdi, string BolumAdi);
    public record YeniKurumIstegi(string KurumAdi, string SubeAdi, string? Tur, string? Adres);
    public record SubeCalismaSaatiIstegi(string Gun, string BaslangicSaati, string BitisSaati);
    public record SubeCalismaSaatleriIstegi(string KurumAdi, string SubeAdi, List<SubeCalismaSaatiIstegi>? Saatler);
}

