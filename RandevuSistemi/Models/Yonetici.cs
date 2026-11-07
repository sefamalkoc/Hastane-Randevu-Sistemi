namespace RandevuSistemi.Models
{
    public class Yonetici : Kullanici
    {
        public override Rol Rol => Rol.Yonetici;
        public override bool RandevuOnaylayabilir => true;
        public Yonetici(string ad, string soyad) : base(ad, soyad) { }
        public override string MenuBasligi() => "YONETICI MENUSU";
    }
}
