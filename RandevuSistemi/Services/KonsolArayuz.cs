using RandevuSistemi.Models;

namespace RandevuSistemi.Services
{
    /// <summary>
    /// Çok katmanlı konsol arayüzü: Hasta, Doktor/Personel ve Yönetici rolleri
    /// için tam etkileşimli menü sistemi sağlar.
    /// </summary>
    public class KonsolArayuz
    {
        private readonly VeriYoneticisi _veri;
        private Kullanici? _aktif;

        public KonsolArayuz(VeriYoneticisi veri) => _veri = veri;

        // ═══════════════════════════ ANA DÖNGÜ ═══════════════════════════════

        public void Baslat()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            try { Console.Title = "Hastane / Klinik Randevu Sistemi"; } catch { }

            while (true)
            {
                _aktif = null;
                GirisEkrani();
                if (_aktif == null) break;      // Kullanıcı çıkışı seçti

                switch (_aktif)
                {
                    case Hasta h:            HastaMenusu(h);    break;
                    case Doktor d:           DoktorMenusu(d);   break;
                    case SaglikPersoneli sp: DoktorMenusu(sp);  break;
                    case Yonetici y:         YoneticiMenusu(y); break;
                }
            }

            Yaz("\nProgram kapatıldı. Görüşmek üzere!", ConsoleColor.Yellow);
        }

        // ═══════════════════════════ GİRİŞ EKRANI ═════════════════════════════

        private void GirisEkrani()
        {
            Baslik("HASTANE / KLİNİK RANDEVU SİSTEMİ");
            Yaz("  Lütfen bir seçenek girin:\n", ConsoleColor.White);
            Console.WriteLine("  1. Giriş Yap");
            Console.WriteLine("  2. Yeni Hasta Kaydı");
            Console.WriteLine("  0. Çıkış");
            Console.Write("\nSeçiminiz: ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1": GirisYap();   break;
                case "2": HastaKayit(); break;
                case "0":               break;
                default:
                    HataYaz("Geçersiz seçim!");
                    BekleDevam();
                    GirisEkrani();
                    break;
            }
        }

        private void GirisYap()
        {
            Baslik("GİRİŞ YAP");
            Console.WriteLine("  Örnek kimlikler: h1, h2, h3  (Hasta)");
            Console.WriteLine("                   d1, d2, d3  (Doktor)");
            Console.WriteLine("                   p1          (Personel)");
            Console.WriteLine("                   y1          (Yönetici)\n");
            Console.Write("Kimlik : ");
            var kimlik = Console.ReadLine()?.Trim() ?? "";
            Console.Write("Şifre  : ");
            var sifre  = SifreOku();

            var kullanici = _veri.GirisYap(kimlik, sifre);
            if (kullanici == null)
            {
                HataYaz("Hatalı kimlik veya şifre!");
                BekleDevam();
                return;
            }
            BasariYaz($"\nHoş geldiniz, {kullanici.AdSoyad}!");
            System.Threading.Thread.Sleep(800);
            _aktif = kullanici;
        }

        private void HastaKayit()
        {
            Baslik("YENİ HASTA KAYDI");
            Console.Write("Ad      : ");
            var ad    = Console.ReadLine()?.Trim() ?? "";
            Console.Write("Soyad   : ");
            var soyad = Console.ReadLine()?.Trim() ?? "";
            Console.Write("TC No (11 hane): ");
            var tc    = Console.ReadLine()?.Trim() ?? "";

            if (tc.Length != 11 || !tc.All(char.IsDigit))
            {
                HataYaz("Geçersiz TC Kimlik No (11 rakam olmalıdır)!");
                BekleDevam();
                return;
            }

            Console.Write("Şifre   : ");
            var sifre = SifreOku();

            var hasta = _veri.HastaKaydet(ad, soyad, tc, sifre);
            if (hasta == null)
            {
                HataYaz("Bu TC No zaten kayıtlı! Giriş yapabilirsiniz.");
                BekleDevam();
                return;
            }
            BasariYaz($"\n✓ Kayıt başarılı! Sisteme otomatik giriş yapılıyor...");
            System.Threading.Thread.Sleep(800);
            _aktif = hasta;
        }

