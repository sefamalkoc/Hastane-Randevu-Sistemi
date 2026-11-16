namespace RandevuSistemi.Models
{
    public class Randevu
    {
        private static int _sayac = 0;

        public int Id { get; private set; }
        public Hasta Hasta { get; }
        public Calisan Calisan { get; }
        public Hizmet Hizmet { get; }
        public DateTime Baslangic { get; }
        public DateTime Bitis => Baslangic.AddMinutes(Hizmet.SureDakika);
        public decimal Ucret => Hizmet.Ucret;
        public RandevuDurumu Durum { get; private set; } = RandevuDurumu.Beklemede;

        internal Randevu(Hasta hasta, Calisan calisan, Hizmet hizmet, DateTime baslangic, int? id = null)
        {
            Id = id is > 0 ? id.Value : Interlocked.Increment(ref _sayac);
            SayaciIlerle(Id);
            Hasta = hasta; Calisan = calisan; Hizmet = hizmet; Baslangic = baslangic;
        }

        internal void KimlikAyarla(int id)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id;
            SayaciIlerle(id);
        }

        private static void SayaciIlerle(int id)
        {
            int mevcut;
            while ((mevcut = Volatile.Read(ref _sayac)) < id &&
                   Interlocked.CompareExchange(ref _sayac, id, mevcut) != mevcut) { }
        }

        // Aktif = takvimde yer tutan randevu (cakisma kontrolunde sayilir)
        public bool Aktif => Durum == RandevuDurumu.Beklemede || Durum == RandevuDurumu.Onaylandi;

        public bool Cakisir(DateTime bas, DateTime bit) => Baslangic < bit && bas < Bitis;

        internal void Onayla() => Durum = RandevuDurumu.Onaylandi;
        internal void Reddet() => Durum = RandevuDurumu.Reddedildi;
        internal void Iptal() => Durum = RandevuDurumu.IptalEdildi;

        // JSON'dan geri yüklerken durumu doğrudan ayarlamak için kullanılır
        internal void DurumAyarla(RandevuDurumu d) => Durum = d;

        public override string ToString() =>
            $"#{Id} | {Baslangic:dd.MM.yyyy HH:mm}-{Bitis:HH:mm} | {Hizmet.Ad} | {Calisan} | Hasta: {Hasta.AdSoyad} | {Ucret:N2} TL | {Durum}";
    }
}
