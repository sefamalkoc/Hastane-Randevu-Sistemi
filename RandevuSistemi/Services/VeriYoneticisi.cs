using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Globalization;
using RandevuSistemi.Models;

namespace RandevuSistemi.Services
{
    // JSON dosyasında randevu kaydı için kullanılan DTO
    internal class RandevuKayit
    {
        public int? Id              { get; set; }
        public string HastaKimlik   { get; set; } = "";
        public string CalisanKimlik { get; set; } = "";
        public string HizmetAdi     { get; set; } = "";
        public DateTime Baslangic   { get; set; }
        public string Durum         { get; set; } = "Beklemede";
    }

    // Çalışan ek bilgileri için yardımcı sınıf (tuple alan ismi kayıplarını önler)
    internal class CalisanEki
    {
        public string? Uzmanlik  { get; set; }
        public string? Unvan     { get; set; }
        public List<(DayOfWeek Gun, TimeSpan Bas, TimeSpan Bit)> Uygunluklar { get; } = new();
        public List<string> HizmetAdlari { get; } = new();
    }

    /// <summary>
    /// SQL Server varsa ana verileri SQL'den yükler; veri.json dosyasını yerel yedek olarak tutar.
    /// SQL bağlantısı yoksa JSON dosyalarıyla çalışır.
    /// </summary>
    public class VeriYoneticisi
    {
        // JSON serileştirme seçenekleri (Türkçe karakter desteği)
        private static readonly JsonSerializerOptions JOpts = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _veriYolu;
        private readonly string _randevuYolu;

        // Kullanıcı giriş kaydı: (kimlik, ad, soyad, sifre, rol, tcNo?)
        private readonly List<(string kimlik, string ad, string soyad,
                                string sifre, string rol, string? tcNo)> _kayitlar = new();

        // Çalışma zamanı nesne kayıt defterleri
        private readonly Dictionary<string, Kullanici> _kullanicilar = new();
        private readonly Dictionary<string, Calisan>   _calisanlar   = new();
        private readonly Dictionary<string, Hasta>     _hastalar     = new();

        public RandevuServisi? Servis   { get; private set; }
        public IReadOnlyList<Kurum> Kurumlar => _kurumListesi;
        private readonly List<Kurum> _kurumListesi = new();

        public string VeritabaniBilgisi { get; set; } = "JSON Dosyası (data/veri.json)";
        public bool SqlServerBagli { get; set; } = false;
        public string? SqlBaglantiDizesi { get; set; }
        private SqlRandevuDeposu? _sqlDepo;
        private SqlAnaVeriDeposu? _sqlAnaVeriDeposu;
        private readonly object _dosyaKilit = new();

        public VeriYoneticisi(string veriYolu, string randevuYolu)
        {
            _veriYolu    = veriYolu;
            _randevuYolu = randevuYolu;
        }

        // ─────────────────────────────── YÜKLEME ───────────────────────────────

        /// <summary>
        /// Ana verileri SQL Server'dan (veya yedek JSON'dan), randevuları SQL/JSON deposundan yükler.
        /// </summary>
        public void Yukle(SqlRandevuDeposu? sqlDepo = null, SqlAnaVeriDeposu? sqlAnaVeriDeposu = null)
        {
            _sqlDepo = sqlDepo;
            _sqlAnaVeriDeposu = sqlAnaVeriDeposu;
            Servis = new RandevuServisi(new BellekRandevuDeposu(), new KonsolBildirim());

            if (!File.Exists(_veriYolu))
                throw new FileNotFoundException("Veri dosyası bulunamadı: " + _veriYolu);

            var json = _sqlAnaVeriDeposu?.Oku();
            if (string.IsNullOrWhiteSpace(json))
                json = File.ReadAllText(_veriYolu, System.Text.Encoding.UTF8);
            if (_sqlAnaVeriDeposu != null)
            {
                _sqlAnaVeriDeposu.Kaydet(json);
                YerelDosyayaYaz(_veriYolu, json);
            }
            var root = JsonNode.Parse(json)!.AsObject();

            // Sıra önemli: önce çalışan ekleri → kullanıcılar → kurumlar → randevular
            var calisanEkleri = OkuCalisanEkleri(root["calisanlar"]?.AsArray());
            OkuKullanicilar(root["kullanicilar"]!.AsArray(), calisanEkleri);
            if (OkuKurumlar(root["kurumlar"]!.AsArray(), calisanEkleri))
            {
                // Önceki sürümlerde çalışma saati olmayan şubeler kaydedilmiş olabilir.
                // Varsayılan hafta içi mesaisini hem SQL'e hem yerel yedeğe kalıcı yaz.
                json = root.ToJsonString(JOpts);
                DosyayaYaz(_veriYolu, json);
            }

            if (SqlRandevulariniYukle())
                return;

            OkuRandevular();
            if (_sqlDepo != null)
            {
                foreach (var r in Servis.Tumu().ToList())
                    r.KimlikAyarla(SqlEkle(r));
                RandevulariKaydet();
            }
        }

        /// <summary>
        /// SQL'de randevu varsa onları belleğe alır ve JSON'u bu listeyle eşitler.
        /// Kayıt yoksa false döner; çağıran JSON'den yükleyip bir kez SQL'e yazar.
        /// </summary>
        private bool SqlRandevulariniYukle()
        {
            if (_sqlDepo == null) return false;
            try
            {
                _sqlDepo.TekrarlariTemizle();
                var satirlar = _sqlDepo.Oku();
                if (satirlar.Count == 0) return false;

                foreach (var s in satirlar)
                    RandevuSatiriniYukle(s);

                RandevulariKaydet();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SQL randevuları okunamadı, JSON kullanılacak: {ex.Message}");
                _sqlDepo = null;
                VeritabaniBilgisi = "Ana veriler SQL Server'da; randevular JSON yedeğinde (SQL okuma hatası)";
                return false;
            }
        }

