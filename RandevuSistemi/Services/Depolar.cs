using RandevuSistemi.Models;

namespace RandevuSistemi.Services
{
    public class BellekRandevuDeposu : IRandevuDeposu
    {
        private readonly List<Randevu> _liste = new();
        public void Ekle(Randevu r) => _liste.Add(r);
        public IEnumerable<Randevu> Tumu() => _liste;
        public virtual void Kaydet() { }
    }

    // Randevu detaylarini (islem, sure, ucret, calisan, tarih/saat, durum) CSV dosyasina kaydeder
    public class DosyaRandevuDeposu : BellekRandevuDeposu
    {
        private readonly string _yol;
        public DosyaRandevuDeposu(string yol) { _yol = yol; }

        public override void Kaydet()
        {
            var satirlar = new List<string> { "Id;Hasta;Calisan;Kurum/Sube;Islem;SureDk;Ucret;Baslangic;Bitis;Durum" };
            foreach (var r in Tumu())
                satirlar.Add(string.Join(";", r.Id, r.Hasta.AdSoyad, r.Calisan.AdSoyad, r.Calisan.Sube,
                    r.Hizmet.Ad, r.Hizmet.SureDakika, r.Ucret.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    r.Baslangic.ToString("yyyy-MM-dd HH:mm"), r.Bitis.ToString("yyyy-MM-dd HH:mm"), r.Durum));
            File.WriteAllLines(_yol, satirlar, System.Text.Encoding.UTF8);
        }
    }

    public class KonsolBildirim : IBildirimServisi
    {
        public void Gonder(Kullanici alici, string mesaj) =>
            Console.WriteLine($"   [Bildirim -> {alici.AdSoyad}] {mesaj}");
    }
}
