namespace RandevuSistemi.Models
{
    // Hastane ve Klinik ortak ozellikleri (KALITIM). Birden cok sube desteklenir.
    public abstract class Kurum
    {
        private readonly List<Sube> _subeler = new();
        public string Ad { get; }
        public IReadOnlyList<Sube> Subeler => _subeler;
        public abstract string Tur { get; }

        protected Kurum(string ad) { Ad = ad; }

        public Sube SubeEkle(string ad, string adres)
        {
            var s = new Sube(this, ad, adres);
            _subeler.Add(s);
            return s;
        }

        public override string ToString() => $"{Tur}: {Ad} ({_subeler.Count} sube)";
    }

    public class Hastane : Kurum
    {
        public override string Tur => "Hastane";
        public Hastane(string ad) : base(ad) { }
    }

    public class Klinik : Kurum
    {
        public override string Tur => "Klinik";
        public Klinik(string ad) : base(ad) { }
    }
}