        private void RandevuSatiriniYukle(RandevuSatiri s)
        {
            if (!_hastalar.TryGetValue(s.HastaKimlik, out var hasta)) return;
            if (!_calisanlar.TryGetValue(s.CalisanKimlik, out var calisan)) return;
            var hizmet = calisan.Hizmetler.FirstOrDefault(h => h.Ad == s.HizmetAdi);
            if (hizmet == null) return;

            var randevu = new Randevu(hasta, calisan, hizmet, s.Baslangic, s.Id);
            if (Enum.TryParse<RandevuDurumu>(s.Durum, out var durum) && durum != RandevuDurumu.Beklemede)
                randevu.DurumAyarla(durum);
            Servis!.GeriYukle(randevu);
        }

        private int SqlEkle(Randevu r)
        {
            if (_sqlDepo == null) return r.Id;
            return _sqlDepo.Ekle(
                KimlikBulHasta(r.Hasta) ?? "",
                KimlikBulCalisan(r.Calisan) ?? "",
                r.Hizmet.Ad,
                r.Baslangic,
                r.Bitis,
                r.Ucret,
                r.Durum.ToString());
        }

        private void SqlDurumGuncelle(Randevu r)
        {
            _sqlDepo?.DurumGuncelle(r.Id, r.Durum.ToString());
        }

        // ── Çalışanların ek bilgileri (uzmanlık, uygunluk, hizmetler) ──
        private static Dictionary<string, CalisanEki> OkuCalisanEkleri(JsonArray? arr)
        {
            var dict = new Dictionary<string, CalisanEki>();
            if (arr == null) return dict;

            foreach (var node in arr)
            {
                var o      = node!.AsObject();
                var kimlik = o["kimlik"]!.GetValue<string>();
                var eki    = new CalisanEki
                {
                    Uzmanlik = o["uzmanlik"]?.GetValue<string>(),
                    Unvan    = o["unvan"]?.GetValue<string>()
                };

                foreach (var u in o["uygunluklar"]!.AsArray())
                {
                    var uo  = u!.AsObject();
                    var gun = Enum.Parse<DayOfWeek>(uo["gun"]!.GetValue<string>());
                    var bas = TimeSpan.Parse(uo["bas"]!.GetValue<string>());
                    var bit = TimeSpan.Parse(uo["bit"]!.GetValue<string>());
                    eki.Uygunluklar.Add((gun, bas, bit));
                }

                foreach (var h in o["hizmetAdlari"]!.AsArray())
                    eki.HizmetAdlari.Add(h!.GetValue<string>());

                dict[kimlik] = eki;
            }
            return dict;
        }

        // ── Kullanıcı nesneleri ──
        private void OkuKullanicilar(
            JsonArray arr,
            Dictionary<string, CalisanEki> ekleri)
        {
            var sifrelerGuncellenecek = false;
            foreach (var node in arr)
            {
                var o      = node!.AsObject();
                var kimlik = o["kimlik"]!.GetValue<string>();
                var ad     = o["ad"]!.GetValue<string>();
                var soyad  = o["soyad"]!.GetValue<string>();
                var rol    = o["rol"]!.GetValue<string>();
                var sifre  = o["sifre"]!.GetValue<string>();
                var tcNo   = o["tcNo"]?.GetValue<string>();
                if (!SifreKoruma.HashMi(sifre))
                {
                    sifre = SifreKoruma.Hashle(sifre);
                    sifrelerGuncellenecek = true;
                }

                _kayitlar.Add((kimlik, ad, soyad, sifre, rol, tcNo));

                ekleri.TryGetValue(kimlik, out var ek);

                Kullanici obj = rol switch
                {
                    "Hasta"    => new Hasta(ad, soyad, tcNo!),
                    "Doktor"   => new Doktor(ad, soyad, ek?.Uzmanlik ?? "Genel"),
                    "Personel" => new SaglikPersoneli(ad, soyad, ek?.Unvan ?? "Personel"),
                    "Yonetici" => new Yonetici(ad, soyad),
                    _ => throw new Exception("Bilinmeyen rol: " + rol)
                };

                _kullanicilar[kimlik] = obj;
                if (obj is Hasta   h) _hastalar[kimlik]  = h;
                if (obj is Calisan c) _calisanlar[kimlik] = c;
            }

            if (sifrelerGuncellenecek)
                KullanicilariKaydet();
        }

