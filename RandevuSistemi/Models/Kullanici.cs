namespace RandevuSistemi.Models
{
    // Soyut taban sinif: tum kullanicilar ortak ozellikleri buradan alir (KALITIM)
    public abstract class Kullanici
    {
        private static int _sayac = 0;

        public int Id { get; }
        public string Ad { get; }
        public string Soyad { get; }
        public string AdSoyad => $"{Ad} {Soyad}";
        public abstract Rol Rol { get; }

        protected Kullanici(string ad, string soyad)
        {
            Id = ++_sayac;
            Ad = ad; Soyad = soyad;
        }

        // Cok bicimlilik: her rol kendi yetkisini tanimlar
        public abstract bool RandevuOnaylayabilir { get; }
        public abstract string MenuBasligi();

        public override string ToString() => $"[{Rol}] {AdSoyad}";
    }
}