        // ═══════════════════════════ HASTA MENÜSÜ ═════════════════════════════

        private void HastaMenusu(Hasta hasta)
        {
            while (true)
            {
                Baslik($"HASTA MENÜSÜ  —  {hasta.AdSoyad}");
                Console.WriteLine("  1. Randevu Al");
                Console.WriteLine("  2. Randevularım");
                Console.WriteLine("  3. Randevu İptal Et");
                Console.WriteLine("  4. Kurum / Şube Bilgileri");
                Console.WriteLine("  0. Çıkış / Kullanıcı Değiştir");
                Console.Write("\nSeçiminiz: ");

                switch (Console.ReadLine()?.Trim())
                {
                    case "1": RandevuAl(hasta);        break;
                    case "2": HastaRandevulari(hasta); break;
                    case "3": RandevuIptal(hasta);     break;
                    case "4": KurumBilgileri();        break;
                    case "0": return;
                    default:  HataYaz("Geçersiz seçim!"); BekleDevam(); break;
                }
            }
        }

        // ── Randevu Al ──
        private void RandevuAl(Hasta hasta)
        {
            Baslik("RANDEVU AL");

            // 1. Kurum seç
            var kurumlar = _veri.Kurumlar.ToList();
            if (!kurumlar.Any()) { HataYaz("Sistemde kayıtlı kurum yok."); BekleDevam(); return; }

            YazBaslik("Kurum seçin:");
            for (int i = 0; i < kurumlar.Count; i++)
                Console.WriteLine($"  {i + 1}. {kurumlar[i]}");

            var kurum = Sec(kurumlar, "Kurum numarası");
            if (kurum == null) return;

            // 2. Şube seç
            var subeler = kurum.Subeler.ToList();
            YazBaslik("Şube seçin:");
            for (int i = 0; i < subeler.Count; i++)
                Console.WriteLine($"  {i + 1}. {subeler[i].Ad}  [{subeler[i].Adres}]");

            var sube = Sec(subeler, "Şube numarası");
            if (sube == null) return;

            // 3. Çalışan seç
            var calisanlar = sube.Calisanlar.ToList();
            if (!calisanlar.Any())
            {
                HataYaz("Bu şubede kayıtlı çalışan yok.");
                BekleDevam();
                return;
            }

            YazBaslik("Çalışan seçin:");
            for (int i = 0; i < calisanlar.Count; i++)
            {
                var hizAd = string.Join(", ", calisanlar[i].Hizmetler.Select(h => h.Ad));
                Console.WriteLine($"  {i + 1}. {calisanlar[i]}  |  Hizmetler: {hizAd}");
            }

            var calisan = Sec(calisanlar, "Çalışan numarası");
            if (calisan == null) return;

            // 4. Hizmet seç
            var hizmetler = calisan.Hizmetler.ToList();
            if (!hizmetler.Any()) { HataYaz("Bu çalışanın tanımlı hizmeti yok."); BekleDevam(); return; }

            YazBaslik("Hizmet seçin:");
            for (int i = 0; i < hizmetler.Count; i++)
                Console.WriteLine($"  {i + 1}. {hizmetler[i]}");

            var hizmet = Sec(hizmetler, "Hizmet numarası");
            if (hizmet == null) return;

            // 5. Tarih gir
            Console.Write("\nTarih girin (GG.AA.YYYY): ");
            if (!DateTime.TryParseExact(Console.ReadLine()?.Trim(), "dd.MM.yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var gun))
            {
                HataYaz("Geçersiz tarih! Örnek: 20.11.2026");
                BekleDevam();
                return;
            }

            // 6. Boş saat seç
            var boslar = _veri.Servis!.BosSaatler(calisan, hizmet, gun);
            if (!boslar.Any())
            {
                HataYaz($"{gun:dd.MM.yyyy} tarihinde {calisan.AdSoyad} için uygun saat yok.");
                BekleDevam();
                return;
            }

            YazBaslik($"Boş saatler ({gun:dd.MM.yyyy} — {calisan.AdSoyad}):");
            for (int i = 0; i < boslar.Count; i++)
                Console.Write($"  {i + 1}. {boslar[i]:HH:mm}  ");
            Console.WriteLine();

            var baslangic = SecDeger(boslar, "Saat numarası");
            if (baslangic == default) return;

            // 7. Randevu oluştur
            try
            {
                var r = _veri.Servis.Olustur(hasta, calisan, hizmet, baslangic);
                _veri.RandevulariKaydet();
                Console.WriteLine();
                BasariYaz($"✓ Randevu oluşturuldu:\n  {r}");
            }
            catch (RandevuHatasi ex) { HataYaz(ex.Message); }
            BekleDevam();
        }

        // ── Randevularımı Göster ──
        private void HastaRandevulari(Hasta hasta)
        {
            Baslik("RANDEVULARIM");
            var liste = _veri.Servis!.HastaRandevulari(hasta)
                             .OrderBy(r => r.Baslangic).ToList();
            if (!liste.Any()) { Yaz("  Henüz randevunuz bulunmuyor.", ConsoleColor.Yellow); BekleDevam(); return; }

            foreach (var r in liste)
            {
                var renk = r.Durum switch
                {
                    RandevuDurumu.Onaylandi   => ConsoleColor.Green,
                    RandevuDurumu.Reddedildi  => ConsoleColor.Red,
                    RandevuDurumu.IptalEdildi => ConsoleColor.DarkGray,
                    _                         => ConsoleColor.White
                };
                Yaz("  " + r, renk);
            }
            Console.WriteLine($"\n  Toplam: {liste.Count} randevu.");
            BekleDevam();
        }

        // ── Randevu İptal ──
        private void RandevuIptal(Hasta hasta)
        {
            Baslik("RANDEVU İPTAL");
            var liste = _veri.Servis!.HastaRandevulari(hasta)
                             .Where(r => r.Aktif).OrderBy(r => r.Baslangic).ToList();
            if (!liste.Any()) { Yaz("  İptal edilebilir aktif randevunuz yok.", ConsoleColor.Yellow); BekleDevam(); return; }

            for (int i = 0; i < liste.Count; i++)
                Console.WriteLine($"  {i + 1}. {liste[i]}");

            var r = Sec(liste, "İptal edilecek randevu numarası");
            if (r == null) return;

            try
            {
                _veri.Servis.Iptal(r, hasta);
                _veri.RandevulariKaydet();
                BasariYaz("✓ Randevu iptal edildi.");
            }
            catch (RandevuHatasi ex) { HataYaz(ex.Message); }
            BekleDevam();
        }

        // ═══════════════════════ DOKTOR / PERSONEL MENÜSÜ ═════════════════════

        private void DoktorMenusu(Calisan calisan)
        {
            while (true)
            {
                Baslik($"{calisan.MenuBasligi()}  —  {calisan}");
                Console.WriteLine("  1. Bekleyen Randevularım");
                Console.WriteLine("  2. Tüm Randevularım");
                Console.WriteLine("  3. Randevu Onayla");
                Console.WriteLine("  4. Randevu Reddet");
                Console.WriteLine("  5. Çalışma Bilgilerim");
                Console.WriteLine("  0. Çıkış / Kullanıcı Değiştir");
                Console.Write("\nSeçiminiz: ");

                switch (Console.ReadLine()?.Trim())
                {
                    case "1": CalisanRandevuGoster(calisan, true);  break;
                    case "2": CalisanRandevuGoster(calisan, false); break;
                    case "3": CalisanOnayla(calisan);               break;
                    case "4": CalisanReddet(calisan);               break;
                    case "5": CalismaBilgileri(calisan);            break;
                    case "0": return;
                    default:  HataYaz("Geçersiz seçim!"); BekleDevam(); break;
                }
            }
        }

        private void CalisanRandevuGoster(Calisan calisan, bool sadeceBekleyen)
        {
            Baslik(sadeceBekleyen ? "BEKLEYEN RANDEVULARIM" : "TÜM RANDEVULARIM");
            var liste = _veri.Servis!.CalisanRandevulari(calisan)
                .Where(r => !sadeceBekleyen || r.Durum == RandevuDurumu.Beklemede)
                .OrderBy(r => r.Baslangic).ToList();
            if (!liste.Any()) { Yaz("  Randevu bulunamadı.", ConsoleColor.Yellow); BekleDevam(); return; }
            foreach (var r in liste) Console.WriteLine("  " + r);
            Console.WriteLine($"\n  Toplam: {liste.Count} randevu.");
            BekleDevam();
        }

        private void CalisanOnayla(Calisan calisan)
        {
            Baslik("RANDEVU ONAYLA");
            var liste = _veri.Servis!.CalisanRandevulari(calisan)
                .Where(r => r.Durum == RandevuDurumu.Beklemede).OrderBy(r => r.Baslangic).ToList();
            if (!liste.Any()) { Yaz("  Onaylanacak bekleyen randevu yok.", ConsoleColor.Yellow); BekleDevam(); return; }

            for (int i = 0; i < liste.Count; i++) Console.WriteLine($"  {i + 1}. {liste[i]}");
            var r = Sec(liste, "Onaylanacak randevu numarası");
            if (r == null) return;

            try { _veri.Servis.Onayla(r, calisan); _veri.RandevulariKaydet(); BasariYaz("✓ Randevu onaylandı."); }
            catch (RandevuHatasi ex) { HataYaz(ex.Message); }
            BekleDevam();
        }

        private void CalisanReddet(Calisan calisan)
        {
            Baslik("RANDEVU REDDET");
            var liste = _veri.Servis!.CalisanRandevulari(calisan)
                .Where(r => r.Durum == RandevuDurumu.Beklemede).OrderBy(r => r.Baslangic).ToList();
            if (!liste.Any()) { Yaz("  Reddedilecek bekleyen randevu yok.", ConsoleColor.Yellow); BekleDevam(); return; }

            for (int i = 0; i < liste.Count; i++) Console.WriteLine($"  {i + 1}. {liste[i]}");
            var r = Sec(liste, "Reddedilecek randevu numarası");
            if (r == null) return;

            try { _veri.Servis.Reddet(r, calisan); _veri.RandevulariKaydet(); BasariYaz("✓ Randevu reddedildi."); }
            catch (RandevuHatasi ex) { HataYaz(ex.Message); }
            BekleDevam();
        }

        private void CalismaBilgileri(Calisan calisan)
        {
            Baslik("ÇALIŞMA BİLGİLERİM");
            Console.WriteLine($"  Ad / Unvan  : {calisan}");
            Console.WriteLine($"  Şube        : {calisan.Sube?.ToString() ?? "Atanmamış"}");

            var hizAd = calisan.Hizmetler.Any()
                ? string.Join(", ", calisan.Hizmetler.Select(h => h.Ad))
                : "(Hizmet tanımlı değil)";
            Console.WriteLine($"  Hizmetler   : {hizAd}");

            Console.WriteLine("  Uygunluklar :");
            foreach (var u in calisan.Uygunluklar)
                Console.WriteLine($"    • {u}");
            BekleDevam();
        }

        // ═══════════════════════════ YÖNETİCİ MENÜSÜ ═════════════════════════

        private void YoneticiMenusu(Yonetici yonetici)
        {
            while (true)
            {
                Baslik($"YÖNETİCİ MENÜSÜ  —  {yonetici.AdSoyad}");
                Console.WriteLine("  1. Tüm Randevular");
                Console.WriteLine("  2. Bekleyen Randevular");
                Console.WriteLine("  3. Randevu Onayla (Yönetici)");
                Console.WriteLine("  4. Randevu Reddet (Yönetici)");
                Console.WriteLine("  5. Kurum / Şube Bilgileri");
                Console.WriteLine("  6. İstatistikler");
                Console.WriteLine("  0. Çıkış / Kullanıcı Değiştir");
                Console.Write("\nSeçiminiz: ");

                switch (Console.ReadLine()?.Trim())
                {
                    case "1": TumRandevular(false);       break;
                    case "2": TumRandevular(true);        break;
                    case "3": YoneticiOnayla(yonetici);  break;
                    case "4": YoneticiReddet(yonetici);  break;
                    case "5": KurumBilgileri();           break;
                    case "6": Istatistikler();            break;
                    case "0": return;
                    default:  HataYaz("Geçersiz seçim!"); BekleDevam(); break;
                }
            }
        }

        private void TumRandevular(bool sadeceBekleyen)
        {
            Baslik(sadeceBekleyen ? "BEKLEYEN RANDEVULAR" : "TÜM RANDEVULAR");
            var liste = _veri.Servis!.Tumu()
                .Where(r => !sadeceBekleyen || r.Durum == RandevuDurumu.Beklemede)
                .OrderBy(r => r.Baslangic).ToList();
            if (!liste.Any()) { Yaz("  Randevu bulunamadı.", ConsoleColor.Yellow); BekleDevam(); return; }
            foreach (var r in liste)
            {
                var renk = r.Durum switch
                {
                    RandevuDurumu.Onaylandi   => ConsoleColor.Green,
                    RandevuDurumu.Reddedildi  => ConsoleColor.Red,
                    RandevuDurumu.IptalEdildi => ConsoleColor.DarkGray,
                    _                         => ConsoleColor.White
                };
                Yaz("  " + r, renk);
            }
            Console.WriteLine($"\n  Toplam: {liste.Count} randevu.");
            BekleDevam();
        }

        private void YoneticiOnayla(Yonetici y)
        {
            Baslik("RANDEVU ONAYLA (Yönetici)");
            var liste = _veri.Servis!.Tumu()
                .Where(r => r.Durum == RandevuDurumu.Beklemede).OrderBy(r => r.Baslangic).ToList();
            if (!liste.Any()) { Yaz("  Onaylanacak randevu yok.", ConsoleColor.Yellow); BekleDevam(); return; }

            for (int i = 0; i < liste.Count; i++) Console.WriteLine($"  {i + 1}. {liste[i]}");
            var r = Sec(liste, "Onaylanacak randevu numarası");
            if (r == null) return;

            try { _veri.Servis.Onayla(r, y); _veri.RandevulariKaydet(); BasariYaz("✓ Randevu onaylandı."); }
            catch (RandevuHatasi ex) { HataYaz(ex.Message); }
            BekleDevam();
        }

        private void YoneticiReddet(Yonetici y)
        {
            Baslik("RANDEVU REDDET (Yönetici)");
            var liste = _veri.Servis!.Tumu()
                .Where(r => r.Durum == RandevuDurumu.Beklemede).OrderBy(r => r.Baslangic).ToList();
            if (!liste.Any()) { Yaz("  Reddedilecek randevu yok.", ConsoleColor.Yellow); BekleDevam(); return; }

            for (int i = 0; i < liste.Count; i++) Console.WriteLine($"  {i + 1}. {liste[i]}");
            var r = Sec(liste, "Reddedilecek randevu numarası");
            if (r == null) return;

            try { _veri.Servis.Reddet(r, y); _veri.RandevulariKaydet(); BasariYaz("✓ Randevu reddedildi."); }
            catch (RandevuHatasi ex) { HataYaz(ex.Message); }
            BekleDevam();
        }

        // ── Kurum Bilgileri (tüm roller için) ──
        private void KurumBilgileri()
        {
            Baslik("KURUM / ŞUBE BİLGİLERİ");
            foreach (var kurum in _veri.Kurumlar)
            {
                Yaz($"\n  {kurum}", ConsoleColor.Cyan);
                foreach (var sube in kurum.Subeler)
                {
                    Console.WriteLine($"    Şube  : {sube.Ad}");
                    Console.WriteLine($"    Adres : {sube.Adres}");
                    Console.WriteLine($"    Bölümler  : {string.Join(", ", sube.Bolumler)}");
                    Console.WriteLine($"    Hizmetler : {string.Join(", ", sube.Hizmetler.Select(h => h.Ad))}");
                    if (sube.Calisanlar.Any())
                    {
                        Console.WriteLine("    Çalışanlar:");
                        foreach (var c in sube.Calisanlar)
                        {
                            var uyg = string.Join(" / ", c.Uygunluklar.Select(u => u.ToString()));
                            Console.WriteLine($"      • {c}  |  {uyg}");
                        }
                    }
                    Console.WriteLine();
                }
            }
            BekleDevam();
        }

        // ── İstatistikler ──
        private void Istatistikler()
        {
            Baslik("SİSTEM İSTATİSTİKLERİ");
            var tum = _veri.Servis!.Tumu().ToList();

            Console.WriteLine($"  Toplam Randevu   : {tum.Count}");
            Yaz  ($"  Beklemede        : {tum.Count(r => r.Durum == RandevuDurumu.Beklemede)}",   ConsoleColor.White);
            Yaz  ($"  Onaylandı        : {tum.Count(r => r.Durum == RandevuDurumu.Onaylandi)}",   ConsoleColor.Green);
            Yaz  ($"  Reddedildi       : {tum.Count(r => r.Durum == RandevuDurumu.Reddedildi)}",  ConsoleColor.Red);
            Yaz  ($"  İptal Edildi     : {tum.Count(r => r.Durum == RandevuDurumu.IptalEdildi)}", ConsoleColor.DarkGray);

            var ciro = tum.Where(r => r.Durum == RandevuDurumu.Onaylandi).Sum(r => r.Ucret);
            Yaz($"\n  Onaylanan Ciro   : {ciro:N2} TL", ConsoleColor.Yellow);

            Console.WriteLine($"\n  Kurum Sayısı     : {_veri.Kurumlar.Count}");
            Console.WriteLine($"  Şube Sayısı      : {_veri.Kurumlar.Sum(k => k.Subeler.Count)}");
            Console.WriteLine($"  Çalışan Sayısı   : {_veri.Kurumlar.SelectMany(k => k.Subeler).SelectMany(s => s.Calisanlar).Distinct().Count()}");
            BekleDevam();
        }

        // ═══════════════════════════ YARDIMCI METOTLAR ════════════════════════

        // Listeden seçim (class tipler için)
        private static T? Sec<T>(List<T> liste, string bilgi) where T : class
        {
            Console.Write($"\n{bilgi} (0: İptal): ");
            if (int.TryParse(Console.ReadLine(), out var n) && n > 0 && n <= liste.Count)
                return liste[n - 1];
            return null;
        }

        // Listeden seçim (struct tipler için — DateTime gibi)
        private static T SecDeger<T>(List<T> liste, string bilgi) where T : struct
        {
            Console.Write($"\n{bilgi} (0: İptal): ");
            if (int.TryParse(Console.ReadLine(), out var n) && n > 0 && n <= liste.Count)
                return liste[n - 1];
            return default;
        }

        private static void Baslik(string baslik)
        {
            try { Console.Clear(); } catch { }
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n" + new string('=', 65));
            Console.WriteLine($"  {baslik}");
            Console.WriteLine(new string('=', 65) + "\n");
            Console.ResetColor();
        }

        private static void YazBaslik(string baslik)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"\n  {baslik}");
            Console.ResetColor();
        }