        // ── Kurum / Şube nesneleri ──
        private bool OkuKurumlar(
            JsonArray arr,
            Dictionary<string, CalisanEki> ekleri)
        {
            bool calismaSaatiDuzeltildi = false;
            foreach (var knode in arr)
            {
                var ko  = knode!.AsObject();
                var ad  = ko["ad"]!.GetValue<string>();
                var tur = ko["tur"]!.GetValue<string>();

                Kurum kurum = tur == "Hastane" ? new Hastane(ad) : new Klinik(ad);
                _kurumListesi.Add(kurum);

                foreach (var snode in ko["subeler"]!.AsArray())
                {
                    var so       = snode!.AsObject();
                    var subAd    = so["ad"]!.GetValue<string>();
                    var subAdres = so["adres"]!.GetValue<string>();

                    var sube = kurum.SubeEkle(subAd, subAdres);

                    // Çalışma saatleri. Boş çalışma takvimi eski kayıt hatasıdır;
                    // yeni şubelerdeki varsayılan hafta içi mesaisini uygula.
                    var saatler = so["calismaSaatleri"]?.AsArray();
                    if (saatler == null)
                    {
                        saatler = new JsonArray();
                        so["calismaSaatleri"] = saatler;
                    }
                    if (saatler.Count == 0)
                    {
                        foreach (var gun in new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" })
                        {
                            saatler.Add(new JsonObject
                            {
                                ["gun"] = JsonValue.Create(gun),
                                ["bas"] = JsonValue.Create("08:00"),
                                ["bit"] = JsonValue.Create("18:00")
                            });
                        }
                        calismaSaatiDuzeltildi = true;
                    }

                    foreach (var cs in saatler)
                    {
                        var cso = cs!.AsObject();
                        sube.CalismaSaatiEkle(new CalismaSaati(
                            Enum.Parse<DayOfWeek>(cso["gun"]!.GetValue<string>()),
                            TimeSpan.Parse(cso["bas"]!.GetValue<string>()),
                            TimeSpan.Parse(cso["bit"]!.GetValue<string>())));
                    }

                    // Bölümler
                    foreach (var b in so["bolumler"]!.AsArray())
                        sube.BolumEkle(b!.GetValue<string>());

                    // Hizmetler
                    var hizmetSozlugu = new Dictionary<string, Hizmet>();
                    foreach (var hn in so["hizmetler"]!.AsArray())
                    {
                        var ho = hn!.AsObject();
                        var hiz = sube.HizmetEkle(
                            ho["ad"]!.GetValue<string>(),
                            ho["sure"]!.GetValue<int>(),
                            ho["ucret"]!.GetValue<decimal>());
                        hizmetSozlugu[hiz.Ad] = hiz;
                    }

                    // Çalışanları şubeye bağla + uygunluk & hizmetlerini ata
                    foreach (var cknNode in so["calisanKimlikleri"]!.AsArray())
                    {
                        var ck = cknNode!.GetValue<string>();
                        if (!_calisanlar.TryGetValue(ck, out var calisan)) continue;
                        sube.CalisanEkle(calisan);

                        if (!ekleri.TryGetValue(ck, out var ek)) continue;

                        foreach (var hAd in ek.HizmetAdlari)
                            if (hizmetSozlugu.TryGetValue(hAd, out var hiz))
                                calisan.HizmetEkle(hiz);

                        foreach (var (gun, bas, bit) in ek.Uygunluklar)
                            calisan.UygunlukEkle(new CalismaSaati(gun, bas, bit));
                    }
                }
            }
            return calismaSaatiDuzeltildi;
        }

        // ── Randevuları yükle ──
        private void OkuRandevular()
        {
            if (!File.Exists(_randevuYolu)) return;
            var raw = File.ReadAllText(_randevuYolu, System.Text.Encoding.UTF8).Trim();
            if (raw is "[]" or "") return;

            var liste = JsonSerializer.Deserialize<List<RandevuKayit>>(raw, JOpts) ?? new();
            foreach (var kayit in liste)
            {
                if (!_hastalar.TryGetValue(kayit.HastaKimlik,     out var hasta))   continue;
                if (!_calisanlar.TryGetValue(kayit.CalisanKimlik, out var calisan)) continue;

                var hizmet = calisan.Hizmetler.FirstOrDefault(h => h.Ad == kayit.HizmetAdi);
                if (hizmet == null) continue;

                var randevu = new Randevu(hasta, calisan, hizmet, kayit.Baslangic, kayit.Id);
                var durum   = Enum.Parse<RandevuDurumu>(kayit.Durum);
                if (durum != RandevuDurumu.Beklemede) randevu.DurumAyarla(durum);
                Servis!.GeriYukle(randevu);
            }
        }

        // ─────────────────────────── KAYDETME ───────────────────────────────────

        /// <summary>
        /// Bellekteki randevuları JSON'a yazar. Yeni kayıt veya durum değişiminde SQL kopyasını da günceller.
        /// </summary>
        public void RandevulariKaydet(Randevu? degisen = null, bool yeniKayit = false)
        {
            lock (_dosyaKilit)
            {
                if (degisen != null && _sqlDepo != null)
                {
                    try
                    {
                        if (yeniKayit) degisen.KimlikAyarla(SqlEkle(degisen));
                        else SqlDurumGuncelle(degisen);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"SQL randevu yazılamadı: {ex.Message}");
                        _sqlDepo = null;
                        VeritabaniBilgisi = "Ana veriler SQL Server'da; randevular JSON yedeğinde (SQL yazma hatası)";
                    }
                }

                var liste = Servis!.Tumu().Select(r => new RandevuKayit
                {
                    Id            = r.Id,
                    HastaKimlik   = _hastalar.FirstOrDefault(kvp   => kvp.Value == r.Hasta).Key   ?? "",
                    CalisanKimlik = _calisanlar.FirstOrDefault(kvp => kvp.Value == r.Calisan).Key ?? "",
                    HizmetAdi     = r.Hizmet.Ad,
                    Baslangic     = r.Baslangic,
                    Durum         = r.Durum.ToString()
                }).ToList();

                DosyayaYaz(_randevuYolu, JsonSerializer.Serialize(liste, JOpts));
            }
        }

        // ─────────────────────────── KULLANICI İŞLEMLERİ ────────────────────────

