using Microsoft.Data.SqlClient;

namespace RandevuSistemi.Services
{
    public readonly record struct RandevuSatiri(
        int Id,
        string HastaKimlik,
        string CalisanKimlik,
        string HizmetAdi,
        DateTime Baslangic,
        string Durum);

    /// <summary>SQL Server appointment persistence. IDs remain stable across restarts.</summary>
    public class SqlRandevuDeposu
    {
        private readonly string _connectionString;

        public SqlRandevuDeposu(string connectionString) => _connectionString = connectionString;

        public void TekrarlariTemizle()
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            const string sql = @"
                ;WITH c AS (
                    SELECT Id, ROW_NUMBER() OVER (
                        PARTITION BY HastaKimlik, CalisanKimlik, Baslangic ORDER BY Id) AS sira
                    FROM Randevular
                ) DELETE FROM c WHERE sira > 1;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Randevular_Kayit' AND object_id = OBJECT_ID('Randevular'))
                    CREATE UNIQUE INDEX UX_Randevular_Kayit ON Randevular (HastaKimlik, CalisanKimlik, Baslangic);";
            using var cmd = new SqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }

        public List<RandevuSatiri> Oku()
        {
            var liste = new List<RandevuSatiri>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            const string sql = "SELECT Id, HastaKimlik, CalisanKimlik, HizmetAdi, Baslangic, Durum FROM Randevular ORDER BY Id;";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                liste.Add(new RandevuSatiri(
                    reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), reader.GetDateTime(4), reader.GetString(5)));
            }
            return liste;
        }

        public int Ekle(string hastaKimlik, string calisanKimlik, string hizmetAdi, DateTime baslangic, DateTime bitis, decimal ucret, string durum)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            const string sql = @"
                DECLARE @id INT;
                SELECT @id = Id FROM Randevular WITH (UPDLOCK, HOLDLOCK)
                WHERE HastaKimlik = @hasta AND CalisanKimlik = @calisan AND Baslangic = @baslangic;
                IF @id IS NULL
                    INSERT INTO Randevular (HastaKimlik, CalisanKimlik, HizmetAdi, Baslangic, Bitis, Ucret, Durum)
                    VALUES (@hasta, @calisan, @hizmet, @baslangic, @bitis, @ucret, @durum);
                ELSE
                    UPDATE Randevular SET HizmetAdi = @hizmet, Bitis = @bitis, Ucret = @ucret, Durum = @durum WHERE Id = @id;
                SELECT COALESCE(@id, CONVERT(INT, SCOPE_IDENTITY()));";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@hasta", hastaKimlik);
            cmd.Parameters.AddWithValue("@calisan", calisanKimlik);
            cmd.Parameters.AddWithValue("@hizmet", hizmetAdi);
            cmd.Parameters.AddWithValue("@baslangic", baslangic);
            cmd.Parameters.AddWithValue("@bitis", bitis);
            cmd.Parameters.AddWithValue("@ucret", ucret);
            cmd.Parameters.AddWithValue("@durum", durum);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void DurumGuncelle(int id, string durum)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            const string sql = "UPDATE Randevular SET Durum = @durum WHERE Id = @id;";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@durum", durum);
            cmd.Parameters.AddWithValue("@id", id);
            if (cmd.ExecuteNonQuery() != 1)
                throw new InvalidOperationException($"SQL randevusu bulunamadı (Id={id}).");
        }
    }
}
