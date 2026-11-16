using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;

namespace RandevuSistemi.Services
{
    /// <summary>SQL Server master-data source and transactional mirror for users and organizations.</summary>
    public sealed class SqlAnaVeriDeposu
    {
        private readonly string _connectionString;

        public SqlAnaVeriDeposu(string connectionString) => _connectionString = connectionString;

        public void Hazirla()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            const string sql = @"
                IF OBJECT_ID('SistemVerisi', 'U') IS NULL
                BEGIN
                    CREATE TABLE SistemVerisi (
                        Id TINYINT NOT NULL PRIMARY KEY CHECK (Id = 1),
                        JsonVeri NVARCHAR(MAX) NOT NULL,
                        GuncellemeZamani DATETIME2 NOT NULL CONSTRAINT DF_SistemVerisi_Guncelleme DEFAULT SYSUTCDATETIME(),
                        CONSTRAINT CK_SistemVerisi_Json CHECK (ISJSON(JsonVeri) = 1)
                    );
                END;";
            using var command = new SqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        public string? Oku()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using (var command = new SqlCommand("SELECT JsonVeri FROM SistemVerisi WHERE Id = 1;", connection))
            {
                var value = command.ExecuteScalar();
                if (value is string json) return json;
            }

            return RelasyonelVeriyiOku(connection);
        }

