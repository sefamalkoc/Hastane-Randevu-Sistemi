namespace RandevuSistemi.Models
{
    // Doktor ve saglik personeli icin ortak sinif
    public abstract class Calisan : Kullanici
    {
        private readonly List<Hizmet> _hizmetler = new();
        private readonly List<CalismaSaati> _uygunluklar = new();

        public Sube? Sube { get; internal set; }
        public IReadOnlyList<Hizmet> Hizmetler => _hizmetler;
        public IReadOnlyList<CalismaSaati> Uygunluklar => _uygunluklar;

        protected Calisan(string ad, string soyad) : base(ad, soyad) { }

        public void HizmetEkle(Hizmet h)
        {
            if (!_hizmetler.Contains(h)) _hizmetler.Add(h);
        }

        public void UygunlukEkle(CalismaSaati c) => _uygunluklar.Add(c);

        public bool HizmetSunuyorMu(Hizmet h) => _hizmetler.Contains(h);

        public bool UygunMu(DateTime bas, DateTime bit) => _uygunluklar.Any(u => u.Kapsar(bas, bit));
    }

    public class Doktor : Calisan
    {
        public string Uzmanlik { get; }
        public override Rol Rol => Rol.Doktor;
        public override bool RandevuOnaylayabilir => true;

        public Doktor(string ad, string soyad, string uzmanlik) : base(ad, soyad) { Uzmanlik = uzmanlik; }

        public override string MenuBasligi() => "DOKTOR MENUSU";
        public override string ToString() => $"Dr. {AdSoyad} ({Uzmanlik})";
    }

    public class SaglikPersoneli : Calisan
    {
        public string Unvan { get; }
        public override Rol Rol => Rol.Doktor; // doktor paneli yetkileri ile calisir
        public override bool RandevuOnaylayabilir => true;

        public SaglikPersoneli(string ad, string soyad, string unvan) : base(ad, soyad) { Unvan = unvan; }

        public override string MenuBasligi() => "SAGLIK PERSONELI MENUSU";
        public override string ToString() => $"{Unvan} {AdSoyad}";
    }
}
