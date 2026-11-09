using RandevuSistemi.Models;

namespace RandevuSistemi.Services
{
    public class BellekRandevuDeposu : IRandevuDeposu
    {
        private readonly List<Randevu> _liste = new();
        public void Ekle(Randevu r) => _liste.Add(r);
        public IEnumerable<Randevu> Tumu() => _liste;
    }

    public class KonsolBildirim : IBildirimServisi
    {
        public void Gonder(Kullanici alici, string mesaj) =>
            Console.WriteLine($"   [Bildirim -> {alici.AdSoyad}] {mesaj}");
    }
}
