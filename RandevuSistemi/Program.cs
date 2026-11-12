using RandevuSistemi.Models;
using RandevuSistemi.Services;

namespace RandevuSistemi
{
    internal class Program
    {
        static Veri veri = null!;
        static RandevuServisi servis = null!;

        static void Main(string[] args)
        {
            veri = Veri.Olustur();
            servis = new RandevuServisi(new BellekRandevuDeposu(), new KonsolBildirim());

            while (true)
            {
                Console.WriteLine("\n=== HASTANE / KLINIK RANDEVU SISTEMI ===");
                Console.WriteLine("Giris yapacak kullaniciyi secin:");
                for (int i = 0; i < veri.Kullanicilar.Count; i++)
                    Console.WriteLine($" {i + 1}. {veri.Kullanicilar[i]}");
                Console.WriteLine(" 0. Cikis");
                int sec = Oku("Seciminiz: ");
                if (sec == 0) return;
                if (sec < 1 || sec > veri.Kullanicilar.Count) continue;

                Kullanici k = veri.Kullanicilar[sec - 1];
                // Cok bicimlilik: rolune gore dogru menu
                switch (k)
                {
                    case Hasta h: HastaMenusu(h); break;
                    case Calisan c: CalisanMenusu(c); break;
                    case Yonetici y: YoneticiMenusu(y); break;
                }
            }
        }

        // ---------------- HASTA ----------------
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
                        var liste = servis.HastaRandevulari(h).Where(r => r.Aktif).ToList();
                        var r = Sec(liste, "Iptal edilecek randevu");
                        if (r != null) Dene(() => { servis.Iptal(r, h); Console.WriteLine("Iptal edildi."); });
                        break;
                    case 0: return;
                }
            }
        }

        static void RandevuAl(Hasta h)
        {
            var subeler = veri.Kurumlar.SelectMany(k => k.Subeler).ToList();
            var sube = Sec(subeler, "Sube");
            if (sube == null) return;
            var calisan = Sec(sube.Calisanlar.ToList(), "Calisan");
            if (calisan == null) return;
            var hizmet = Sec(calisan.Hizmetler.ToList(), "Hizmet");
            if (hizmet == null) return;

            Console.Write("Tarih (gg.aa.yyyy): ");
            if (!DateTime.TryParseExact(Console.ReadLine(), "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var gun)) { Console.WriteLine("Gecersiz tarih."); return; }

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
                Console.WriteLine("1. Randevularim\n2. Randevu onayla\n3. Randevu reddet\n0. Cikis");
                int secim = Oku("Secim: ");
                switch (secim)
                {
                    case 1: Yazdir(servis.CalisanRandevulari(c)); break;
                    case 2:
                    case 3:
                        bool onay = secim == 2;
                        var bek = servis.CalisanRandevulari(c).Where(r => r.Durum == RandevuDurumu.Beklemede).ToList();
                        var r = Sec(bek, "Randevu");
                        if (r == null) break;
                        Dene(() => { if (onay) servis.Onayla(r, c); else servis.Reddet(r, c); });
                        break;
                    case 0: return;
                }
            }
        }

        // ---------------- YONETICI ----------------
        static void YoneticiMenusu(Yonetici y)
        {
            while (true)
            {
                Console.WriteLine($"\n--- {y.MenuBasligi()} ---");
                Console.WriteLine("1. Kurum/Sube listesi\n2. Tum randevular\n3. Bekleyen randevu onayla\n4. Yeni hizmet ekle\n5. Yeni doktor ekle\n0. Cikis");
                switch (Oku("Secim: "))
                {
                    case 1:
                        foreach (var k in veri.Kurumlar)
                        {
                            Console.WriteLine(k);
                            foreach (var s in k.Subeler)
                            {
                                Console.WriteLine($"  - {s.Ad} ({s.Adres}) Saatler: {string.Join(", ", s.CalismaSaatleri.Take(1))}...");
                                Console.WriteLine($"    Bolumler : {string.Join(", ", s.Bolumler)}");
                                Console.WriteLine($"    Hizmetler: {string.Join("; ", s.Hizmetler)}");
                                Console.WriteLine($"    Calisanlar: {string.Join("; ", s.Calisanlar)}");
                            }
                        }
                        break;
                    case 2: Yazdir(servis.Tumu()); break;
                    case 3:
                        var r = Sec(servis.Tumu().Where(x => x.Durum == RandevuDurumu.Beklemede).ToList(), "Randevu");
                        if (r != null) Dene(() => servis.Onayla(r, y));
                        break;
                    case 4:
                        var sube = Sec(veri.Kurumlar.SelectMany(k => k.Subeler).ToList(), "Sube");
                        if (sube == null) break;
                        Console.Write("Hizmet adi: "); var ad = Console.ReadLine() ?? "";
                        int sure = Oku("Sure (dk): "); int ucret = Oku("Ucret (TL): ");
                        Dene(() => Console.WriteLine("Eklendi: " + sube.HizmetEkle(ad, sure, ucret)));
                        break;
                    case 5:
                        var sb = Sec(veri.Kurumlar.SelectMany(k => k.Subeler).ToList(), "Sube");
                        if (sb == null) break;
                        Console.Write("Ad: "); var dad = Console.ReadLine() ?? "";
                        Console.Write("Soyad: "); var dsoyad = Console.ReadLine() ?? "";
                        Console.Write("Uzmanlik: "); var uz = Console.ReadLine() ?? "";
                        var dr = new Doktor(dad, dsoyad, uz);
                        var hz = Sec(sb.Hizmetler.ToList(), "Doktorun sunacagi hizmet");
                        if (hz != null) dr.HizmetEkle(hz);
                        foreach (var g in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
                            dr.UygunlukEkle(new CalismaSaati(g, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0)));
                        sb.CalisanEkle(dr); veri.Kullanicilar.Add(dr);
                        Console.WriteLine("Doktor eklendi (Hafta ici 09:00-12:00 uygun).");
                        break;
                    case 0: return;
                }
            }
        }

        // ---------------- Yardimcilar ----------------
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
    }
}
