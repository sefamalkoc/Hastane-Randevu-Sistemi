using RandevuSistemi.Models;

namespace RandevuSistemi.Services
{
    public class RandevuServisi
    {
        private readonly IRandevuDeposu _depo;
        private readonly IBildirimServisi _bildirim;

        // Bagimliliklar interface uzerinden enjekte edilir
        public RandevuServisi(IRandevuDeposu depo, IBildirimServisi bildirim)
        {
            _depo = depo; _bildirim = bildirim;
        }

        public IEnumerable<Randevu> Tumu() => _depo.Tumu();

        public Randevu Olustur(Hasta hasta, Calisan calisan, Hizmet hizmet, DateTime baslangic)
        {
            if (calisan.Sube == null) throw new RandevuHatasi("Calisan bir subeye atanmamis.");
            if (!calisan.HizmetSunuyorMu(hizmet))
                throw new RandevuHatasi($"{calisan} '{hizmet.Ad}' hizmetini sunmuyor.");
            if (baslangic <= DateTime.Now)
                throw new RandevuHatasi("Gecmis bir zamana randevu alinamaz.");

            var bitis = baslangic.AddMinutes(hizmet.SureDakika);

            if (!calisan.Sube.AcikMi(baslangic, bitis))
                throw new RandevuHatasi($"{calisan.Sube} bu saatlerde kapali.");
            if (!calisan.UygunMu(baslangic, bitis))
                throw new RandevuHatasi($"{calisan} bu zaman diliminde musait degil.");

            // Cakisma kontrolu: hem calisan hem hasta icin
            if (_depo.Tumu().Any(r => r.Aktif && r.Calisan == calisan && r.Cakisir(baslangic, bitis)))
                throw new RandevuHatasi("Secilen saatte calisanin baska bir randevusu var (cakisma).");
            if (_depo.Tumu().Any(r => r.Aktif && r.Hasta == hasta && r.Cakisir(baslangic, bitis)))
                throw new RandevuHatasi("Bu saatte sizin baska bir randevunuz var (cakisma).");

            var randevu = new Randevu(hasta, calisan, hizmet, baslangic);
            _depo.Ekle(randevu);
            _bildirim.Gonder(calisan, $"Yeni randevu talebi: {randevu}");
            return randevu;
        }

        public IEnumerable<Randevu> HastaRandevulari(Hasta h) => _depo.Tumu().Where(r => r.Hasta == h);
        public IEnumerable<Randevu> CalisanRandevulari(Calisan c) => _depo.Tumu().Where(r => r.Calisan == c);

        // Bos slotlari hesaplar (hizmet suresi adiminda)
        public List<DateTime> BosSaatler(Calisan c, Hizmet h, DateTime gun)
        {
            var sonuc = new List<DateTime>();
            if (c.Sube == null || !c.HizmetSunuyorMu(h)) return sonuc;
            foreach (var u in c.Uygunluklar.Where(u => u.Gun == gun.DayOfWeek))
            {
                for (var t = gun.Date + u.Baslangic; t.AddMinutes(h.SureDakika) <= gun.Date + u.Bitis; t = t.AddMinutes(h.SureDakika))
                {
                    var bit = t.AddMinutes(h.SureDakika);
                    if (t <= DateTime.Now) continue;
                    if (!c.Sube.AcikMi(t, bit)) continue;
                    if (_depo.Tumu().Any(r => r.Aktif && r.Calisan == c && r.Cakisir(t, bit))) continue;
                    sonuc.Add(t);
                }
            }
            return sonuc;
        }
    }
}
