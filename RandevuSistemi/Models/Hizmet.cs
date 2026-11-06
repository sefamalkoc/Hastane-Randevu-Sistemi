namespace RandevuSistemi.Models
{
    // Kurumun sundugu saglik hizmeti (sure ve ucret ile)
    public class Hizmet
    {
        public string Ad { get; }
        public int SureDakika { get; }
        public decimal Ucret { get; }

        public Hizmet(string ad, int sureDakika, decimal ucret)
        {
            if (string.IsNullOrWhiteSpace(ad)) throw new ArgumentException("Hizmet adi bos olamaz.");
            if (sureDakika <= 0) throw new ArgumentException("Sure 0'dan buyuk olmali.");
            if (ucret < 0) throw new ArgumentException("Ucret negatif olamaz.");
            Ad = ad; SureDakika = sureDakika; Ucret = ucret;
        }

        public override string ToString() => $"{Ad} ({SureDakika} dk, {Ucret:N2} TL)";
    }
}
