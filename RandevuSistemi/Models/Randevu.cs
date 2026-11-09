namespace RandevuSistemi.Models
{
    public class Randevu
    {
        private static int _sayac = 0;

        public int Id { get; }
        public Hasta Hasta { get; }
        public Calisan Calisan { get; }
        public Hizmet Hizmet { get; }
        public DateTime Baslangic { get; }
        public DateTime Bitis => Baslangic.AddMinutes(Hizmet.SureDakika);
        public decimal Ucret => Hizmet.Ucret;
        public RandevuDurumu Durum { get; private set; } = RandevuDurumu.Beklemede;

        internal Randevu(Hasta hasta, Calisan calisan, Hizmet hizmet, DateTime baslangic)
        {
            Id = ++_sayac;
            Hasta = hasta; Calisan = calisan; Hizmet = hizmet; Baslangic = baslangic;
        }

        // Aktif = takvimde yer tutan randevu (cakisma kontrolunde sayilir)
        public bool Aktif => Durum == RandevuDurumu.Beklemede || Durum == RandevuDurumu.Onaylandi;

        public bool Cakisir(DateTime bas, DateTime bit) => Baslangic < bit && bas < Bitis;

        internal void Onayla() => Durum = RandevuDurumu.Onaylandi;
        internal void Reddet() => Durum = RandevuDurumu.Reddedildi;
        internal void Iptal() => Durum = RandevuDurumu.IptalEdildi;

        public override string ToString() =>
            $"#{Id} | {Baslangic:dd.MM.yyyy HH:mm}-{Bitis:HH:mm} | {Hizmet.Ad} | {Calisan} | Hasta: {Hasta.AdSoyad} | {Ucret:N2} TL | {Durum}";
    }
}
