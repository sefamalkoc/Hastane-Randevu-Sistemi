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
            _depo.Kaydet();
            _bildirim.Gonder(calisan, $"Yeni randevu talebi: {randevu}");
            return randevu;
        }

        // Onay mekanizmasi: sadece yetkili roller onaylayabilir
        public void Onayla(Randevu r, Kullanici onaylayan)
        {
            YetkiKontrol(r, onaylayan);
            r.Onayla();
            _depo.Kaydet();
            _bildirim.Gonder(r.Hasta, $"Randevunuz onaylandi: {r}");
        }

        public void Reddet(Randevu r, Kullanici onaylayan)
        {
            YetkiKontrol(r, onaylayan);
            r.Reddet();
            _depo.Kaydet();
            _bildirim.Gonder(r.Hasta, $"Randevunuz reddedildi: {r}");
        }

        public void Iptal(Randevu r, Kullanici islemYapan)
        {
            if (islemYapan != r.Hasta) YonetimYetkisi(r, islemYapan);
            if (!r.Aktif) throw new RandevuHatasi("Randevu zaten aktif degil.");
            r.Iptal();
            _depo.Kaydet();
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

        private static void YonetimYetkisi(Randevu r, Kullanici k)
        {
            if (!k.RandevuOnaylayabilir)
                throw new RandevuHatasi("Bu islem icin yetkiniz yok.");
            if (k is Yonetici y && r.Calisan.Sube != null && !y.YonetebilirMi(r.Calisan.Sube.Kurum))
                throw new RandevuHatasi("Bu kurumun randevularini yonetme yetkiniz yok.");
            // Doktor/personel sadece kendi randevusunu yonetir
            if (k is Calisan c && c != r.Calisan)
                throw new RandevuHatasi("Yalnizca kendi randevularinizi yonetebilirsiniz.");
        }

        private static void YetkiKontrol(Randevu r, Kullanici k)
        {
            YonetimYetkisi(r, k);
            if (r.Durum != RandevuDurumu.Beklemede)
                throw new RandevuHatasi("Yalnizca 'Beklemede' durumundaki randevular islenebilir.");
        }
    }
}
