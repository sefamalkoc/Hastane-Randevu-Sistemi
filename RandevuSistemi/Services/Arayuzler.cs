using RandevuSistemi.Models;

namespace RandevuSistemi.Services
{
    // Veri saklama soyutlamasi: bellek, dosya veya veritabani ile degistirilebilir (hocanin IDb ornegi gibi)
    public interface IRandevuDeposu
    {
        void Ekle(Randevu r);
        IEnumerable<Randevu> Tumu();
    }

    // Bildirim soyutlamasi
    public interface IBildirimServisi
    {
        void Gonder(Kullanici alici, string mesaj);
    }
}
