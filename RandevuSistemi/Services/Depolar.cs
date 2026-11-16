using RandevuSistemi.Models;

namespace RandevuSistemi.Services
{
    public class BellekRandevuDeposu : IRandevuDeposu
    {
        private readonly List<Randevu> _liste = new();
        private readonly object _kilit = new();
        public void Ekle(Randevu r) { lock (_kilit) _liste.Add(r); }
        public IEnumerable<Randevu> Tumu() { lock (_kilit) return _liste.ToArray(); }
    }

    public class KonsolBildirim : IBildirimServisi
    {
        public void Gonder(Kullanici alici, string mesaj) =>
            Console.WriteLine($"   [Bildirim -> {alici.AdSoyad}] {mesaj}");
    }
}