        private static void Yaz(string mesaj, ConsoleColor renk)
        {
            Console.ForegroundColor = renk;
            Console.WriteLine(mesaj);
            Console.ResetColor();
        }

        private static void BasariYaz(string mesaj) => Yaz(mesaj, ConsoleColor.Green);
        private static void HataYaz(string mesaj)   => Yaz("\n  ⚠ " + mesaj, ConsoleColor.Red);

        private static void BekleDevam()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("\n  [ Devam etmek için Enter'a basın... ]");
            Console.ResetColor();
            Console.ReadLine();
        }

        private static string SifreOku()
        {
            if (Console.IsInputRedirected)
            {
                return Console.ReadLine()?.Trim() ?? "";
            }
            try
            {
                var sb = new System.Text.StringBuilder();
                ConsoleKeyInfo k;
                while ((k = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
                {
                    if (k.Key == ConsoleKey.Backspace && sb.Length > 0)
                    {
                        sb.Remove(sb.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                    else if (k.Key != ConsoleKey.Backspace)
                    {
                        sb.Append(k.KeyChar);
                        Console.Write('*');
                    }
                }
                Console.WriteLine();
                return sb.ToString();
            }
            catch
            {
                return Console.ReadLine()?.Trim() ?? "";
            }
        }
    }
}

