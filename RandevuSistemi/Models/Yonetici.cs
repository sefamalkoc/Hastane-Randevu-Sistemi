namespace RandevuSistemi.Models
{
    // Yetki cercevesi: Kurum == null ise sistem yoneticisidir (tum kurumlari ve yeni kurum tanimlayabilir).
    // Kurum doluysa yalnizca o hastane/klinigi yonetebilir.
    public class Yonetici : Kullanici
    {
        public Kurum? Kurum { get; }
        public override Rol Rol => Rol.Yonetici;
        public override bool RandevuOnaylayabilir => true;
        public bool SistemYoneticisi => Kurum == null;

        public Yonetici(string ad, string soyad, Kurum? kurum = null) : base(ad, soyad) { Kurum = kurum; }

        public bool YonetebilirMi(Kurum k) => Kurum == null || Kurum == k;

        public override string MenuBasligi() =>
            SistemYoneticisi ? "SISTEM YONETICISI MENUSU" : $"YONETICI MENUSU ({Kurum!.Ad})";

        public override string ToString() =>
            SistemYoneticisi ? $"[Yonetici] {AdSoyad} (tum kurumlar)" : $"[Yonetici] {AdSoyad} ({Kurum!.Ad})";
    }
}
