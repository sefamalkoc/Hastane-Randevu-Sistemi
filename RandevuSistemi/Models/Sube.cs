namespace RandevuSistemi.Models
{
    // Bir kurumun subesi: calisma saatleri, bolumleri, hizmetleri ve calisanlari vardir.
    public class Sube
    {
        private readonly List<CalismaSaati> _calismaSaatleri = new();
        private readonly List<Bolum> _bolumler = new();
        private readonly List<Hizmet> _hizmetler = new();
        private readonly List<Calisan> _calisanlar = new();

        public Kurum Kurum { get; }
        public string Ad { get; }
        public string Adres { get; }
        public IReadOnlyList<CalismaSaati> CalismaSaatleri => _calismaSaatleri;
        public IReadOnlyList<Bolum> Bolumler => _bolumler;
        public IReadOnlyList<Hizmet> Hizmetler => _hizmetler;
        public IReadOnlyList<Calisan> Calisanlar => _calisanlar;

        internal Sube(Kurum kurum, string ad, string adres) { Kurum = kurum; Ad = ad; Adres = adres; }

        public void CalismaSaatiEkle(CalismaSaati c) => _calismaSaatleri.Add(c);
        public Bolum BolumEkle(string ad) { var b = new Bolum(ad); _bolumler.Add(b); return b; }
        public Hizmet HizmetEkle(string ad, int sure, decimal ucret)
        {
            var h = new Hizmet(ad, sure, ucret); _hizmetler.Add(h); return h;
        }
        public void CalisanEkle(Calisan c) { c.Sube = this; _calisanlar.Add(c); }

        public bool AcikMi(DateTime bas, DateTime bit) => _calismaSaatleri.Any(c => c.Kapsar(bas, bit));

        public override string ToString() => $"{Kurum.Ad} / {Ad}";
    }
}
