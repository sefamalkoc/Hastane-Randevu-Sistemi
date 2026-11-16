using System.Text.Json;
using RandevuSistemi.Services;

namespace RandevuSistemi
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // data/ klasörü konumu belirleme (önce proje köküne bak, böylece veriler kalıcı olur)
            var baseDir = AppContext.BaseDirectory;
            var projKok = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            var veriYolu = Path.Combine(projKok, "data", "veri.json");
            var randevuYolu = Path.Combine(projKok, "data", "randevular.json");

            if (!File.Exists(veriYolu))
            {
                veriYolu = Path.Combine(baseDir, "data", "veri.json");
                randevuYolu = Path.Combine(baseDir, "data", "randevular.json");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(veriYolu)!);
            if (!File.Exists(veriYolu))
                File.WriteAllText(veriYolu, "{\"kullanicilar\":[],\"calisanlar\":[],\"kurumlar\":[]}", System.Text.Encoding.UTF8);
            if (!File.Exists(randevuYolu))
                File.WriteAllText(randevuYolu, "[]", System.Text.Encoding.UTF8);

            VeriYoneticisi veriMgr;
            try
            {
                veriMgr = new VeriYoneticisi(veriYolu, randevuYolu);

                // SQL Server (MSSQL / SSMS 20) bağlantısını sına
                var vtServisi = new VeritabaniServisi(SqlBaglantiOku());
                bool sqlBaglandi = false;
                try
                {
                    sqlBaglandi = vtServisi.BaglantiyiDene();
                }
                catch { }

                if (sqlBaglandi && !string.IsNullOrWhiteSpace(vtServisi.CalisanBaglantiDizesi))
                {
                    veriMgr.SqlServerBagli = true;
                    veriMgr.SqlBaglantiDizesi = vtServisi.CalisanBaglantiDizesi;
                    veriMgr.VeritabaniBilgisi = "Randevu, kurum ve kullanıcı verileri: SQL Server";
                    var sqlDepo = new SqlRandevuDeposu(vtServisi.CalisanBaglantiDizesi);
                    var sqlAnaVeriDeposu = new SqlAnaVeriDeposu(vtServisi.CalisanBaglantiDizesi);
                    sqlAnaVeriDeposu.Hazirla();
                    veriMgr.Yukle(sqlDepo, sqlAnaVeriDeposu);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(vtServisi.SonBaglantiHatasi))
                        Console.WriteLine($"[SQL BAĞLANTI HATASI] {vtServisi.SonBaglantiHatasi}");
                    veriMgr.Yukle();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[HATA] Veri yüklenemedi: {ex.Message}");
                Console.ResetColor();
                return;
            }

            // Eğer --console parametresi verildiyse konsol arayüzünü aç
            if (args.Contains("--console"))
            {
                new KonsolArayuz(veriMgr).Baslat();
                return;
            }

            // Varsayılan olarak modern Web Sunucusunu başlat
            BaslatWebSunucusu(args, veriMgr);
        }

        private static void BaslatWebSunucusu(string[] args, VeriYoneticisi veriMgr)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                WebRootPath = BulWwwRoot()
            });

            // Port ayarı: 5000 portu (127.0.0.1 ve localhost)
            var urls = builder.Configuration["urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(urls)
                ? new[] { "http://127.0.0.1:5000", "http://localhost:5000" }
                : urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            var app = builder.Build();

            // Statik dosyalar (HTML, CSS, JS)
            app.UseDefaultFiles();
            app.UseStaticFiles();

            // API Uç Noktaları
            RandevuWebApi.MapEndpoints(app, veriMgr, new OturumServisi());

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n" + new string('=', 65));
            Console.WriteLine("  HASTANE / KLİNİK RANDEVU SİSTEMİ — WEB SUNUCUSU");
            Console.WriteLine(new string('=', 65));
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n  ✓ Web arayüzü hazır!");
            Console.WriteLine("  🌐 Tarayıcınızda açın: http://localhost:5000");

            if (veriMgr.SqlServerBagli)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  SQL bağlantısı: {veriMgr.VeritabaniBilgisi}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Veri deposu: JSON (SQL Server kullanılamıyor veya yapılandırılmamış)");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  💡 SSMS 20 için 'veritabani.sql' scriptini çalıştırabilirsiniz.");
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  (Konsol modunda çalıştırmak için: dotnet run -- --console)");
            Console.WriteLine("  (Durdurmak için Ctrl + C tuşlarına basın)\n");
            Console.ResetColor();

            app.Run();
        }

        private static string? SqlBaglantiOku()
        {
            var ortamDegeri = Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer");
            if (!string.IsNullOrWhiteSpace(ortamDegeri)) return ortamDegeri;

            foreach (var yol in new[]
            {
                Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "appsettings.json"))
            })
            {
                if (!File.Exists(yol)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(yol));
                    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
                        cs.TryGetProperty("SqlServer", out var s))
                        return s.GetString();
                }
                catch
                {
                    // Sonraki aday dosyaya geç
                }
            }
            return null;
        }

        private static string BulWwwRoot()
        {
            var baseDir = AppContext.BaseDirectory;
            var path = Path.Combine(baseDir, "wwwroot");
            if (Directory.Exists(path)) return path;

            var projKok = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            path = Path.Combine(projKok, "wwwroot");
            if (Directory.Exists(path)) return path;

            Directory.CreateDirectory(path);
            return path;
        }
    }
}
