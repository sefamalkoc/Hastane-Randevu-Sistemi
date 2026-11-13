using System.Globalization;
using RandevuSistemi.Models;
using RandevuSistemi.Services;

namespace RandevuSistemi
{
    internal class Program
    {
        static Veri veri = null!;
        static RandevuServisi servis = null!;

        static readonly DayOfWeek[] HaftaIci =
            { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

        static readonly string[] GunAdlari = { "Pazartesi", "Sali", "Carsamba", "Persembe", "Cuma", "Cumartesi", "Pazar" };
        static readonly DayOfWeek[] Gunler =
            { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

        static void Main(string[] args)
        {
            veri = Veri.Olustur();
            bool demo = args.Contains("--demo");
            // Demo bellekte calisir; normal calismada randevular dosyaya kaydedilir
            IRandevuDeposu depo = demo ? new BellekRandevuDeposu() : new DosyaRandevuDeposu("randevular.csv");
            servis = new RandevuServisi(depo, new KonsolBildirim());

            if (demo) { Demo(); return; }

            while (true)
            {
                Console.WriteLine("\n=== HASTANE / KLINIK RANDEVU SISTEMI ===");
                Console.WriteLine("Giris yapacak kullaniciyi secin:");
                for (int i = 0; i < veri.Kullanicilar.Count; i++)
                    Console.WriteLine($" {i + 1}. {veri.Kullanicilar[i]}");
                Console.WriteLine(" K. Yeni hasta kaydi\n 0. Cikis");
                Console.Write("Seciminiz: ");
                string giris = (Console.ReadLine() ?? "0").Trim();
                if (giris == "0") return;
                if (giris.Equals("K", StringComparison.OrdinalIgnoreCase)) { HastaKaydet(); continue; }
                if (!int.TryParse(giris, out int sec) || sec < 1 || sec > veri.Kullanicilar.Count) continue;

                // Cok bicimlilik: rolune gore dogru menu
                switch (veri.Kullanicilar[sec - 1])
                {
                    case Hasta h: HastaMenusu(h); break;
                    case Calisan c: CalisanMenusu(c); break;
                    case Yonetici y: YoneticiMenusu(y); break;
                }
            }
        }

        // ---------------- HASTA ----------------
        static void HastaKaydet()
        {
            Console.Write("Ad: "); var ad = (Console.ReadLine() ?? "").Trim();
            Console.Write("Soyad: "); var soyad = (Console.ReadLine() ?? "").Trim();
            Console.Write("TC No (11 hane): "); var tc = (Console.ReadLine() ?? "").Trim();
            if (ad == "" || soyad == "") { Console.WriteLine("HATA: Ad ve soyad bos olamaz."); return; }
            if (tc.Length != 11 || !tc.All(char.IsDigit)) { Console.WriteLine("HATA: TC No 11 haneli rakam olmali."); return; }
            if (veri.Kullanicilar.OfType<Hasta>().Any(x => x.TcNo == tc)) { Console.WriteLine("HATA: Bu TC No ile kayitli hasta var."); return; }
            var h = new Hasta(ad, soyad, tc);
            veri.Kullanicilar.Add(h);
            Console.WriteLine("Kayit olusturuldu: " + h);
        }

        static void HastaMenusu(Hasta h)
        {
            while (true)
            {
                Console.WriteLine($"\n--- {h.MenuBasligi()} ({h.AdSoyad}) ---");
                Console.WriteLine("1. Randevu al\n2. Randevularim\n3. Randevu iptal\n0. Cikis");
                switch (Oku("Secim: "))
                {
                    case 1: RandevuAl(h); break;
                    case 2: Yazdir(servis.HastaRandevulari(h)); break;
                    case 3:
                        var r = Sec(servis.HastaRandevulari(h).Where(x => x.Aktif).ToList(), "Iptal edilecek randevu");
                        if (r != null) Dene(() => { servis.Iptal(r, h); Console.WriteLine("Iptal edildi."); });
                        break;
                    case 0: return;
                }
            }
        }

        static void RandevuAl(Hasta h)
        {
            var sube = Sec(veri.Kurumlar.SelectMany(k => k.Subeler).ToList(), "Sube");
            if (sube == null) return;
            var calisan = Sec(sube.Calisanlar.ToList(), "Calisan");
            if (calisan == null) return;
            var hizmet = Sec(calisan.Hizmetler.ToList(), "Hizmet");
            if (hizmet == null) return;

            Console.Write("Tarih (gg.aa.yyyy): ");
            if (!DateTime.TryParseExact(Console.ReadLine(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var gun))
            { Console.WriteLine("Gecersiz tarih."); return; }

            var bos = servis.BosSaatler(calisan, hizmet, gun);
            if (bos.Count == 0) { Console.WriteLine("Bu gun icin bos saat yok."); return; }
            var saat = Sec(bos, "Saat", t => t.ToString("HH:mm"));
            if (saat == default) return;

            Dene(() =>
            {
                var r = servis.Olustur(h, calisan, hizmet, saat);
                Console.WriteLine("Randevu talebi olusturuldu (onay bekliyor):\n " + r);
            });
        }

        // ---------------- CALISAN ----------------
        static void CalisanMenusu(Calisan c)
        {
            while (true)
            {
                Console.WriteLine($"\n--- {c.MenuBasligi()} ({c}) ---");
                Console.WriteLine("1. Randevularim\n2. Randevu onayla\n3. Randevu reddet\n4. Uygunluk saatlerim\n0. Cikis");
                int secim = Oku("Secim: ");
                switch (secim)
                {
                    case 1: Yazdir(servis.CalisanRandevulari(c)); break;
                    case 2:
                    case 3:
                        var bek = servis.CalisanRandevulari(c).Where(x => x.Durum == RandevuDurumu.Beklemede).ToList();
                        var r = Sec(bek, "Randevu");
                        if (r == null) break;
                        Dene(() => { if (secim == 2) servis.Onayla(r, c); else servis.Reddet(r, c); });
                        break;
                    case 4:
                        foreach (var u in c.Uygunluklar) Console.WriteLine(" " + u);
                        break;
                    case 0: return;
                }
            }
        }

        // ---------------- YONETICI ----------------
        static IEnumerable<Kurum> YonetilenKurumlar(Yonetici y) => veri.Kurumlar.Where(y.YonetebilirMi);

        static void YoneticiMenusu(Yonetici y)
        {
            while (true)
            {
                Console.WriteLine($"\n--- {y.MenuBasligi()} ---");
                Console.WriteLine("1. Kurum/sube/hizmet listesi\n2. Randevular\n3. Bekleyen randevu onayla/reddet");
                Console.WriteLine("4. Yeni hastane/klinik ekle" + (y.SistemYoneticisi ? "" : "  (yetkiniz yok)"));
                Console.WriteLine("5. Yeni sube ekle\n6. Subeye bolum ekle\n7. Sube calisma saati ekle\n8. Subeye hizmet ekle (sure/ucret)");
                Console.WriteLine("9. Doktor/saglik personeli ekle\n10. Calisana hizmet ata\n11. Calisan uygunluk saati ekle\n0. Cikis");
                switch (Oku("Secim: "))
                {
                    case 1: Listele(y); break;
                    case 2: Yazdir(servis.Tumu().Where(r => r.Calisan.Sube != null && y.YonetebilirMi(r.Calisan.Sube.Kurum))); break;
                    case 3: BekleyenIsle(y); break;
                    case 4: KurumEkle(y); break;
                    case 5: SubeEkle(y); break;
                    case 6: BolumEkle(y); break;
                    case 7: SubeSaatiEkle(y); break;
                    case 8: HizmetEkle(y); break;
                    case 9: CalisanEkle(y); break;
                    case 10: HizmetAta(y); break;
                    case 11: UygunlukEkle(y); break;
                    case 0: return;
                }
            }
        }

        static void Listele(Yonetici y)
        {
            foreach (var k in YonetilenKurumlar(y))
            {
                Console.WriteLine(k);
                foreach (var s in k.Subeler)
                {
                    Console.WriteLine($"  - {s.Ad} ({s.Adres})");
                    Console.WriteLine($"    Calisma saatleri: {string.Join(", ", s.CalismaSaatleri)}");
                    Console.WriteLine($"    Bolumler  : {string.Join(", ", s.Bolumler)}");
                    Console.WriteLine($"    Hizmetler : {string.Join("; ", s.Hizmetler)}");
                    Console.WriteLine($"    Calisanlar: {string.Join("; ", s.Calisanlar)}");
                }
            }
        }

        static void BekleyenIsle(Yonetici y)
        {
            var bek = servis.Tumu().Where(x => x.Durum == RandevuDurumu.Beklemede &&
                x.Calisan.Sube != null && y.YonetebilirMi(x.Calisan.Sube.Kurum)).ToList();
            var r = Sec(bek, "Randevu");
            if (r == null) return;
            Console.Write("1=Onayla  2=Reddet: ");
            int s = Oku("");
            Dene(() => { if (s == 1) servis.Onayla(r, y); else if (s == 2) servis.Reddet(r, y); });
        }

        static void KurumEkle(Yonetici y)
        {
            if (!y.SistemYoneticisi) { Console.WriteLine("HATA: Yeni kurum yalnizca sistem yoneticisi tanimlayabilir."); return; }
            Console.WriteLine("1=Hastane  2=Klinik");
            int tur = Oku("Tur: ");
            Console.Write("Kurum adi: "); var ad = (Console.ReadLine() ?? "").Trim();
            if (ad == "" || (tur != 1 && tur != 2)) { Console.WriteLine("HATA: Gecersiz giris."); return; }
            Kurum k = tur == 1 ? new Hastane(ad) : new Klinik(ad);
            veri.Kurumlar.Add(k);
            Console.WriteLine("Eklendi: " + k);
        }

        static Sube? SubeSec(Yonetici y) =>
            Sec(YonetilenKurumlar(y).SelectMany(k => k.Subeler).ToList(), "Sube");

        static void SubeEkle(Yonetici y)
        {
            var kurum = Sec(YonetilenKurumlar(y).ToList(), "Kurum");
            if (kurum == null) return;
            Console.Write("Sube adi: "); var ad = (Console.ReadLine() ?? "").Trim();
            Console.Write("Adres: "); var adres = (Console.ReadLine() ?? "").Trim();
            if (ad == "") { Console.WriteLine("HATA: Sube adi bos olamaz."); return; }
            Console.WriteLine("Eklendi: " + kurum.SubeEkle(ad, adres));
        }

        static void BolumEkle(Yonetici y)
        {
            var s = SubeSec(y); if (s == null) return;
            Console.Write("Bolum adi: "); var ad = (Console.ReadLine() ?? "").Trim();
            if (ad == "") { Console.WriteLine("HATA: Bolum adi bos olamaz."); return; }
            Console.WriteLine("Eklendi: " + s.BolumEkle(ad));
        }

        static void SubeSaatiEkle(Yonetici y)
        {
            var s = SubeSec(y); if (s == null) return;
            var saat = SaatAraligiOku(); if (saat == null) return;
            Console.WriteLine("Gunler: 1=Hafta ici (Pzt-Cuma)  2=Tek gun");
            foreach (var g in GunSec(Oku("Secim: "))) s.CalismaSaatiEkle(new CalismaSaati(g, saat.Value.bas, saat.Value.bit));
            Console.WriteLine("Calisma saatleri guncellendi.");
        }

        static void HizmetEkle(Yonetici y)
        {
            var s = SubeSec(y); if (s == null) return;
            Console.Write("Hizmet adi: "); var ad = (Console.ReadLine() ?? "").Trim();
            int sure = Oku("Sure (dk): ");
            Console.Write("Ucret (TL): ");
            if (!decimal.TryParse(Console.ReadLine(), NumberStyles.Number, CultureInfo.InvariantCulture, out var ucret)) { Console.WriteLine("HATA: Gecersiz ucret."); return; }
            Dene(() => Console.WriteLine("Eklendi: " + s.HizmetEkle(ad, sure, ucret)));
        }

        static void CalisanEkle(Yonetici y)
        {
            var s = SubeSec(y); if (s == null) return;
            Console.WriteLine("1=Doktor  2=Saglik personeli");
            int tur = Oku("Tur: ");
            Console.Write("Ad: "); var ad = (Console.ReadLine() ?? "").Trim();
            Console.Write("Soyad: "); var soyad = (Console.ReadLine() ?? "").Trim();
            Console.Write(tur == 1 ? "Uzmanlik alani: " : "Unvan: "); var uz = (Console.ReadLine() ?? "").Trim();
            if (ad == "" || soyad == "" || uz == "" || (tur != 1 && tur != 2)) { Console.WriteLine("HATA: Gecersiz giris."); return; }
            Calisan c = tur == 1 ? new Doktor(ad, soyad, uz) : new SaglikPersoneli(ad, soyad, uz);
            s.CalisanEkle(c);
            veri.Kullanicilar.Add(c);
            Console.WriteLine("Eklendi: " + c + ". Simdi hizmet ve uygunluk saati tanimlayin (menu 10 ve 11).");
        }

        static Calisan? CalisanSec(Yonetici y)
        {
            var s = SubeSec(y); if (s == null) return null;
            return Sec(s.Calisanlar.ToList(), "Calisan");
        }

        static void HizmetAta(Yonetici y)
        {
            var c = CalisanSec(y); if (c == null) return;
            var h = Sec(c.Sube!.Hizmetler.ToList(), "Atanacak hizmet"); if (h == null) return;
            c.HizmetEkle(h);
            Console.WriteLine($"{c} artik '{h.Ad}' hizmetini sunuyor.");
        }

        static void UygunlukEkle(Yonetici y)
        {
            var c = CalisanSec(y); if (c == null) return;
            var saat = SaatAraligiOku(); if (saat == null) return;
            Console.WriteLine("Gunler: 1=Hafta ici (Pzt-Cuma)  2=Tek gun");
            foreach (var g in GunSec(Oku("Secim: "))) c.UygunlukEkle(new CalismaSaati(g, saat.Value.bas, saat.Value.bit));
            Console.WriteLine("Uygunluk saatleri guncellendi.");
        }

        // ---------------- Yardimcilar ----------------
        static (TimeSpan bas, TimeSpan bit)? SaatAraligiOku()
        {
            Console.Write("Baslangic saati (SS:dd): "); var a = Console.ReadLine();
            Console.Write("Bitis saati (SS:dd): "); var b = Console.ReadLine();
            if (TimeSpan.TryParseExact(a, "hh\\:mm", CultureInfo.InvariantCulture, out var bas) &&
                TimeSpan.TryParseExact(b, "hh\\:mm", CultureInfo.InvariantCulture, out var bit) && bit > bas)
                return (bas, bit);
            Console.WriteLine("HATA: Saatler SS:dd biciminde ve bitis > baslangic olmali.");
            return null;
        }

        static IEnumerable<DayOfWeek> GunSec(int secim)
        {
            if (secim == 1) return HaftaIci;
            if (secim == 2)
            {
                for (int i = 0; i < Gunler.Length; i++) Console.WriteLine($" {i + 1}. {GunAdlari[i]}");
                int g = Oku("Gun: ");
                if (g >= 1 && g <= 7) return new[] { Gunler[g - 1] };
            }
            Console.WriteLine("Gecersiz secim, hafta ici kullanildi.");
            return HaftaIci;
        }

        static int Oku(string mesaj)
        {
            Console.Write(mesaj);
            return int.TryParse(Console.ReadLine(), out var n) ? n : -1;
        }

        static T? Sec<T>(List<T> liste, string baslik, Func<T, string>? goster = null)
        {
            if (liste.Count == 0) { Console.WriteLine($"{baslik} icin kayit yok."); return default; }
            Console.WriteLine($"-- {baslik} sec --");
            for (int i = 0; i < liste.Count; i++)
                Console.WriteLine($" {i + 1}. {(goster != null ? goster(liste[i]) : liste[i]!.ToString())}");
            int n = Oku("Secim (0=geri): ");
            return n >= 1 && n <= liste.Count ? liste[n - 1] : default;
        }

        static void Yazdir(IEnumerable<Randevu> liste)
        {
            var l = liste.ToList();
            if (l.Count == 0) Console.WriteLine("Kayit yok.");
            foreach (var r in l) Console.WriteLine(" " + r);
        }

        static void Dene(Action a)
        {
            try { a(); }
            catch (RandevuHatasi e) { Console.WriteLine("HATA: " + e.Message); }
            catch (ArgumentException e) { Console.WriteLine("HATA: " + e.Message); }
        }

        // Otomatik senaryo: tum kurallari gosterir (dotnet run -- --demo)
        static void Demo()
        {
            var hasta1 = veri.Kullanicilar.OfType<Hasta>().First();
            var hasta2 = veri.Kullanicilar.OfType<Hasta>().Last();
            var doktor = veri.Kurumlar[0].Subeler[0].Calisanlar.OfType<Doktor>().First();
            var hizmet = doktor.Hizmetler[0];
            var klinikYon = veri.Kullanicilar.OfType<Yonetici>().First(y => y.Kurum is Klinik);

            var gun = DateTime.Today.AddDays(1);
            while (gun.DayOfWeek == DayOfWeek.Saturday || gun.DayOfWeek == DayOfWeek.Sunday) gun = gun.AddDays(1);

            Console.WriteLine("1) Basarili randevu:");
            var r1 = servis.Olustur(hasta1, doktor, hizmet, gun.AddHours(9));
            Console.WriteLine(" " + r1);

            Console.WriteLine("2) Ayni saate ikinci hasta (cakisma beklenir):");
            Dene(() => servis.Olustur(hasta2, doktor, hizmet, gun.AddHours(9).AddMinutes(10)));

            Console.WriteLine("3) Doktorun uygun olmadigi saat (15:00):");
            Dene(() => servis.Olustur(hasta2, doktor, hizmet, gun.AddHours(15)));

            Console.WriteLine("4) Doktorun sunmadigi hizmet:");
            Dene(() => servis.Olustur(hasta2, doktor, veri.Kurumlar[0].Subeler[0].Hizmetler[1], gun.AddHours(10)));

            Console.WriteLine("5) Hasta kendi randevusunu onaylamaya calisir (yetki yok):");
            Dene(() => servis.Onayla(r1, hasta1));

            Console.WriteLine("6) Baska kurumun yoneticisi onaylamaya calisir (yetki yok):");
            Dene(() => servis.Onayla(r1, klinikYon));

            Console.WriteLine("7) Doktor onaylar:");
            Dene(() => servis.Onayla(r1, doktor));
            Console.WriteLine(" " + r1);

            Console.WriteLine("8) Iptal sonrasi ayni saat tekrar alinabilir:");
            servis.Iptal(r1, hasta1);
            Dene(() => Console.WriteLine(" " + servis.Olustur(hasta2, doktor, hizmet, gun.AddHours(9))));

            Console.WriteLine("9) Bos saatler (ertesi is gunu, ilk 5):");
            Console.WriteLine(" " + string.Join(", ", servis.BosSaatler(doktor, hizmet, gun).Take(5).Select(t => t.ToString("HH:mm"))));
        }
    }
}
