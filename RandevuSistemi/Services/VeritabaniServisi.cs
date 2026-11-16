using Microsoft.Data.SqlClient;

namespace RandevuSistemi.Services
{
    /// <summary>
    /// Microsoft SQL Server (MSSQL / SSMS 20) veritabanı yönetim servisi.
    /// Veritabanı ve tabloların otomatik oluşturulmasını ve bağlantı denetimini sağlar.
    /// </summary>
    public class VeritabaniServisi
    {
        public string? CalisanBaglantiDizesi { get; private set; }
        public bool BaglantiBasarili { get; private set; }
        public string? SonBaglantiHatasi { get; private set; }

        private readonly string[] _baglantiSecenekleri = new[]
        {
            @"Server=.\SQLSERVER;Database=HastaneRandevuDb;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=3;",
            @"Server=localhost\SQLSERVER;Database=HastaneRandevuDb;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=3;",
            @"Server=(local)\SQLSERVER;Database=HastaneRandevuDb;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=3;",
            @"Server=127.0.0.1\SQLSERVER;Database=HastaneRandevuDb;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=3;",
            @"Server=.;Database=HastaneRandevuDb;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=3;",
            @"Server=localhost;Database=HastaneRandevuDb;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=3;",
            @"Server=(local);Database=HastaneRandevuDb;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=3;"
        };

        public VeritabaniServisi(string? ozelBaglanti = null)
        {
            if (!string.IsNullOrWhiteSpace(ozelBaglanti))
            {
                _baglantiSecenekleri = new[] { ozelBaglanti }.Concat(_baglantiSecenekleri).ToArray();
            }
        }

        /// <summary>
        /// SQL Server bağlantısını test eder ve çalışan bağlantı dizesini kaydeder.
        /// </summary>
        public bool BaglantiyiDene()
        {
            foreach (var connStr in _baglantiSecenekleri)
            {
                try
                {
                    // Önce master veritabanı üzerinden sunucuya erişimi sına
                    var masterBuilder = new SqlConnectionStringBuilder(connStr)
                    {
                        InitialCatalog = "master",
                        ConnectTimeout = 3
                    };

                    using (var conn = new SqlConnection(masterBuilder.ConnectionString))
                    {
                        conn.Open();
                        // Veritabanı yoksa oluştur
                        const string createDbSql = @"
                            IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'HastaneRandevuDb')
                            BEGIN
                                CREATE DATABASE HastaneRandevuDb;
                            END";
                        using var cmd = new SqlCommand(createDbSql, conn);
                        cmd.ExecuteNonQuery();
                    }

                    // Şimdi hedef veritabanına bağlan
                    using (var conn = new SqlConnection(connStr))
                    {
                        conn.Open();
                        CalisanBaglantiDizesi = connStr;
                        BaglantiBasarili = true;

                        // Tabloları oluştur
                        TablolariHazirla(conn);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    SonBaglantiHatasi = $"{ex.GetType().Name}: {ex.Message}";
                    // Bu bağlantı dizesi çalışmadı, bir sonrakini dene
                }
            }

            BaglantiBasarili = false;
            return false;
        }

        private static void TablolariHazirla(SqlConnection conn)
        {
            const string ddl = @"
                IF OBJECT_ID('Kurumlar', 'U') IS NULL
                CREATE TABLE Kurumlar (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Ad NVARCHAR(150) NOT NULL,
                    Tur NVARCHAR(50) NOT NULL
                );

                IF OBJECT_ID('Subeler', 'U') IS NULL
                CREATE TABLE Subeler (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    KurumId INT NOT NULL,
                    Ad NVARCHAR(150) NOT NULL,
                    Adres NVARCHAR(300) NOT NULL
                );

                IF OBJECT_ID('Bolumler', 'U') IS NULL
                CREATE TABLE Bolumler (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    SubeId INT NOT NULL,
                    Ad NVARCHAR(100) NOT NULL
                );

                IF OBJECT_ID('Hizmetler', 'U') IS NULL
                CREATE TABLE Hizmetler (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    SubeId INT NOT NULL,
                    Ad NVARCHAR(100) NOT NULL,
                    SureDakika INT NOT NULL,
                    Ucret DECIMAL(18,2) NOT NULL
                );

                IF OBJECT_ID('Kullanicilar', 'U') IS NULL
                CREATE TABLE Kullanicilar (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Kimlik NVARCHAR(50) NOT NULL UNIQUE,
                    Ad NVARCHAR(100) NOT NULL,
                    Soyad NVARCHAR(100) NOT NULL,
                    Rol NVARCHAR(50) NOT NULL,
                    Sifre NVARCHAR(100) NOT NULL,
                    TcNo NVARCHAR(11) NULL,
                    Unvan NVARCHAR(100) NULL,
                    Uzmanlik NVARCHAR(100) NULL,
                    SubeId INT NULL
                );

                IF OBJECT_ID('CalisanHizmetler', 'U') IS NULL
                CREATE TABLE CalisanHizmetler (
                    CalisanKimlik NVARCHAR(50) NOT NULL,
                    HizmetAdi NVARCHAR(100) NOT NULL,
                    PRIMARY KEY (CalisanKimlik, HizmetAdi)
                );

                IF OBJECT_ID('CalismaSaatleri', 'U') IS NULL
                CREATE TABLE CalismaSaatleri (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    SubeId INT NULL,
                    CalisanKimlik NVARCHAR(50) NULL,
                    Gun NVARCHAR(20) NOT NULL,
                    Baslangic TIME NOT NULL,
                    Bitis TIME NOT NULL
                );

                IF OBJECT_ID('Randevular', 'U') IS NULL
                CREATE TABLE Randevular (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    HastaKimlik NVARCHAR(50) NOT NULL,
                    CalisanKimlik NVARCHAR(50) NOT NULL,
                    HizmetAdi NVARCHAR(100) NOT NULL,
                    Baslangic DATETIME NOT NULL,
                    Bitis DATETIME NOT NULL,
                    Ucret DECIMAL(18,2) NOT NULL,
                    Durum NVARCHAR(50) NOT NULL DEFAULT 'Beklemede'
                );";

            using var cmd = new SqlCommand(ddl, conn);
            cmd.ExecuteNonQuery();
        }
    }
}

