namespace RandevuSistemi.Models
{
    // Belirli bir gunde baslangic-bitis araligi (sube calisma saati veya doktor uygunlugu icin kullanilir)
    public class CalismaSaati
    {
        public DayOfWeek Gun { get; }
        public TimeSpan Baslangic { get; }
        public TimeSpan Bitis { get; }

        public CalismaSaati(DayOfWeek gun, TimeSpan baslangic, TimeSpan bitis)
        {
            if (bitis <= baslangic) throw new ArgumentException("Bitis saati baslangictan sonra olmali.");
            Gun = gun; Baslangic = baslangic; Bitis = bitis;
        }

        // [bas, bit) araligi bu zaman diliminin icine tamamen sigiyor mu?
        public bool Kapsar(DateTime bas, DateTime bit) =>
            bas.DayOfWeek == Gun && bas.Date == bit.Date &&
            bas.TimeOfDay >= Baslangic && bit.TimeOfDay <= Bitis;

        public override string ToString() => $"{Gun} {Baslangic:hh\\:mm}-{Bitis:hh\\:mm}";
    }
}
