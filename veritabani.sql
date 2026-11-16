-- ============================================================================
-- Hospital appointment database schema (non-destructive and repeatable).
-- Run in SQL Server Management Studio. Master data and accounts are stored in JSON.
-- ============================================================================

-- 1. VERİTABANI OLUŞTURMA
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'HastaneRandevuDb')
BEGIN
    CREATE DATABASE HastaneRandevuDb;
    PRINT 'HastaneRandevuDb veritabanı başarıyla oluşturuldu.';
END
GO

USE HastaneRandevuDb;
GO

-- 2. TABLOLARI OLUŞTURMA
-- Tablolar mevcutsa temizleme sırası
GO

-- Kurumlar (Hastane, Klinik)
IF OBJECT_ID('Kurumlar', 'U') IS NULL
BEGIN
CREATE TABLE Kurumlar (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Ad NVARCHAR(150) NOT NULL,
    Tur NVARCHAR(50) NOT NULL -- 'Hastane' veya 'Klinik'
);
END;

-- Şubeler
IF OBJECT_ID('Subeler', 'U') IS NULL
BEGIN
CREATE TABLE Subeler (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    KurumId INT NOT NULL FOREIGN KEY REFERENCES Kurumlar(Id) ON DELETE CASCADE,
    Ad NVARCHAR(150) NOT NULL,
    Adres NVARCHAR(300) NOT NULL
);
END;

-- Poliklinikler / Bölümler
IF OBJECT_ID('Bolumler', 'U') IS NULL
BEGIN
CREATE TABLE Bolumler (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SubeId INT NOT NULL FOREIGN KEY REFERENCES Subeler(Id) ON DELETE CASCADE,
    Ad NVARCHAR(100) NOT NULL
);
END;

-- Sağlık Hizmetleri (Muayene, EKG, Tetkik vb.)
IF OBJECT_ID('Hizmetler', 'U') IS NULL
BEGIN
CREATE TABLE Hizmetler (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SubeId INT NOT NULL FOREIGN KEY REFERENCES Subeler(Id) ON DELETE CASCADE,
    Ad NVARCHAR(100) NOT NULL,
    SureDakika INT NOT NULL CHECK (SureDakika > 0),
    Ucret DECIMAL(18,2) NOT NULL CHECK (Ucret >= 0)
);
END;

-- Kullanıcılar (Hasta, Doktor, Sağlık Personeli, Yönetici)
IF OBJECT_ID('Kullanicilar', 'U') IS NULL
BEGIN
CREATE TABLE Kullanicilar (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Kimlik NVARCHAR(50) NOT NULL UNIQUE,
    Ad NVARCHAR(100) NOT NULL,
    Soyad NVARCHAR(100) NOT NULL,
    Rol NVARCHAR(50) NOT NULL, -- 'Hasta', 'Doktor', 'Personel', 'Yonetici'
    Sifre NVARCHAR(100) NOT NULL,
    TcNo NVARCHAR(11) NULL,
    Unvan NVARCHAR(100) NULL,
    Uzmanlik NVARCHAR(100) NULL,
    SubeId INT NULL FOREIGN KEY REFERENCES Subeler(Id)
);
END;

-- Çalışanların Sunduğu Hizmetler (Çoka Çok İlişki)
IF OBJECT_ID('CalisanHizmetler', 'U') IS NULL
BEGIN
CREATE TABLE CalisanHizmetler (
    CalisanKimlik NVARCHAR(50) NOT NULL,
    HizmetAdi NVARCHAR(100) NOT NULL,
    PRIMARY KEY (CalisanKimlik, HizmetAdi)
);
END;

-- Çalışma Saatleri (Şube ve Çalışan Uygunlukları)
IF OBJECT_ID('CalismaSaatleri', 'U') IS NULL
BEGIN
CREATE TABLE CalismaSaatleri (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SubeId INT NULL FOREIGN KEY REFERENCES Subeler(Id),
    CalisanKimlik NVARCHAR(50) NULL,
    Gun NVARCHAR(20) NOT NULL, -- 'Monday', 'Tuesday', ...
    Baslangic TIME NOT NULL,
    Bitis TIME NOT NULL
);
END;

-- Randevular
IF OBJECT_ID('Randevular', 'U') IS NULL
BEGIN
CREATE TABLE Randevular (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    HastaKimlik NVARCHAR(50) NOT NULL,
    CalisanKimlik NVARCHAR(50) NOT NULL,
    HizmetAdi NVARCHAR(100) NOT NULL,
    Baslangic DATETIME NOT NULL,
    Bitis DATETIME NOT NULL,
    Ucret DECIMAL(18,2) NOT NULL,
    Durum NVARCHAR(50) NOT NULL DEFAULT 'Beklemede' -- 'Beklemede', 'Onaylandi', 'Reddedildi', 'IptalEdildi'
);
END;
GO

IF OBJECT_ID('SistemVerisi', 'U') IS NULL
BEGIN
    CREATE TABLE SistemVerisi (
        Id TINYINT NOT NULL PRIMARY KEY CHECK (Id = 1),
        JsonVeri NVARCHAR(MAX) NOT NULL,
        GuncellemeZamani DATETIME2 NOT NULL CONSTRAINT DF_SistemVerisi_Guncelleme DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_SistemVerisi_Json CHECK (ISJSON(JsonVeri) = 1)
    );
END;
GO

PRINT 'HastaneRandevuDb schema is ready.';
GO

