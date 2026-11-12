using RandevuSistemi.Models;

namespace RandevuSistemi
{
    // Ornek baslangic verisi: 1 hastane (2 sube) + 1 klinik (tek sube)
    public class Veri
    {
        public List<Kurum> Kurumlar { get; } = new();
        public List<Kullanici> Kullanicilar { get; } = new();

        public static Veri Olustur()
        {
            var v = new Veri();
            var hafta = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

            // --- Hastane, 2 sube ---
            var hastane = new Hastane("Sakarya Sehir Hastanesi");
            var merkez = hastane.SubeEkle("Merkez Sube", "Adapazari");
            var serdivan = hastane.SubeEkle("Serdivan Sube", "Serdivan");
            foreach (var g in hafta)
            {
                merkez.CalismaSaatiEkle(new CalismaSaati(g, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)));
                serdivan.CalismaSaatiEkle(new CalismaSaati(g, new TimeSpan(9, 0, 0), new TimeSpan(16, 0, 0)));
            }
            merkez.BolumEkle("Dahiliye"); merkez.BolumEkle("Radyoloji"); merkez.BolumEkle("Laboratuvar");
            var muayene = merkez.HizmetEkle("Genel Muayene", 20, 500m);
            var mr = merkez.HizmetEkle("MR Cekimi", 40, 1800m);
            var kan = merkez.HizmetEkle("Kan Tahlili", 10, 250m);
            var kontrol = serdivan.HizmetEkle("Kontrol Muayenesi", 15, 350m);

            var d1 = new Doktor("Ayse", "Yilmaz", "Dahiliye");
            d1.HizmetEkle(muayene);
            foreach (var g in hafta) d1.UygunlukEkle(new CalismaSaati(g, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0)));
            merkez.CalisanEkle(d1);

            var d2 = new Doktor("Mehmet", "Kara", "Radyoloji");
            d2.HizmetEkle(mr);
            foreach (var g in hafta) d2.UygunlukEkle(new CalismaSaati(g, new TimeSpan(13, 0, 0), new TimeSpan(16, 0, 0)));
            merkez.CalisanEkle(d2);

            var p1 = new SaglikPersoneli("Zeynep", "Demir", "Hemsire");
            p1.HizmetEkle(kan);
            foreach (var g in hafta) p1.UygunlukEkle(new CalismaSaati(g, new TimeSpan(8, 0, 0), new TimeSpan(11, 0, 0)));
            merkez.CalisanEkle(p1);

            var d3 = new Doktor("Can", "Ozturk", "Dahiliye");
            d3.HizmetEkle(kontrol);
            foreach (var g in hafta) d3.UygunlukEkle(new CalismaSaati(g, new TimeSpan(10, 0, 0), new TimeSpan(15, 0, 0)));
            serdivan.CalisanEkle(d3);

            // --- Klinik, tek sube ---
            var klinik = new Klinik("Mavi Dis Klinigi");
            var ks = klinik.SubeEkle("Ana Sube", "Adapazari");
            foreach (var g in hafta.Append(DayOfWeek.Saturday))
                ks.CalismaSaatiEkle(new CalismaSaati(g, new TimeSpan(10, 0, 0), new TimeSpan(18, 0, 0)));
            ks.BolumEkle("Dis Hekimligi");
            var temizlik = ks.HizmetEkle("Dis Temizligi", 30, 700m);
            var d4 = new Doktor("Elif", "Sahin", "Dis Hekimi");
            d4.HizmetEkle(temizlik);
            foreach (var g in hafta) d4.UygunlukEkle(new CalismaSaati(g, new TimeSpan(10, 0, 0), new TimeSpan(17, 0, 0)));
            ks.CalisanEkle(d4);

            v.Kurumlar.Add(hastane); v.Kurumlar.Add(klinik);
            v.Kullanicilar.AddRange(new Kullanici[] {
                new Yonetici("Admin", "Yonetici"),
                d1, d2, p1, d3, d4,
                new Hasta("Ali", "Veli", "11111111111"),
                new Hasta("Fatma", "Gul", "22222222222"),
            });
            return v;
        }
    }
}
