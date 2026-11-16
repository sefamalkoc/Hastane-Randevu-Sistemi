using System.Collections.Concurrent;
using System.Security.Cryptography;
using RandevuSistemi.Models;

namespace RandevuSistemi.Services
{
    /// <summary>
    /// Girişten sonra verilen bearer token'ı bellekte tutar.
    /// API, istek gövdesindeki kimliğe değil bu oturuma bakar.
    /// </summary>
    public class OturumServisi
    {
        private sealed record Oturum(string Kimlik, DateTime SonKullanimUtc);
        private static readonly TimeSpan OturumOmru = TimeSpan.FromHours(8);
        private readonly ConcurrentDictionary<string, Oturum> _oturumlar = new();

        public string Ac(string kimlik)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            _oturumlar[token] = new Oturum(kimlik, DateTime.UtcNow.Add(OturumOmru));
            return token;
        }

        public void Kapat(HttpRequest req)
        {
            var token = TokenAl(req);
            if (token != null) _oturumlar.TryRemove(token, out _);
        }

        public Kullanici? KullaniciGetir(HttpRequest req, VeriYoneticisi veri)
        {
            var token = TokenAl(req);
            if (token == null || !_oturumlar.TryGetValue(token, out var oturum)) return null;
            if (oturum.SonKullanimUtc <= DateTime.UtcNow)
            {
                _oturumlar.TryRemove(token, out _);
                return null;
            }
            return veri.KullaniciBul(oturum.Kimlik);
        }

        private static string? TokenAl(HttpRequest req)
        {
            if (!req.Headers.TryGetValue("Authorization", out var baslik)) return null;
            var deger = baslik.ToString().Trim();
            const string onek = "Bearer ";
            if (!deger.StartsWith(onek, StringComparison.OrdinalIgnoreCase)) return null;
            deger = deger[onek.Length..].Trim();
            return string.IsNullOrWhiteSpace(deger) ? null : deger;
        }
    }
}