        public void Kaydet(string json)
        {
            using var document = JsonDocument.Parse(json);
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                const string saveSnapshot = @"
                    IF EXISTS (SELECT 1 FROM SistemVerisi WITH (UPDLOCK, HOLDLOCK) WHERE Id = 1)
                        UPDATE SistemVerisi SET JsonVeri = @json, GuncellemeZamani = SYSUTCDATETIME() WHERE Id = 1;
                    ELSE
                        INSERT INTO SistemVerisi (Id, JsonVeri) VALUES (1, @json);";
                Execute(connection, transaction, saveSnapshot, ("@json", SqlDbType.NVarChar, -1, json));

                foreach (var table in new[] { "CalismaSaatleri", "CalisanHizmetler", "Kullanicilar", "Hizmetler", "Bolumler", "Subeler", "Kurumlar" })
                    Execute(connection, transaction, $"DELETE FROM {table};");

                var root = JsonNode.Parse(json)!.AsObject();
                var branchIds = new Dictionary<(string Kurum, string Sube), int>(new OrganizationBranchComparer());
                var employeeBranches = new Dictionary<string, (string Kurum, string Sube)>(StringComparer.OrdinalIgnoreCase);

                foreach (var institutionNode in root["kurumlar"]?.AsArray() ?? new JsonArray())
                {
                    var institution = institutionNode!.AsObject();
                    var institutionName = GetString(institution, "ad");
                    var institutionId = ExecuteScalarInt(connection, transaction,
                        "INSERT INTO Kurumlar (Ad, Tur) OUTPUT INSERTED.Id VALUES (@ad, @tur);",
                        ("@ad", SqlDbType.NVarChar, 150, institutionName),
                        ("@tur", SqlDbType.NVarChar, 50, GetString(institution, "tur", "Hastane")));

                    foreach (var branchNode in institution["subeler"]?.AsArray() ?? new JsonArray())
                    {
                        var branch = branchNode!.AsObject();
                        var branchName = GetString(branch, "ad");
                        var branchId = ExecuteScalarInt(connection, transaction,
                            "INSERT INTO Subeler (KurumId, Ad, Adres) OUTPUT INSERTED.Id VALUES (@kurumId, @ad, @adres);",
                            ("@kurumId", SqlDbType.Int, 0, institutionId),
                            ("@ad", SqlDbType.NVarChar, 150, branchName),
                            ("@adres", SqlDbType.NVarChar, 300, GetString(branch, "adres")));
                        branchIds[(institutionName, branchName)] = branchId;

                        foreach (var departmentNode in branch["bolumler"]?.AsArray() ?? new JsonArray())
                            Execute(connection, transaction,
                                "INSERT INTO Bolumler (SubeId, Ad) VALUES (@subeId, @ad);",
                                ("@subeId", SqlDbType.Int, 0, branchId),
                                ("@ad", SqlDbType.NVarChar, 100, departmentNode!.GetValue<string>()));

                        foreach (var serviceNode in branch["hizmetler"]?.AsArray() ?? new JsonArray())
                        {
                            var service = serviceNode!.AsObject();
                            Execute(connection, transaction,
                                "INSERT INTO Hizmetler (SubeId, Ad, SureDakika, Ucret) VALUES (@subeId, @ad, @sure, @ucret);",
                                ("@subeId", SqlDbType.Int, 0, branchId),
                                ("@ad", SqlDbType.NVarChar, 100, GetString(service, "ad")),
                                ("@sure", SqlDbType.Int, 0, service["sure"]?.GetValue<int>() ?? 30),
                                ("@ucret", SqlDbType.Decimal, 0, service["ucret"]?.GetValue<decimal>() ?? 0m));
                        }

                        foreach (var hourNode in branch["calismaSaatleri"]?.AsArray() ?? new JsonArray())
                        {
                            var hour = hourNode!.AsObject();
                            InsertHours(connection, transaction, branchId, null, hour);
                        }

                        foreach (var employeeNode in branch["calisanKimlikleri"]?.AsArray() ?? new JsonArray())
                            employeeBranches[employeeNode!.GetValue<string>()] = (institutionName, branchName);
                    }
                }

                var employeeDetails = (root["calisanlar"]?.AsArray() ?? new JsonArray())
                    .Where(node => node is not null)
                    .Select(node => node!.AsObject())
                    .ToDictionary(node => GetString(node, "kimlik"), StringComparer.OrdinalIgnoreCase);

                foreach (var userNode in root["kullanicilar"]?.AsArray() ?? new JsonArray())
                {
                    var user = userNode!.AsObject();
                    var kimlik = GetString(user, "kimlik");
                    employeeBranches.TryGetValue(kimlik, out var employeeBranch);
                    var branchId = employeeBranches.ContainsKey(kimlik) && branchIds.TryGetValue(employeeBranch, out var foundId)
                        ? foundId : (int?)null;
                    employeeDetails.TryGetValue(kimlik, out var employee);

                    Execute(connection, transaction, @"
                        INSERT INTO Kullanicilar (Kimlik, Ad, Soyad, Rol, Sifre, TcNo, Unvan, Uzmanlik, SubeId)
                        VALUES (@kimlik, @ad, @soyad, @rol, @sifre, @tcNo, @unvan, @uzmanlik, @subeId);",
                        ("@kimlik", SqlDbType.NVarChar, 50, kimlik),
                        ("@ad", SqlDbType.NVarChar, 100, GetString(user, "ad")),
                        ("@soyad", SqlDbType.NVarChar, 100, GetString(user, "soyad")),
                        ("@rol", SqlDbType.NVarChar, 50, GetString(user, "rol")),
                        ("@sifre", SqlDbType.NVarChar, 100, GetString(user, "sifre")),
                        ("@tcNo", SqlDbType.NVarChar, 11, GetNullableString(user, "tcNo")),
                        ("@unvan", SqlDbType.NVarChar, 100, employee is null ? null : GetNullableString(employee, "unvan")),
                        ("@uzmanlik", SqlDbType.NVarChar, 100, employee is null ? null : GetNullableString(employee, "uzmanlik")),
                        ("@subeId", SqlDbType.Int, 0, branchId));

                    if (employee is null) continue;
                    foreach (var serviceName in employee["hizmetAdlari"]?.AsArray() ?? new JsonArray())
                        Execute(connection, transaction,
                            "INSERT INTO CalisanHizmetler (CalisanKimlik, HizmetAdi) VALUES (@kimlik, @hizmet);",
                            ("@kimlik", SqlDbType.NVarChar, 50, kimlik),
                            ("@hizmet", SqlDbType.NVarChar, 100, serviceName!.GetValue<string>()));

                    foreach (var hourNode in employee["uygunluklar"]?.AsArray() ?? new JsonArray())
                        InsertHours(connection, transaction, null, kimlik, hourNode!.AsObject());
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static string? RelasyonelVeriyiOku(SqlConnection connection)
        {
            var users = new List<UserRow>();
            using (var command = new SqlCommand(@"
                SELECT Kimlik, Ad, Soyad, Rol, Sifre, TcNo, Unvan, Uzmanlik, SubeId FROM Kullanicilar ORDER BY Id;", connection))
            using (var reader = command.ExecuteReader())
                while (reader.Read()) users.Add(new UserRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), reader.GetString(4), NullableString(reader, 5), NullableString(reader, 6),
                    NullableString(reader, 7), reader.IsDBNull(8) ? null : reader.GetInt32(8)));

            var institutions = new List<InstitutionRow>();
            using (var command = new SqlCommand("SELECT Id, Ad, Tur FROM Kurumlar ORDER BY Id;", connection))
            using (var reader = command.ExecuteReader())
                while (reader.Read()) institutions.Add(new InstitutionRow(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));

            if (users.Count == 0 && institutions.Count == 0) return null;

            var branches = new List<BranchRow>();
            using (var command = new SqlCommand("SELECT Id, KurumId, Ad, Adres FROM Subeler ORDER BY Id;", connection))
            using (var reader = command.ExecuteReader())
                while (reader.Read()) branches.Add(new BranchRow(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3)));

            var departments = ReadPairs(connection, "SELECT SubeId, Ad FROM Bolumler ORDER BY Id;");
            var services = new List<ServiceRow>();
            using (var command = new SqlCommand("SELECT SubeId, Ad, SureDakika, Ucret FROM Hizmetler ORDER BY Id;", connection))
            using (var reader = command.ExecuteReader())
                while (reader.Read()) services.Add(new ServiceRow(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetDecimal(3)));

            var branchHours = ReadHours(connection, "SubeId IS NOT NULL AND CalisanKimlik IS NULL");
            var employeeHours = ReadHours(connection, "CalisanKimlik IS NOT NULL");
            var employeeServices = ReadStringPairs(connection, "SELECT CalisanKimlik, HizmetAdi FROM CalisanHizmetler;");
            var root = new JsonObject
            {
                ["kullanicilar"] = new JsonArray(),
                ["calisanlar"] = new JsonArray(),
                ["kurumlar"] = new JsonArray()
            };
            var institutionNodes = new Dictionary<int, JsonObject>();
            foreach (var row in institutions)
            {
                var node = new JsonObject { ["ad"] = row.Name, ["tur"] = row.Type, ["subeler"] = new JsonArray() };
                root["kurumlar"]!.AsArray().Add(node);
                institutionNodes[row.Id] = node;
            }

            var branchNodes = new Dictionary<int, JsonObject>();
            foreach (var row in branches)
            {
                if (!institutionNodes.TryGetValue(row.InstitutionId, out var institution)) continue;
                var node = new JsonObject
                {
                    ["ad"] = row.Name,
                    ["adres"] = row.Address,
                    ["calismaSaatleri"] = new JsonArray(),
                    ["bolumler"] = new JsonArray(),
                    ["hizmetler"] = new JsonArray(),
                    ["calisanKimlikleri"] = new JsonArray()
                };
                institution["subeler"]!.AsArray().Add(node);
                branchNodes[row.Id] = node;
                foreach (var hour in branchHours.Where(hour => hour.BranchId == row.Id)) node["calismaSaatleri"]!.AsArray().Add(HourNode(hour));
                foreach (var department in departments.Where(pair => pair.Key == row.Id)) node["bolumler"]!.AsArray().Add(JsonValue.Create(department.Value));
                foreach (var service in services.Where(service => service.BranchId == row.Id))
                    node["hizmetler"]!.AsArray().Add(new JsonObject { ["ad"] = service.Name, ["sure"] = service.Duration, ["ucret"] = service.Price });
            }

            var employees = new JsonArray();
            foreach (var user in users)
            {
                var userNode = new JsonObject
                {
                    ["kimlik"] = user.Identity, ["ad"] = user.FirstName, ["soyad"] = user.LastName,
                    ["rol"] = user.Role, ["sifre"] = user.Password
                };
                if (user.TcNo is not null) userNode["tcNo"] = user.TcNo;
                root["kullanicilar"]!.AsArray().Add(userNode);

                if (user.Role is "Doktor" or "Personel")
                {
                    var employee = new JsonObject
                    {
                        ["kimlik"] = user.Identity,
                        ["uygunluklar"] = new JsonArray(),
                        ["hizmetAdlari"] = new JsonArray()
                    };
                    if (user.Specialty is not null) employee["uzmanlik"] = user.Specialty;
                    if (user.Title is not null) employee["unvan"] = user.Title;
                    foreach (var hour in employeeHours.Where(hour => hour.EmployeeId == user.Identity)) employee["uygunluklar"]!.AsArray().Add(HourNode(hour));
                    foreach (var pair in employeeServices.Where(pair => pair.Key == user.Identity)) employee["hizmetAdlari"]!.AsArray().Add(JsonValue.Create(pair.Value));
                    employees.Add(employee);
                    if (user.BranchId is int branchId && branchNodes.TryGetValue(branchId, out var branch))
                        branch["calisanKimlikleri"]!.AsArray().Add(JsonValue.Create(user.Identity));
                }
            }
            root["calisanlar"] = employees;
            return root.ToJsonString();
        }

        private static List<(int Key, string Value)> ReadPairs(SqlConnection connection, string sql)
        {
            var values = new List<(int, string)>();
            using var command = new SqlCommand(sql, connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetValue(0) is int key) values.Add((key, reader.GetString(1)));
            }
            return values;
        }

