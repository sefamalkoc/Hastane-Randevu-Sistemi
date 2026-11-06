namespace RandevuSistemi.Models
{
    public class Bolum
    {
        public string Ad { get; }
        public Bolum(string ad) { Ad = ad; }
        public override string ToString() => Ad;
    }
}