        /// <summary>Kimlik ve şifre doğrulayarak kullanıcı nesnesini döner.</summary>
        public Kullanici? GirisYap(string kimlik, string sifre)
        {
            lock (_dosyaKilit)
            {
                var index = _kayitlar.FindIndex(k => string.Equals(k.kimlik, kimlik, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    // Hasta kayıtlarında kullanılan TC numarası da giriş kimliği olarak kabul edilir.
                    index = _kayitlar.FindIndex(k => k.rol == "Hasta" &&
                        !string.IsNullOrWhiteSpace(k.tcNo) &&
                        string.Equals(k.tcNo, kimlik, StringComparison.Ordinal));
                }
                if (index < 0)
                {
                    // Personel hesapları için kayıt sırasında üretilen kimlik bilinmiyorsa
                    // tam ad + soyad da giriş adı olarak kullanılabilir.
                    var turkce = CultureInfo.GetCultureInfo("tr-TR");
                    static string GirisAdiNormallestir(string deger, CultureInfo kultur) =>
                        string.Join(" ", deger.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                            .Normalize(NormalizationForm.FormKC)
                            .ToLower(kultur);
                    var girisAdi = GirisAdiNormallestir(kimlik, turkce);
                    var eslesenPersonel = _calisanlar
                        .Where(x => GirisAdiNormallestir(x.Value.AdSoyad, turkce) == girisAdi)
                        .Select(x => x.Key)
                        .Take(2)
                        .ToArray();
                    if (eslesenPersonel.Length == 1)
                        index = _kayitlar.FindIndex(k => k.kimlik == eslesenPersonel[0]);
                }
                if (index < 0) return null;

                var kayit = _kayitlar[index];
                if (!SifreKoruma.Dogrula(sifre, kayit.sifre, out var guncelHash)) return null;
                if (guncelHash != null)
                {
                    _kayitlar[index] = (kayit.kimlik, kayit.ad, kayit.soyad, guncelHash, kayit.rol, kayit.tcNo);
                    KullanicilariKaydet();
                }
                return _kullanicilar.GetValueOrDefault(kayit.kimlik);
            }
        }

        /// <summary>
        /// Yeni hasta kaydeder; veri.json güncellenir.
        /// TC No zaten kayıtlıysa null döner.
        /// </summary>
        public Hasta? HastaKaydet(string ad, string soyad, string tcNo, string sifre)
        {
            lock (_dosyaKilit)
            {
            if (_kayitlar.Any(k => k.tcNo == tcNo)) return null;

            // Benzersiz kimlik üret
            var kimlik = "yh" + Math.Abs(DateTime.Now.Ticks % 999_999L);
            while (_kullanicilar.ContainsKey(kimlik))
                kimlik = "yh" + Math.Abs(DateTime.Now.Ticks % 999_999L);

            var hasta = new Hasta(ad, soyad, tcNo);
            _kayitlar.Add((kimlik, ad, soyad, SifreKoruma.Hashle(sifre), "Hasta", tcNo));
            _kullanicilar[kimlik] = hasta;
            _hastalar[kimlik]     = hasta;

            KullanicilariKaydet();
            return hasta;
            }
        }

        // Kullanıcı listesini veri.json'da günceller (geri kalanı korur)
        private void KullanicilariKaydet()
        {
            lock (_dosyaKilit)
            {
            var json = File.ReadAllText(_veriYolu, System.Text.Encoding.UTF8);
            var root = JsonNode.Parse(json)!.AsObject();
            var kArr = new JsonArray();

            foreach (var (kimlik, ad, soyad, sifre, rol, tcNo) in _kayitlar)
            {
                var o = new JsonObject
                {
                    ["kimlik"] = JsonValue.Create(kimlik),
                    ["ad"]     = JsonValue.Create(ad),
                    ["soyad"]  = JsonValue.Create(soyad),
                    ["rol"]    = JsonValue.Create(rol),
                    ["sifre"]  = JsonValue.Create(sifre)
                };
                if (tcNo != null) o["tcNo"] = JsonValue.Create(tcNo);
                kArr.Add(o);
            }

            root["kullanicilar"] = kArr;
            DosyayaYaz(_veriYolu, root.ToJsonString(JOpts));
            }
        }

        // ─────────────────────── YARDIMCI ARAMA ─────────────────────────────────

        public Kullanici? KullaniciBul(string kimlik) => _kullanicilar.GetValueOrDefault(kimlik);
        public Calisan? CalisanBul(string kimlik)     => _calisanlar.GetValueOrDefault(kimlik);
        public Hasta? HastaBul(string kimlik)         => _hastalar.GetValueOrDefault(kimlik);

        public string? KimlikBulHasta(Hasta h) =>
            _hastalar.FirstOrDefault(kvp => kvp.Value == h).Key;

        public string? KimlikBulCalisan(Calisan c) =>
            _calisanlar.FirstOrDefault(kvp => kvp.Value == c).Key;

        public string? KimlikBulKullanici(Kullanici k) =>
            _kullanicilar.FirstOrDefault(kvp => kvp.Value == k).Key;

        public IReadOnlyDictionary<string, Kullanici> Kullanicilar => _kullanicilar;
        public IReadOnlyDictionary<string, Calisan> Calisanlar     => _calisanlar;
        public IReadOnlyDictionary<string, Hasta> Hastalar         => _hastalar;

        // ─────────────────────── YÖNETİCİ: YENİ VERİ EKLEME (ADMIN) ──────────────

        public (Kurum Kurum, Sube Sube) KurumVeSubeGetirVeyaOlustur(string kurumAd, string subeAd, string? subeAdres = null, string? kurumTuru = null)
        {
            lock (_dosyaKilit)
            {
            if (string.IsNullOrWhiteSpace(kurumAd)) kurumAd = "Merkez Hastanesi";
            if (string.IsNullOrWhiteSpace(subeAd)) subeAd = "Ana Şube";

            var kurum = _kurumListesi.FirstOrDefault(k => k.Ad.Equals(kurumAd.Trim(), StringComparison.OrdinalIgnoreCase));
            if (kurum == null)
            {
                string tur;
                if (string.IsNullOrWhiteSpace(kurumTuru))
                    tur = kurumAd.Contains("Klinik", StringComparison.OrdinalIgnoreCase) ? "Klinik" : "Hastane";
                else if (kurumTuru.Equals("Klinik", StringComparison.OrdinalIgnoreCase))
                    tur = "Klinik";
                else if (kurumTuru.Equals("Hastane", StringComparison.OrdinalIgnoreCase))
                    tur = "Hastane";
                else
                    throw new ArgumentException("Kurum türü Hastane veya Klinik olmalıdır.");
                kurum = tur == "Klinik" ? new Klinik(kurumAd.Trim()) : new Hastane(kurumAd.Trim());
                _kurumListesi.Add(kurum);

                // JSON'a kurum ekle
                try
                {
                    var json = File.ReadAllText(_veriYolu, System.Text.Encoding.UTF8);
                    var root = JsonNode.Parse(json)!.AsObject();
                    var kArr = root["kurumlar"]?.AsArray();
                    if (kArr != null)
                    {
                        var kObj = new JsonObject
                        {
                            ["kimlik"] = JsonValue.Create("k" + (_kurumListesi.Count)),
                            ["ad"] = JsonValue.Create(kurum.Ad),
                            ["tur"] = JsonValue.Create(kurum.Tur),
                            ["subeler"] = new JsonArray()
                        };
                        kArr.Add(kObj);
                        DosyayaYaz(_veriYolu, root.ToJsonString(JOpts));
                    }
                }
                catch (Exception ex) { throw new IOException("Kurum bilgisi kaydedilemedi.", ex); }

                // SQL Server'a kurum ekle
                if (_sqlAnaVeriDeposu == null && !string.IsNullOrWhiteSpace(SqlBaglantiDizesi))
                {
                    try
                    {
                        using var conn = new Microsoft.Data.SqlClient.SqlConnection(SqlBaglantiDizesi);
                        conn.Open();
                        const string sqlK = "IF NOT EXISTS (SELECT 1 FROM Kurumlar WHERE Ad = @ad) INSERT INTO Kurumlar (Ad, Tur) VALUES (@ad, @tur);";
                        using var cmdK = new Microsoft.Data.SqlClient.SqlCommand(sqlK, conn);
                        cmdK.Parameters.AddWithValue("@ad", kurum.Ad);
                        cmdK.Parameters.AddWithValue("@tur", kurum.Tur);
                        cmdK.ExecuteNonQuery();
                    }
                    catch { }
                }
            }

            var sube = kurum.Subeler.FirstOrDefault(s => s.Ad.Equals(subeAd.Trim(), StringComparison.OrdinalIgnoreCase));
            if (sube == null)
            {
                string adres = !string.IsNullOrWhiteSpace(subeAdres) ? subeAdres : $"{subeAd.Trim()}, Merkez";
                sube = kurum.SubeEkle(subeAd.Trim(), adres);

                // Varsayılan çalışma saatleri ekle
                sube.CalismaSaatiEkle(new CalismaSaati(DayOfWeek.Monday, TimeSpan.Parse("08:00"), TimeSpan.Parse("18:00")));
                sube.CalismaSaatiEkle(new CalismaSaati(DayOfWeek.Tuesday, TimeSpan.Parse("08:00"), TimeSpan.Parse("18:00")));
                sube.CalismaSaatiEkle(new CalismaSaati(DayOfWeek.Wednesday, TimeSpan.Parse("08:00"), TimeSpan.Parse("18:00")));
                sube.CalismaSaatiEkle(new CalismaSaati(DayOfWeek.Thursday, TimeSpan.Parse("08:00"), TimeSpan.Parse("18:00")));
                sube.CalismaSaatiEkle(new CalismaSaati(DayOfWeek.Friday, TimeSpan.Parse("08:00"), TimeSpan.Parse("18:00")));

                // JSON'a şube ekle
                try
                {
                    var json = File.ReadAllText(_veriYolu, System.Text.Encoding.UTF8);
                    var root = JsonNode.Parse(json)!.AsObject();
                    var kArr = root["kurumlar"]?.AsArray();
                    if (kArr != null)
                    {
                        foreach (var kNode in kArr)
                        {
                            var ko = kNode!.AsObject();
                            if (ko["ad"]?.GetValue<string>() == kurum.Ad)
                            {
                                var sArr = ko["subeler"]?.AsArray();
                                if (sArr != null)
                                {
                                    var csArr = new JsonArray();
                                    string[] gunler = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
                                    foreach (var g in gunler)
                                    {
                                        csArr.Add(new JsonObject { ["gun"] = JsonValue.Create(g), ["bas"] = JsonValue.Create("08:00"), ["bit"] = JsonValue.Create("18:00") });
                                    }

                                    var sObj = new JsonObject
                                    {
                                        ["kimlik"] = JsonValue.Create("sb" + (DateTime.Now.Ticks % 9999)),
                                        ["ad"] = JsonValue.Create(sube.Ad),
                                        ["adres"] = JsonValue.Create(sube.Adres),
                                        ["calismaSaatleri"] = csArr,
                                        ["bolumler"] = new JsonArray(),
                                        ["hizmetler"] = new JsonArray(),
                                        ["calisanKimlikleri"] = new JsonArray()
                                    };
                                    sArr.Add(sObj);
                                }
                                break;
                            }
                        }
                        DosyayaYaz(_veriYolu, root.ToJsonString(JOpts));
                    }
                }
                catch (Exception ex) { throw new IOException("Şube bilgisi kaydedilemedi.", ex); }

                // SQL Server'a şube ekle
                if (_sqlAnaVeriDeposu == null && !string.IsNullOrWhiteSpace(SqlBaglantiDizesi))
                {
                    try
                    {
                        using var conn = new Microsoft.Data.SqlClient.SqlConnection(SqlBaglantiDizesi);
                        conn.Open();
                        const string sqlS = @"
                            IF NOT EXISTS (SELECT 1 FROM Subeler WHERE Ad = @sAd AND KurumId = (SELECT Id FROM Kurumlar WHERE Ad = @kAd))
                            INSERT INTO Subeler (KurumId, Ad, Adres)
                            SELECT Id, @sAd, @adres FROM Kurumlar WHERE Ad = @kAd;";
                        using var cmdS = new Microsoft.Data.SqlClient.SqlCommand(sqlS, conn);
                        cmdS.Parameters.AddWithValue("@kAd", kurum.Ad);
                        cmdS.Parameters.AddWithValue("@sAd", sube.Ad);
                        cmdS.Parameters.AddWithValue("@adres", adres);
                        cmdS.ExecuteNonQuery();
                    }
                    catch { }
                }
            }

            return (kurum, sube);
            }
        }

        public void SubeCalismaSaatleriniGuncelle(
            string kurumAd,
            string subeAd,
            IReadOnlyCollection<(DayOfWeek Gun, TimeSpan Baslangic, TimeSpan Bitis)> saatler)
        {
            lock (_dosyaKilit)
            {
                var kurum = _kurumListesi.FirstOrDefault(k => k.Ad.Equals(kurumAd, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException("Kurum bulunamadı.");
                var sube = kurum.Subeler.FirstOrDefault(s => s.Ad.Equals(subeAd, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException("Şube bulunamadı.");

                var json = File.ReadAllText(_veriYolu, System.Text.Encoding.UTF8);
                var root = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("Kurum verisi okunamadı.");
                var kurumNode = root["kurumlar"]?.AsArray()?.FirstOrDefault(node =>
                    string.Equals(node?["ad"]?.GetValue<string>(), kurum.Ad, StringComparison.OrdinalIgnoreCase))?.AsObject()
                    ?? throw new InvalidDataException("Kurum kaydı dosyada bulunamadı.");
                var subeNode = kurumNode["subeler"]?.AsArray()?.FirstOrDefault(node =>
                    string.Equals(node?["ad"]?.GetValue<string>(), sube.Ad, StringComparison.OrdinalIgnoreCase))?.AsObject()
                    ?? throw new InvalidDataException("Şube kaydı dosyada bulunamadı.");

                var saatDizisi = new JsonArray();
                foreach (var saat in saatler.OrderBy(s => (int)s.Gun))
                {
                    saatDizisi.Add(new JsonObject
                    {
                        ["gun"] = JsonValue.Create(saat.Gun.ToString()),
                        ["bas"] = JsonValue.Create(saat.Baslangic.ToString(@"hh\:mm")),
                        ["bit"] = JsonValue.Create(saat.Bitis.ToString(@"hh\:mm"))
                    });
                }
                subeNode["calismaSaatleri"] = saatDizisi;

                // SQL ana veri deposu ve yerel JSON yedeği aynı işlemle güncellenir.
                DosyayaYaz(_veriYolu, root.ToJsonString(JOpts));
                sube.CalismaSaatleriniDegistir(saatler
                    .OrderBy(s => (int)s.Gun)
                    .Select(s => new CalismaSaati(s.Gun, s.Baslangic, s.Bitis)));
            }
        }

        public Hizmet HizmetEkle(string kurumAd, string subeAd, string hizmetAd, int sureDakika, decimal ucret, string? bolumAd = null)
        {
            lock (_dosyaKilit)
            {
            var (kurum, sube) = KurumVeSubeGetirVeyaOlustur(kurumAd, subeAd);

            if (sube.Hizmetler.Any(h => h.Ad.Equals(hizmetAd, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"'{hizmetAd}' hizmeti bu şubede zaten mevcut.");

            var yeniHizmet = sube.HizmetEkle(hizmetAd, sureDakika, ucret);

            if (!string.IsNullOrWhiteSpace(bolumAd) && !sube.Bolumler.Any(b => b.Ad.Equals(bolumAd, StringComparison.OrdinalIgnoreCase)))
            {
                sube.BolumEkle(bolumAd);
            }

            // JSON Güncelleme
            try
            {
                var json = File.ReadAllText(_veriYolu, System.Text.Encoding.UTF8);
                var root = JsonNode.Parse(json)!.AsObject();
                var kArr = root["kurumlar"]?.AsArray();
                if (kArr != null)
                {
                    foreach (var kNode in kArr)
                    {
                        var ko = kNode!.AsObject();
                        if (ko["ad"]?.GetValue<string>() == kurum.Ad)
                        {
                            var sArr = ko["subeler"]?.AsArray();
                            if (sArr != null)
                            {
                                foreach (var sNode in sArr)
                                {
                                    var so = sNode!.AsObject();
                                    if (so["ad"]?.GetValue<string>() == sube.Ad)
                                    {
                                        var hList = so["hizmetler"]?.AsArray();
                                        if (hList != null)
                                        {
                                            hList.Add(new JsonObject
                                            {
                                                ["ad"] = JsonValue.Create(hizmetAd),
                                                ["sure"] = JsonValue.Create(sureDakika),
                                                ["ucret"] = JsonValue.Create(ucret)
                                            });
                                        }

                                        if (!string.IsNullOrWhiteSpace(bolumAd))
                                        {
                                            var bList = so["bolumler"]?.AsArray();
                                            if (bList != null && !bList.Any(b => b?.GetValue<string>() == bolumAd))
                                            {
                                                bList.Add(JsonValue.Create(bolumAd));
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                            break;
                        }
                    }
                    DosyayaYaz(_veriYolu, root.ToJsonString(JOpts));
                }
            }
            catch (Exception ex) { throw new IOException("Hizmet bilgisi kaydedilemedi.", ex); }

            // SQL Server Güncelleme (Eğer bağlıysa)
            if (_sqlAnaVeriDeposu == null && !string.IsNullOrWhiteSpace(SqlBaglantiDizesi))
            {
                try
                {
                    using var conn = new Microsoft.Data.SqlClient.SqlConnection(SqlBaglantiDizesi);
                    conn.Open();
                    const string sql = @"
                        INSERT INTO Hizmetler (SubeId, Ad, SureDakika, Ucret)
                        SELECT Id, @ad, @sure, @ucret FROM Subeler WHERE Ad = @subeAd;";
                    using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@ad", hizmetAd);
                    cmd.Parameters.AddWithValue("@sure", sureDakika);
                    cmd.Parameters.AddWithValue("@ucret", ucret);
                    cmd.Parameters.AddWithValue("@subeAd", sube.Ad);
                    cmd.ExecuteNonQuery();

                    if (!string.IsNullOrWhiteSpace(bolumAd))
                    {
                        const string bSql = @"
                            IF NOT EXISTS (SELECT 1 FROM Bolumler WHERE Ad = @bAd AND SubeId = (SELECT Id FROM Subeler WHERE Ad = @subeAd))
                            INSERT INTO Bolumler (SubeId, Ad) SELECT Id, @bAd FROM Subeler WHERE Ad = @subeAd;";
                        using var bCmd = new Microsoft.Data.SqlClient.SqlCommand(bSql, conn);
                        bCmd.Parameters.AddWithValue("@bAd", bolumAd);
                        bCmd.Parameters.AddWithValue("@subeAd", sube.Ad);
                        bCmd.ExecuteNonQuery();
                    }
                }
                catch { }
            }

            return yeniHizmet;
            }
        }

        public Calisan DoktorEkle(
            string kurumAd, 
            string subeAd, 
            string ad, 
            string soyad, 
            string rol, 
            string uzmanlikVeyaUnvan, 
            string sifre, 
            List<string> hizmetAdlari, 
            List<(DayOfWeek Gun, TimeSpan Bas, TimeSpan Bit)> uygunluklar)
        {
            lock (_dosyaKilit)
            {
            var (kurum, sube) = KurumVeSubeGetirVeyaOlustur(kurumAd, subeAd);

            // Benzersiz kimlik oluştur (örn. d4, d5 veya p2, p3)
            string prefix = rol.Equals("Doktor", StringComparison.OrdinalIgnoreCase) ? "d" : "p";
            int sayi = 1;
            while (_kullanicilar.ContainsKey($"{prefix}{sayi}")) sayi++;
            string kimlik = $"{prefix}{sayi}";

            Calisan calisan;
            if (rol.Equals("Doktor", StringComparison.OrdinalIgnoreCase))
            {
                calisan = new Doktor(ad, soyad, uzmanlikVeyaUnvan);
            }
            else
            {
                calisan = new SaglikPersoneli(ad, soyad, uzmanlikVeyaUnvan);
            }

            // Şubeye ata
            sube.CalisanEkle(calisan);

            // Hizmetleri ekle (şubede yoksa şubeye de ekler)
            foreach (var hAd in hizmetAdlari)
            {
                if (string.IsNullOrWhiteSpace(hAd)) continue;
                var h = sube.Hizmetler.FirstOrDefault(x => x.Ad.Equals(hAd, StringComparison.OrdinalIgnoreCase));
                if (h == null)
                {
                    h = sube.HizmetEkle(hAd, 30, 500.0m);
                }
                calisan.HizmetEkle(h);
            }

            // Uzmanlığı şube polikliniklerine ekle
            if (!string.IsNullOrWhiteSpace(uzmanlikVeyaUnvan) && !sube.Bolumler.Any(b => b.Ad.Equals(uzmanlikVeyaUnvan, StringComparison.OrdinalIgnoreCase)))
            {
                sube.BolumEkle(uzmanlikVeyaUnvan);
            }

            // Uygunlukları ekle
            foreach (var (gun, bas, bit) in uygunluklar)
            {
                calisan.UygunlukEkle(new CalismaSaati(gun, bas, bit));
            }

            // Kayıt defterlerine ekle
            _kullanicilar[kimlik] = calisan;
            _calisanlar[kimlik] = calisan;
            sifre = SifreKoruma.Hashle(sifre);
            _kayitlar.Add((kimlik, ad, soyad, sifre, rol, null));

            // JSON Güncelleme
            try
            {
                var json = File.ReadAllText(_veriYolu, System.Text.Encoding.UTF8);
                var root = JsonNode.Parse(json)!.AsObject();

                // 1. Kullanıcılar
                var kArr = root["kullanicilar"]?.AsArray();
                if (kArr != null)
                {
                    kArr.Add(new JsonObject
                    {
                        ["kimlik"] = JsonValue.Create(kimlik),
                        ["ad"] = JsonValue.Create(ad),
                        ["soyad"] = JsonValue.Create(soyad),
                        ["rol"] = JsonValue.Create(rol),
                        ["sifre"] = JsonValue.Create(sifre)
                    });
                }

                // 2. Çalışanlar
                var cArr = root["calisanlar"]?.AsArray();
                if (cArr != null)
                {
                    var uygArr = new JsonArray();
                    foreach (var (gun, bas, bit) in uygunluklar)
                    {
                        uygArr.Add(new JsonObject
                        {
                            ["gun"] = JsonValue.Create(gun.ToString()),
                            ["bas"] = JsonValue.Create(bas.ToString(@"hh\:mm")),
                            ["bit"] = JsonValue.Create(bit.ToString(@"hh\:mm"))
                        });
                    }

                    var hizArr = new JsonArray();
                    foreach (var h in hizmetAdlari) hizArr.Add(JsonValue.Create(h));

                    var cObj = new JsonObject
                    {
                        ["kimlik"] = JsonValue.Create(kimlik),
                        ["uygunluklar"] = uygArr,
                        ["hizmetAdlari"] = hizArr
                    };
                    if (rol.Equals("Doktor", StringComparison.OrdinalIgnoreCase))
                        cObj["uzmanlik"] = JsonValue.Create(uzmanlikVeyaUnvan);
                    else
                        cObj["unvan"] = JsonValue.Create(uzmanlikVeyaUnvan);

                    cArr.Add(cObj);
                }

                // 3. Şubedeki çalışan kimlikleri
                var kurArr = root["kurumlar"]?.AsArray();
                if (kurArr != null)
                {
                    foreach (var kNode in kurArr)
                    {
                        var ko = kNode!.AsObject();
                        if (ko["ad"]?.GetValue<string>() == kurum.Ad)
                        {
                            var sArr = ko["subeler"]?.AsArray();
                            if (sArr != null)
                            {
                                foreach (var sNode in sArr)
                                {
                                    var so = sNode!.AsObject();
                                    if (so["ad"]?.GetValue<string>() == sube.Ad)
                                    {
                                        so["calisanKimlikleri"]?.AsArray().Add(JsonValue.Create(kimlik));
                                        break;
                                    }
                                }
                            }
                            break;
                        }
                    }
                }

                DosyayaYaz(_veriYolu, root.ToJsonString(JOpts));
            }
            catch (Exception ex) { throw new IOException("Çalışan bilgisi kaydedilemedi.", ex); }

            // SQL Server Güncelleme (Eğer bağlıysa)
            if (_sqlAnaVeriDeposu == null && !string.IsNullOrWhiteSpace(SqlBaglantiDizesi))
            {
                try
                {
                    using var conn = new Microsoft.Data.SqlClient.SqlConnection(SqlBaglantiDizesi);
                    conn.Open();

                    const string sqlK = @"
                        INSERT INTO Kullanicilar (Kimlik, Ad, Soyad, Rol, Sifre, Unvan, Uzmanlik, SubeId)
                        SELECT @kimlik, @ad, @soyad, @rol, @sifre, @unvan, @uzmanlik, Id FROM Subeler WHERE Ad = @subeAd;";
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(sqlK, conn))
                    {
                        cmd.Parameters.AddWithValue("@kimlik", kimlik);
                        cmd.Parameters.AddWithValue("@ad", ad);
                        cmd.Parameters.AddWithValue("@soyad", soyad);
                        cmd.Parameters.AddWithValue("@rol", rol);
                        cmd.Parameters.AddWithValue("@sifre", sifre);
                        cmd.Parameters.AddWithValue("@unvan", rol.Equals("Doktor", StringComparison.OrdinalIgnoreCase) ? (object)DBNull.Value : uzmanlikVeyaUnvan);
                        cmd.Parameters.AddWithValue("@uzmanlik", rol.Equals("Doktor", StringComparison.OrdinalIgnoreCase) ? (object)uzmanlikVeyaUnvan : DBNull.Value);
                        cmd.Parameters.AddWithValue("@subeAd", sube.Ad);
                        cmd.ExecuteNonQuery();
                    }

                    foreach (var hAd in hizmetAdlari)
                    {
                        const string sqlH = "INSERT INTO CalisanHizmetler (CalisanKimlik, HizmetAdi) VALUES (@kimlik, @hAd);";
                        using var cmdH = new Microsoft.Data.SqlClient.SqlCommand(sqlH, conn);
                        cmdH.Parameters.AddWithValue("@kimlik", kimlik);
                        cmdH.Parameters.AddWithValue("@hAd", hAd);
                        cmdH.ExecuteNonQuery();
                    }

                    foreach (var (gun, bas, bit) in uygunluklar)
                    {
                        const string sqlU = "INSERT INTO CalismaSaatleri (CalisanKimlik, Gun, Baslangic, Bitis) VALUES (@kimlik, @gun, @bas, @bit);";
                        using var cmdU = new Microsoft.Data.SqlClient.SqlCommand(sqlU, conn);
                        cmdU.Parameters.AddWithValue("@kimlik", kimlik);
                        cmdU.Parameters.AddWithValue("@gun", gun.ToString());
                        cmdU.Parameters.AddWithValue("@bas", bas);
                        cmdU.Parameters.AddWithValue("@bit", bit);
                        cmdU.ExecuteNonQuery();
                    }
                }
                catch { }
            }

            return calisan;
            }
        }

        public Bolum BolumEkle(string kurumAd, string subeAd, string bolumAd)
        {
            lock (_dosyaKilit)
            {
            var (kurum, sube) = KurumVeSubeGetirVeyaOlustur(kurumAd, subeAd);

            if (sube.Bolumler.Any(b => b.Ad.Equals(bolumAd, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"'{bolumAd}' bölümü bu şubede zaten mevcut.");

            var yeniBolum = sube.BolumEkle(bolumAd);

            // JSON Güncelleme
            try
            {
                var json = File.ReadAllText(_veriYolu, System.Text.Encoding.UTF8);
                var root = JsonNode.Parse(json)!.AsObject();
                var kArr = root["kurumlar"]?.AsArray();
                if (kArr != null)
                {
                    foreach (var kNode in kArr)
                    {
                        var ko = kNode!.AsObject();
                        if (ko["ad"]?.GetValue<string>() == kurum.Ad)
                        {
                            var sArr = ko["subeler"]?.AsArray();
                            if (sArr != null)
                            {
                                foreach (var sNode in sArr)
                                {
                                    var so = sNode!.AsObject();
                                    if (so["ad"]?.GetValue<string>() == sube.Ad)
                                    {
                                        so["bolumler"]?.AsArray().Add(JsonValue.Create(bolumAd));
                                        break;
                                    }
                                }
                            }
                            break;
                        }
                    }
                    DosyayaYaz(_veriYolu, root.ToJsonString(JOpts));
                }
            }
            catch (Exception ex) { throw new IOException("Bölüm bilgisi kaydedilemedi.", ex); }

            // SQL Server Güncelleme
            if (_sqlAnaVeriDeposu == null && !string.IsNullOrWhiteSpace(SqlBaglantiDizesi))
            {
                try
                {
                    using var conn = new Microsoft.Data.SqlClient.SqlConnection(SqlBaglantiDizesi);
                    conn.Open();
                    const string sql = "INSERT INTO Bolumler (SubeId, Ad) SELECT Id, @bAd FROM Subeler WHERE Ad = @subeAd;";
                    using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@bAd", bolumAd);
                    cmd.Parameters.AddWithValue("@subeAd", sube.Ad);
                    cmd.ExecuteNonQuery();
                }
                catch { }
            }

            return yeniBolum;
            }
        }

        private void DosyayaYaz(string yol, string icerik)
        {
            lock (_dosyaKilit)
            {
                var dizin = Path.GetDirectoryName(yol);
                if (!string.IsNullOrEmpty(dizin)) Directory.CreateDirectory(dizin);
                var geciciYol = yol + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    if (string.Equals(Path.GetFullPath(yol), Path.GetFullPath(_veriYolu), StringComparison.OrdinalIgnoreCase))
                        _sqlAnaVeriDeposu?.Kaydet(icerik);
                    File.WriteAllText(geciciYol, icerik, System.Text.Encoding.UTF8);
                    File.Move(geciciYol, yol, overwrite: true);
                }
                finally
                {
                    if (File.Exists(geciciYol)) File.Delete(geciciYol);
                }
            }
        }

        private static void YerelDosyayaYaz(string yol, string icerik)
        {
            var dizin = Path.GetDirectoryName(yol);
            if (!string.IsNullOrEmpty(dizin)) Directory.CreateDirectory(dizin);
            var geciciYol = yol + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(geciciYol, icerik, System.Text.Encoding.UTF8);
                File.Move(geciciYol, yol, overwrite: true);
            }
            finally
            {
                if (File.Exists(geciciYol)) File.Delete(geciciYol);
            }
        }
    }
}