        private static List<(string Key, string Value)> ReadStringPairs(SqlConnection connection, string sql)
        {
            var values = new List<(string, string)>();
            using var command = new SqlCommand(sql, connection);
            using var reader = command.ExecuteReader();
            while (reader.Read()) values.Add((reader.GetString(0), reader.GetString(1)));
            return values;
        }

        private static List<HourRow> ReadHours(SqlConnection connection, string filter)
        {
            var values = new List<HourRow>();
            using var command = new SqlCommand($"SELECT SubeId, CalisanKimlik, Gun, Baslangic, Bitis FROM CalismaSaatleri WHERE {filter};", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read()) values.Add(new HourRow(reader.IsDBNull(0) ? null : reader.GetInt32(0),
                NullableString(reader, 1), reader.GetString(2), reader.GetTimeSpan(3), reader.GetTimeSpan(4)));
            return values;
        }

        private static JsonObject HourNode(HourRow hour) => new()
        {
            ["gun"] = hour.Day,
            ["bas"] = hour.Start.ToString(@"hh\:mm"),
            ["bit"] = hour.End.ToString(@"hh\:mm")
        };

        private static void InsertHours(SqlConnection connection, SqlTransaction transaction, int? branchId, string? employeeId, JsonObject hour)
        {
            var day = GetString(hour, "gun");
            var start = TimeSpan.Parse(GetString(hour, "bas"));
            var end = TimeSpan.Parse(GetString(hour, "bit"));
            Execute(connection, transaction,
                "INSERT INTO CalismaSaatleri (SubeId, CalisanKimlik, Gun, Baslangic, Bitis) VALUES (@subeId, @calisan, @gun, @bas, @bit);",
                ("@subeId", SqlDbType.Int, 0, branchId),
                ("@calisan", SqlDbType.NVarChar, 50, employeeId),
                ("@gun", SqlDbType.NVarChar, 20, day),
                ("@bas", SqlDbType.Time, 0, start),
                ("@bit", SqlDbType.Time, 0, end));
        }

