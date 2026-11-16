using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace RandevuSistemi.Services
{
    public static class SifreKoruma
    {
        private const int Iterasyon = 310_000;
        private const int SaltUzunluk = 16;
        private const int HashUzunluk = 32;
        private const string OnEk = "pbkdf2-sha256";

        public static bool HashMi(string deger) =>
            deger.StartsWith(OnEk + "$", StringComparison.Ordinal) || EskiSha256Mi(deger);

        public static string Hashle(string sifre)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltUzunluk);
            var hash = Rfc2898DeriveBytes.Pbkdf2(sifre, salt, Iterasyon, HashAlgorithmName.SHA256, HashUzunluk);
            return string.Join('$', OnEk, Iterasyon.ToString(CultureInfo.InvariantCulture),
                Convert.ToBase64String(salt), Convert.ToBase64String(hash));
        }

        public static bool Dogrula(string sifre, string kayitliHash, out string? guncelHash)
        {
            guncelHash = null;
            if (kayitliHash.StartsWith(OnEk + "$", StringComparison.Ordinal))
            {
                var bolumler = kayitliHash.Split('$');
                if (bolumler.Length != 4 || !int.TryParse(bolumler[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterasyon)
                    || iterasyon < 100_000 || iterasyon > 2_000_000)
                    return false;
                try
                {
                    var salt = Convert.FromBase64String(bolumler[2]);
                    var beklenen = Convert.FromBase64String(bolumler[3]);
                    if (salt.Length != SaltUzunluk || beklenen.Length != HashUzunluk) return false;
                    var hesaplanan = Rfc2898DeriveBytes.Pbkdf2(sifre, salt, iterasyon, HashAlgorithmName.SHA256, beklenen.Length);
                    return CryptographicOperations.FixedTimeEquals(hesaplanan, beklenen);
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException or CryptographicException) { return false; }
            }

            if (!EskiSha256Mi(kayitliHash)) return false;
            var eski = Convert.FromHexString(kayitliHash);
            var girilen = SHA256.HashData(Encoding.UTF8.GetBytes(sifre));
            if (!CryptographicOperations.FixedTimeEquals(girilen, eski)) return false;
            guncelHash = Hashle(sifre);
            return true;
        }

        private static bool EskiSha256Mi(string deger) =>
            deger.Length == 64 && deger.All(c => char.IsAsciiHexDigit(c));
    }
}
