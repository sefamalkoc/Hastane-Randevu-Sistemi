namespace RandevuSistemi.Models
{
    public class Hasta : Kullanici
    {
        public string TcNo { get; }
        public override Rol Rol => Rol.Hasta;
        public override bool RandevuOnaylayabilir => false;

        public Hasta(string ad, string soyad, string tcNo) : base(ad, soyad) { TcNo = tcNo; }

        public override string MenuBasligi() => "HASTA MENUSU";
    }
}