        private static int ExecuteScalarInt(SqlConnection connection, SqlTransaction transaction, string sql, params (string Name, SqlDbType Type, int Size, object? Value)[] parameters)
        {
            using var command = new SqlCommand(sql, connection, transaction);
            AddParameters(command, parameters);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static void Execute(SqlConnection connection, SqlTransaction transaction, string sql, params (string Name, SqlDbType Type, int Size, object? Value)[] parameters)
        {
            using var command = new SqlCommand(sql, connection, transaction);
            AddParameters(command, parameters);
            command.ExecuteNonQuery();
        }

        private static void AddParameters(SqlCommand command, IEnumerable<(string Name, SqlDbType Type, int Size, object? Value)> parameters)
        {
            foreach (var (name, type, size, value) in parameters)
            {
                var parameter = size != 0 ? command.Parameters.Add(name, type, size) : command.Parameters.Add(name, type);
                parameter.Value = value ?? DBNull.Value;
            }
        }

        private static string GetString(JsonObject node, string property, string fallback = "") =>
            node[property]?.GetValue<string>() ?? fallback;

        private static string? GetNullableString(JsonObject node, string property) => node[property]?.GetValue<string>();
        private static string? NullableString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

        private sealed record UserRow(string Identity, string FirstName, string LastName, string Role, string Password,
            string? TcNo, string? Title, string? Specialty, int? BranchId);
        private sealed record InstitutionRow(int Id, string Name, string Type);
        private sealed record BranchRow(int Id, int InstitutionId, string Name, string Address);
        private sealed record ServiceRow(int BranchId, string Name, int Duration, decimal Price);
        private sealed record HourRow(int? BranchId, string? EmployeeId, string Day, TimeSpan Start, TimeSpan End);

        private sealed class OrganizationBranchComparer : IEqualityComparer<(string Kurum, string Sube)>
        {
            public bool Equals((string Kurum, string Sube) x, (string Kurum, string Sube) y) =>
                StringComparer.OrdinalIgnoreCase.Equals(x.Kurum, y.Kurum) && StringComparer.OrdinalIgnoreCase.Equals(x.Sube, y.Sube);
            public int GetHashCode((string Kurum, string Sube) value) => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Kurum), StringComparer.OrdinalIgnoreCase.GetHashCode(value.Sube));
        }
    }
}
