// Uygulama Durumu (State)
let aktifKullanici = null;
let cikisSuruyor = false;
let kurumlarListesi = [];
let tumRandevular = [];
let uygulamaBaslangicSekmesi = 'kurumlar';
let uygulamaBaslangicAdminSekmesi = null;

// Randevu Alma Formu Geçici Seçimleri
let seciliForm = {
  kurum: null,
  sube: null,
  calisan: null,
  hizmet: null,
  tarih: null,
  saat: null
};

// Sayfa Yüklendiğinde
document.addEventListener('DOMContentLoaded', async () => {
  const sekmeler = ['kurumlar', 'randevu-al', 'randevularim', 'doktor-paneli', 'yonetici-paneli'];
  const adminSekmeleri = ['randevular', 'doktor-ekle', 'hizmet-ekle', 'bolum-ekle', 'kurum-ekle', 'sube-mesai'];
  const hashParcalari = decodeURIComponent(location.hash.replace(/^#/, '')).split('/');
  uygulamaBaslangicSekmesi = sekmeler.includes(hashParcalari[0]) ? hashParcalari[0] : 'kurumlar';
  uygulamaBaslangicAdminSekmesi = uygulamaBaslangicSekmesi === 'yonetici-paneli' && adminSekmeleri.includes(hashParcalari[1])
    ? hashParcalari[1]
    : null;
  const ilkGecmisDurumu = {
    ...(history.state || {}),
    appSekme: uygulamaBaslangicSekmesi,
    ...(uygulamaBaslangicAdminSekmesi ? { adminAltSekme: uygulamaBaslangicAdminSekmesi } : {})
  };
  const ilkHash = uygulamaBaslangicAdminSekmesi
    ? `#${uygulamaBaslangicSekmesi}/${uygulamaBaslangicAdminSekmesi}`
    : `#${uygulamaBaslangicSekmesi}`;
  history.replaceState(ilkGecmisDurumu, '', `${location.pathname}${location.search}${ilkHash}`);

  const sonGirisKimligi = localStorage.getItem('saglik_son_giris_kimlik');
  const girisKimlikAlani = document.getElementById('giris-kimlik');
  if (girisKimlikAlani && sonGirisKimligi) girisKimlikAlani.value = sonGirisKimligi;

  window.addEventListener('popstate', event => {
    const durum = event.state || {};
    const sekme = sekmeler.includes(durum.appSekme) ? durum.appSekme : 'kurumlar';
    sekmeDegistir(sekme, false);
    if (sekme === 'yonetici-paneli' && adminSekmeleri.includes(durum.adminAltSekme)) {
      adminAltSekme(durum.adminAltSekme, false);
    }
  });

  // Tarih seçici için bugünü minimum yap
  const simdi = new Date();
  const bugun = `${simdi.getFullYear()}-${String(simdi.getMonth() + 1).padStart(2, '0')}-${String(simdi.getDate()).padStart(2, '0')}`;
  const tarihInput = document.getElementById('secim-tarih');
  if (tarihInput) tarihInput.min = bugun;

  // Yerel hafızadan kullanıcıyı geri yükle (oturum token'ı yoksa eski kaydı sil)
  const kayitliToken = oturumTokeni();
  const kayitliKullanici = localStorage.getItem('saglik_aktif_kullanici');
  if (kayitliToken && kayitliKullanici) {
    try {
      aktifKullanici = JSON.parse(kayitliKullanici);
      kullaniciArayuzGuncelle();
    } catch {
      localStorage.removeItem('saglik_aktif_kullanici');
      localStorage.removeItem('saglik_oturum_token');
    }
  } else {
    localStorage.removeItem('saglik_aktif_kullanici');
    localStorage.removeItem('saglik_oturum_token');
  }

  // İlk verileri yükle
  await kurumBilgileriniYukle();
  await randevulariYukle();
  await veritabaniDurumuYukle();

  // Varsayılan sekmeyi aç
  sekmeDegistir(uygulamaBaslangicSekmesi, false);
  if (uygulamaBaslangicAdminSekmesi) adminAltSekme(uygulamaBaslangicAdminSekmesi, false);
});

async function veritabaniDurumuYukle() {
  try {
    const res = await fetch('/api/veritabani-durum');
    const data = await res.json();
    const rozet = document.getElementById('vt-durum-rozet');
    const yazi = document.getElementById('vt-durum-yazi');
    if (yazi && data) {
      if (data.bagli) {
        yazi.textContent = 'Veritabanı: SQL Server (bağlı)';
        rozet.className = 'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[11px] bg-emerald-50 text-emerald-700 font-semibold border border-emerald-200';
      } else {
        yazi.textContent = 'Veri deposu: JSON';
        rozet.className = 'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[11px] bg-slate-100 text-slate-700 font-semibold border border-slate-200';
      }
    }
  } catch {}
}

function oturumTokeni() {
  return localStorage.getItem('saglik_oturum_token');
}

async function apiFetch(url, options = {}) {
  const headers = { ...(options.headers || {}) };
  const token = oturumTokeni();
  if (token) headers.Authorization = 'Bearer ' + token;
  const res = await fetch(url, { ...options, headers });
  if (res.status === 401 && token) cikisYap(true);
  return res;
}

// ──────────────────────────────── TOAST BİLDİRİMLERİ ────────────────────────────────
function toast(mesaj, tip = 'bilgi') {
  const container = document.getElementById('toast-container');
  if (!container) return;

  const el = document.createElement('div');
  el.className = `toast px-4 py-3 rounded-xl shadow-xl text-sm font-semibold flex items-center gap-3 border transition-all text-white max-w-md ${
    tip === 'basari' ? 'bg-emerald-600 border-emerald-500' :
    tip === 'hata'   ? 'bg-rose-600 border-rose-500' :
    tip === 'uyari'  ? 'bg-amber-600 border-amber-500' : 'bg-slate-800 border-slate-700'
  }`;

  const icon = tip === 'basari' ? 'fa-circle-check' :
               tip === 'hata'   ? 'fa-circle-xmark' :
               tip === 'uyari'  ? 'fa-triangle-exclamation' : 'fa-circle-info';

  el.innerHTML = `<i class="fa-solid ${icon} text-base"></i><span>${mesaj}</span>`;
  container.appendChild(el);

  setTimeout(() => {
    el.style.opacity = '0';
    el.style.transform = 'translateY(-10px)';
    setTimeout(() => el.remove(), 300);
  }, 4000);
}

// ──────────────────────────────── SEKME DEĞİŞTİRME ────────────────────────────────
function sekmeDegistir(sekmeId, gecmiseEkle = true) {
  const rol = aktifKullanici?.rol;
  const yetkisizOzelSekme =
    (sekmeId === 'yonetici-paneli' && rol !== 'Yonetici') ||
    (sekmeId === 'doktor-paneli' && !['Doktor', 'Personel'].includes(rol));
  if (yetkisizOzelSekme) {
    sekmeId = 'kurumlar';
    gecmiseEkle = false;
    history.replaceState({ appSekme: sekmeId }, '', `${location.pathname}${location.search}#${sekmeId}`);
  }

  if (gecmiseEkle) {
    const mevcutDurum = history.state || {};
    if (mevcutDurum.appSekme !== sekmeId || mevcutDurum.adminAltSekme) {
      history.pushState({ appSekme: sekmeId }, '', `${location.pathname}${location.search}#${sekmeId}`);
    }
  }

  // Tüm sekmeleri gizle
  const sekmeler = ['kurumlar', 'randevu-al', 'randevularim', 'doktor-paneli', 'yonetici-paneli'];
  sekmeler.forEach(id => {
    const el = document.getElementById(`sekme-${id}`);
    const navBtn = document.getElementById(`nav-${id}`);
    if (el) el.classList.add('hidden');
    if (navBtn) {
      navBtn.classList.remove('text-brand-700', 'bg-brand-50');
      navBtn.classList.add('text-slate-600');
    }
  });

  // Seçilen sekmeyi göster
  const hedef = document.getElementById(`sekme-${sekmeId}`);
  const navBtn = document.getElementById(`nav-${sekmeId}`);
  if (hedef) hedef.classList.remove('hidden');
  if (navBtn) {
    navBtn.classList.add('text-brand-700', 'bg-brand-50');
    navBtn.classList.remove('text-slate-600');
  }

  // Sekmeye özel veri tazeleme
  if (sekmeId === 'randevularim') randevularimListele();
  if (sekmeId === 'doktor-paneli') doktorPaneliniYukle();
  if (sekmeId === 'yonetici-paneli') yoneticiPaneliniYukle();
  if (sekmeId === 'randevu-al') randevuFormuGuncelle();
}

// ──────────────────────────────── MODAL YÖNETİMİ ────────────────────────────────
function modalAc(id) {
  const el = document.getElementById(id);
  if (el) el.classList.remove('hidden');
}
function modalKapat(id) {
  const el = document.getElementById(id);
  if (el) el.classList.add('hidden');
}

// ──────────────────────────────── KULLANICI GİRİŞ / ÇIKIŞ ────────────────────────
async function girisFormuGonder(e) {
  e.preventDefault();
  const kimlik = document.getElementById('giris-kimlik').value.trim();
  const sifre = document.getElementById('giris-sifre').value.trim();
  await girisIslemi(kimlik, sifre);
}

async function girisIslemi(kimlik, sifre) {
  try {
    const res = await fetch('/api/auth/giris', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ kimlik, sifre })
    });
    const data = await res.json();

    if (!res.ok || !data.basarili) {
      toast(data.mesaj || 'Giriş başarısız!', 'hata');
      return;
    }

    aktifKullanici = data.kullanici;
    localStorage.setItem('saglik_son_giris_kimlik', kimlik);
    localStorage.setItem('saglik_oturum_token', data.token);
    localStorage.setItem('saglik_aktif_kullanici', JSON.stringify(aktifKullanici));
    modalKapat('modal-giris');
    kullaniciArayuzGuncelle();
    toast(`Hoş geldiniz, ${aktifKullanici.adSoyad}!`, 'basari');

    // Role göre en uygun sekmeye yönlendir
    if (aktifKullanici.rol === 'Doktor' || aktifKullanici.rol === 'Personel') sekmeDegistir('doktor-paneli');
    else if (aktifKullanici.rol === 'Yonetici') sekmeDegistir('yonetici-paneli');
    else sekmeDegistir('randevularim');
  } catch (err) {
    toast('Sunucuya bağlanılamadı: ' + err.message, 'hata');
  }
}

async function kayitFormuGonder(e) {
  e.preventDefault();
  const ad = document.getElementById('kayit-ad').value.trim();
  const soyad = document.getElementById('kayit-soyad').value.trim();
  const tcNo = document.getElementById('kayit-tc').value.trim();
  const sifre = document.getElementById('kayit-sifre').value.trim();

  try {
    const res = await fetch('/api/auth/kayit', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ad, soyad, tcNo, sifre })
    });
    const data = await res.json();

    if (!res.ok || !data.basarili) {
      toast(data.mesaj || 'Kayıt yapılamadı!', 'hata');
      return;
    }

    aktifKullanici = data.kullanici;
    const hastaGirisKimligi = data.kullanici.tcNo || data.kullanici.kimlik;
    localStorage.setItem('saglik_son_giris_kimlik', hastaGirisKimligi);
    localStorage.setItem('saglik_oturum_token', data.token);
    localStorage.setItem('saglik_aktif_kullanici', JSON.stringify(aktifKullanici));
    modalKapat('modal-kayit');
    kullaniciArayuzGuncelle();
    toast(`Kayıt oluşturuldu. Giriş kimliğiniz: ${data.kullanici.kimlik}. Çıkıştan sonra TC numaranızla veya bu kimlikle giriş yapabilirsiniz.`, 'basari');
    sekmeDegistir('randevu-al');
  } catch (err) {
    toast('Kayıt sırasında hata oluştu: ' + err.message, 'hata');
  }
}

function cikisYap(sessiz = false) {
  const token = oturumTokeni();
  aktifKullanici = null;
  localStorage.removeItem('saglik_aktif_kullanici');
  localStorage.removeItem('saglik_oturum_token');
  if (token && !cikisSuruyor) {
    cikisSuruyor = true;
    fetch('/api/auth/cikis', { method: 'POST', headers: { Authorization: 'Bearer ' + token } }).catch(() => {});
    cikisSuruyor = false;
  }
  kullaniciArayuzGuncelle();
  if (!sessiz) toast('Çıkış yapıldı.', 'bilgi');
  history.replaceState({ appSekme: 'kurumlar' }, '', `${location.pathname}${location.search}#kurumlar`);
  sekmeDegistir('kurumlar', false);
}

function kullaniciArayuzGuncelle() {
  const anonim = document.getElementById('auth-anonim');
  const user = document.getElementById('auth-kullanici');
  const navDoktor = document.getElementById('nav-doktor-paneli');
  const navYonetici = document.getElementById('nav-yonetici-paneli');
  const uyariRandevu = document.getElementById('randevu-giris-uyari');

  if (aktifKullanici) {
    anonim.classList.add('hidden');
    user.classList.remove('hidden');
    document.getElementById('kullanici-ad').textContent = aktifKullanici.adSoyad;
    document.getElementById('kullanici-rol-badge').textContent = aktifKullanici.unvan || aktifKullanici.rol;

    if (uyariRandevu) uyariRandevu.classList.add('hidden');

    if (aktifKullanici.rol === 'Doktor' || aktifKullanici.rol === 'Personel') navDoktor.classList.remove('hidden');
    else navDoktor.classList.add('hidden');

    if (aktifKullanici.rol === 'Yonetici') navYonetici.classList.remove('hidden');
    else navYonetici.classList.add('hidden');
  } else {
    anonim.classList.remove('hidden');
    user.classList.add('hidden');
    navDoktor.classList.add('hidden');
    navYonetici.classList.add('hidden');
    if (uyariRandevu) uyariRandevu.classList.remove('hidden');
  }

  randevuFormuGuncelle();
}

// ──────────────────────────────── KURUMLAR VE ŞUBELER ────────────────────────
async function kurumBilgileriniYukle() {
  try {
    const res = await fetch('/api/kurumlar', { cache: 'no-store' });
    kurumlarListesi = await res.json();
    kurumlarKartlariniCiz();
    kurumSeciminiDoldur();
    // Admin paneli açıksa dropdown'ları da güncelle
    await adminKurumDropdownlariniDoldur();
  } catch (err) {
    console.error('Kurumlar yüklenemedi:', err);
  }
}

function kurumlarKartlariniCiz() {
  const container = document.getElementById('kurumlar-kart-listesi');
  if (!container) return;

  if (!kurumlarListesi.length) {
    container.innerHTML = '<div class="col-span-2 text-center text-slate-400 py-12">Kayıtlı kurum bulunamadı.</div>';
    return;
  }

  container.innerHTML = kurumlarListesi.map(kurum => `
    <div class="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 hover-card">
      <div class="flex items-start justify-between border-b border-slate-100 pb-4 mb-4">
        <div>
          <span class="inline-block px-2.5 py-0.5 rounded-full text-[11px] font-bold ${kurum.tur === 'Hastane' ? 'bg-sky-100 text-sky-800' : 'bg-teal-100 text-teal-800'} mb-1">
            ${kurum.tur}
          </span>
          <h3 class="text-xl font-bold text-slate-900">${kurum.ad}</h3>
        </div>
        <div class="w-10 h-10 rounded-xl bg-slate-50 text-slate-600 flex items-center justify-center text-lg border border-slate-100">
          <i class="fa-solid ${kurum.tur === 'Hastane' ? 'fa-hospital' : 'fa-house-medical'}"></i>
        </div>
      </div>

      <div class="space-y-4">
        ${kurum.subeler.map(sube => `
          <div class="bg-slate-50 p-4 rounded-xl border border-slate-200/80 space-y-2">
            <div class="flex items-center justify-between">
              <span class="font-bold text-slate-800 text-sm flex items-center gap-2">
                <i class="fa-solid fa-location-dot text-rose-500 text-xs"></i> ${sube.ad}
              </span>
              <button onclick="subedenRandevuAl('${kurum.ad}', '${sube.ad}')" class="text-xs font-semibold text-brand-600 hover:text-brand-800 bg-brand-50 hover:bg-brand-100 px-2.5 py-1 rounded-lg transition">
                Randevu Al &rarr;
              </button>
            </div>
            <p class="text-xs text-slate-500">${sube.adres}</p>

            <div class="pt-2 text-xs text-slate-600 border-t border-slate-200/60">
              <span class="font-semibold text-slate-700">Poliklinikler:</span>
              <span>${sube.bolumler.join(', ') || 'Belirtilmemiş'}</span>
            </div>

            <div class="pt-1 text-xs text-slate-600">
              <span class="font-semibold text-slate-700">Hizmetler:</span>
              <div class="flex flex-wrap gap-1.5 mt-1">
                ${sube.hizmetler.map(h => `
                  <span class="bg-white border border-slate-200 px-2 py-0.5 rounded text-[11px]">
                    ${h.ad} <b class="text-emerald-600">(${h.ucret} ₺)</b>
                  </span>
                `).join('')}
              </div>
            </div>
          </div>
        `).join('')}
      </div>
    </div>
  `).join('');
}

function subedenRandevuAl(kurumAd, subeAd) {
  sekmeDegistir('randevu-al');

  const kurumSelect = document.getElementById('secim-kurum');
  if (kurumSelect) {
    kurumSelect.value = kurumAd;
    kurumSecildi();

    setTimeout(() => {
      const subeSelect = document.getElementById('secim-sube');
      if (subeSelect) {
        subeSelect.value = subeAd;
        subeSecildi();
      }
    }, 50);
  }
}

// ──────────────────────────────── RANDEVU AL SİHİRBAZI ────────────────────────
function kurumSeciminiDoldur() {
  const select = document.getElementById('secim-kurum');
  if (!select) return;
  select.innerHTML = '<option value="">Kurum Seçiniz...</option>' +
    kurumlarListesi.map(k => `<option value="${k.ad}">${k.ad} (${k.tur})</option>`).join('');
}

function kurumSecildi() {
  const kurumAd = document.getElementById('secim-kurum').value;
  const subeSelect = document.getElementById('secim-sube');
  seciliForm = { kurum: null, sube: null, calisan: null, hizmet: null, tarih: null, saat: null };

  if (!kurumAd) {
    subeSelect.innerHTML = '<option value="">Önce kurum seçiniz...</option>';
    subeSelect.disabled = true;
    calisanSeciminiSifirla();
    randevuOzetiniGuncelle();
    return;
  }

  seciliForm.kurum = kurumlarListesi.find(k => k.ad === kurumAd);
  subeSelect.innerHTML = '<option value="">Şube Seçiniz...</option>' +
    seciliForm.kurum.subeler.map(s => `<option value="${s.ad}">${s.ad}</option>`).join('');
  subeSelect.disabled = false;

  calisanSeciminiSifirla();
  randevuOzetiniGuncelle();
}

function subeSecildi() {
  const subeAd = document.getElementById('secim-sube').value;
  const calisanSelect = document.getElementById('secim-calisan');

  if (!subeAd || !seciliForm.kurum) {
    calisanSeciminiSifirla();
    return;
  }

  seciliForm.sube = seciliForm.kurum.subeler.find(s => s.ad === subeAd);
  calisanSelect.innerHTML = '<option value="">Doktor / Personel Seçiniz...</option>' +
    seciliForm.sube.calisanlar.map(c => `
      <option value="${c.kimlik}">${c.adSoyad} (${c.unvan})</option>
    `).join('');
  calisanSelect.disabled = false;

  hizmetSeciminiSifirla();
  randevuOzetiniGuncelle();
}

function calisanSecildi() {
  const calisanKimlik = document.getElementById('secim-calisan').value;
  const hizmetSelect = document.getElementById('secim-hizmet');

  if (!calisanKimlik || !seciliForm.sube) {
    hizmetSeciminiSifirla();
    return;
  }

  seciliForm.calisan = seciliForm.sube.calisanlar.find(c => c.kimlik === calisanKimlik);

  // Bu çalışanın sunduğu hizmetleri şubenin hizmet listesinden eşleştir
  const uygunHizmetler = seciliForm.sube.hizmetler.filter(h =>
    seciliForm.calisan.hizmetler.includes(h.ad)
  );

  hizmetSelect.innerHTML = '<option value="">Hizmet Seçiniz...</option>' +
    uygunHizmetler.map(h => `
      <option value="${h.ad}">${h.ad} - ${h.sureDakika} dk (${h.ucret} ₺)</option>
    `).join('');
  hizmetSelect.disabled = false;

  tarihVeSlotlariSifirla();
  randevuOzetiniGuncelle();
}

function hizmetSecildi() {
  const hizmetAd = document.getElementById('secim-hizmet').value;
  const tarihInput = document.getElementById('secim-tarih');

  if (!hizmetAd || !seciliForm.sube) {
    tarihVeSlotlariSifirla();
    return;
  }

  seciliForm.hizmet = seciliForm.sube.hizmetler.find(h => h.ad === hizmetAd);
  tarihInput.disabled = false;

  if (tarihInput.value) tarihSecildi();
  randevuOzetiniGuncelle();
}

async function tarihSecildi() {
  const tarih = document.getElementById('secim-tarih').value;
  const slotAlani = document.getElementById('saat-slotlari-alani');
  seciliForm.tarih = tarih;
  seciliForm.saat = null;

  if (!tarih || !seciliForm.calisan || !seciliForm.hizmet) {
    slotAlani.innerHTML = '<span>Lütfen geçerli bir tarih seçiniz.</span>';
    randevuOzetiniGuncelle();
    return;
  }

  slotAlani.innerHTML = '<span class="text-brand-600"><i class="fa-solid fa-spinner fa-spin mr-2"></i>Müsait saatler kontrol ediliyor...</span>';

  try {
    const res = await fetch(`/api/bos-saatler?calisanKimlik=${seciliForm.calisan.kimlik}&hizmetAdi=${encodeURIComponent(seciliForm.hizmet.ad)}&tarih=${tarih}`);
    const data = await res.json();

    if (!res.ok || !data.saatler || !data.saatler.length) {
      slotAlani.innerHTML = `<span class="text-amber-600"><i class="fa-solid fa-calendar-xmark mr-1.5"></i>Bu tarihte (${tarih}) hekimin veya şubenin uygun randevu saati bulunmuyor.</span>`;
      randevuOzetiniGuncelle();
      return;
    }

    slotAlani.innerHTML = `
      <div class="flex flex-wrap gap-2 w-full">
        ${data.saatler.map(saat => `
          <button type="button" onclick="saatSec('${saat}', this)" class="slot-btn px-3 py-1.5 bg-white border border-slate-300 hover:border-brand-500 rounded-lg text-xs font-bold text-slate-700 transition">
            ${saat}
          </button>
        `).join('')}
      </div>
    `;
  } catch (err) {
    slotAlani.innerHTML = '<span class="text-rose-500">Saatler alınırken hata oluştu.</span>';
  }

  randevuOzetiniGuncelle();
}

function saatSec(saat, btn) {
  document.querySelectorAll('.slot-btn').forEach(b => b.classList.remove('selected'));
  btn.classList.add('selected');
  seciliForm.saat = saat;
  randevuOzetiniGuncelle();
}

function calisanSeciminiSifirla() {
  const el = document.getElementById('secim-calisan');
  el.innerHTML = '<option value="">Önce şube seçiniz...</option>';
  el.disabled = true;
  hizmetSeciminiSifirla();
}
function hizmetSeciminiSifirla() {
  const el = document.getElementById('secim-hizmet');
  el.innerHTML = '<option value="">Önce hekim seçiniz...</option>';
  el.disabled = true;
  tarihVeSlotlariSifirla();
}
function tarihVeSlotlariSifirla() {
  const el = document.getElementById('secim-tarih');
  el.disabled = true;
  document.getElementById('saat-slotlari-alani').innerHTML = '<span>Hizmet ve tarih seçtiğinizde uygun saat dilimleri burada listelenecektir.</span>';
  seciliForm.saat = null;
}

function randevuFormuGuncelle() {
  const ozetHasta = document.getElementById('ozet-hasta');
  if (ozetHasta) {
    ozetHasta.textContent = aktifKullanici ? `${aktifKullanici.adSoyad} (${aktifKullanici.rol})` : 'Giriş Yapılmadı';
  }
  randevuOzetiniGuncelle();
}

function randevuOzetiniGuncelle() {
  const btn = document.getElementById('btn-randevu-onayla');
  document.getElementById('ozet-kurum').textContent = (seciliForm.kurum && seciliForm.sube) ? `${seciliForm.kurum.ad} / ${seciliForm.sube.ad}` : '-';
  document.getElementById('ozet-calisan').textContent = seciliForm.calisan ? `${seciliForm.calisan.adSoyad} (${seciliForm.calisan.unvan})` : '-';
  document.getElementById('ozet-hizmet').textContent = seciliForm.hizmet ? `${seciliForm.hizmet.ad} (${seciliForm.hizmet.sureDakika} dk)` : '-';
  document.getElementById('ozet-zaman').textContent = (seciliForm.tarih && seciliForm.saat) ? `${seciliForm.tarih} Saat: ${seciliForm.saat}` : '-';
  document.getElementById('ozet-ucret').textContent = seciliForm.hizmet ? `${seciliForm.hizmet.ucret.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺` : '0,00 ₺';

  // Buton aktifliği
  const hazir = aktifKullanici && aktifKullanici.rol === 'Hasta' &&
                seciliForm.kurum && seciliForm.sube && seciliForm.calisan && seciliForm.hizmet && seciliForm.tarih && seciliForm.saat;
  if (btn) btn.disabled = !hazir;
}

async function randevuKaydet() {
  if (!aktifKullanici) {
    toast('Randevu oluşturmak için giriş yapmalısınız.', 'uyari');
    modalAc('modal-giris');
    return;
  }

  const baslangic = `${seciliForm.tarih}T${seciliForm.saat}:00`;
  const istek = {
    hastaKimlik: aktifKullanici.kimlik,
    calisanKimlik: seciliForm.calisan.kimlik,
    hizmetAdi: seciliForm.hizmet.ad,
    baslangic: baslangic
  };

  try {
    const res = await apiFetch('/api/randevular', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(istek)
    });
    const data = await res.json();

    if (!res.ok || !data.basarili) {
      toast(data.mesaj || 'Randevu oluşturulamadı!', 'hata');
      return;
    }

    toast('✓ Randevunuz başarıyla oluşturuldu!', 'basari');
    await randevulariYukle();

    // Formu sıfırla
    kurumSeciminiDoldur();
    kurumSecildi();
    sekmeDegistir('randevularim');
  } catch (err) {
    toast('Bağlantı hatası: ' + err.message, 'hata');
  }
}

// ──────────────────────────────── RANDEVULARIM (HASTA) ────────────────────────
async function randevulariYukle() {
  try {
    const res = await apiFetch('/api/randevular');
    if (res.ok) tumRandevular = await res.json();
  } catch (err) {
    console.error('Randevular yüklenemedi:', err);
  }
}

function randevularimListele() {
  const container = document.getElementById('randevularim-kart-listesi');
  if (!container) return;

  if (!aktifKullanici) {
    container.innerHTML = `
      <div class="col-span-3 text-center py-12 bg-white rounded-2xl border border-slate-200 p-8 space-y-4">
        <i class="fa-solid fa-lock text-4xl text-slate-300"></i>
        <h3 class="text-lg font-bold text-slate-700">Randevularınızı görmek için lütfen giriş yapın.</h3>
        <button onclick="modalAc('modal-giris')" class="px-5 py-2.5 bg-brand-600 hover:bg-brand-700 text-white font-bold rounded-xl shadow-md transition">
          Giriş Yap
        </button>
      </div>
    `;
    return;
  }

  // Hasta ise kendi randevuları; Yönetici ise tümü
  const filtreli = (aktifKullanici.rol === 'Hasta')
    ? tumRandevular.filter(r => r.hastaKimlik === aktifKullanici.kimlik)
    : tumRandevular;

  if (!filtreli.length) {
    container.innerHTML = `
      <div class="col-span-3 text-center py-12 bg-white rounded-2xl border border-slate-200 p-8 space-y-4">
        <i class="fa-solid fa-calendar-xmark text-4xl text-slate-300"></i>
        <h3 class="text-lg font-bold text-slate-700">Henüz kayıtlı bir randevunuz bulunmuyor.</h3>
        <button onclick="sekmeDegistir('randevu-al')" class="px-5 py-2.5 bg-brand-600 hover:bg-brand-700 text-white font-bold rounded-xl shadow-md transition">
          Hemen Randevu Al
        </button>
      </div>
    `;
    return;
  }

  container.innerHTML = filtreli.map(r => `
    <div class="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 hover-card flex flex-col justify-between space-y-4">
      <div>
        <div class="flex items-start justify-between gap-2 mb-3">
          <span class="inline-block px-2.5 py-0.5 rounded-full text-xs font-bold badge-${r.durum.toLowerCase()}">
            ${r.durum}
          </span>
          <span class="text-xs text-slate-400 font-mono">#${r.id}</span>
        </div>

        <h4 class="text-base font-bold text-slate-900">${r.hizmetAdi}</h4>
        <p class="text-xs text-slate-500 font-medium">${r.kurumAdi} - ${r.subeAdi}</p>

        <div class="mt-4 pt-3 border-t border-slate-100 space-y-1.5 text-xs text-slate-600">
          <div class="flex items-center gap-2">
            <i class="fa-solid fa-user-doctor text-slate-400 w-4"></i>
            <span>${r.calisanAdSoyad} (${r.calisanUnvan})</span>
          </div>
          <div class="flex items-center gap-2 font-semibold text-slate-800">
            <i class="fa-solid fa-clock text-brand-600 w-4"></i>
            <span>${r.tarihFormatli} - ${r.bitisFormatli} (${r.sureDakika} dk)</span>
          </div>
          <div class="flex items-center gap-2 text-emerald-600 font-bold text-sm">
            <i class="fa-solid fa-tag text-emerald-500 w-4"></i>
            <span>${r.ucret.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺</span>
          </div>
        </div>
      </div>

      ${r.aktif ? `
        <button onclick="randevuIptalEt(${r.id})" class="w-full py-2 bg-rose-50 hover:bg-rose-100 text-rose-700 font-semibold rounded-xl text-xs transition border border-rose-200">
          <i class="fa-solid fa-ban mr-1"></i> Randevuyu İptal Et
        </button>
      ` : `
        <div class="text-center py-1 text-xs text-slate-400 font-medium bg-slate-50 rounded-lg">
          İşlem kapandı (${r.durum})
        </div>
      `}
    </div>
  `).join('');
}

async function randevuIptalEt(id) {
  if (!confirm('Bu randevuyu iptal etmek istediğinize emin misiniz?')) return;

  try {
    const res = await apiFetch(`/api/randevular/${id}/iptal`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ yapanKimlik: aktifKullanici.kimlik })
    });
    const data = await res.json();

    if (!res.ok || !data.basarili) {
      toast(data.mesaj || 'İptal edilemedi!', 'hata');
      return;
    }

    toast('Randevunuz iptal edildi.', 'basari');
    await randevulariYukle();
    randevularimListele();
  } catch (err) {
    toast('Hata oluştu: ' + err.message, 'hata');
  }
}

// ──────────────────────────────── DOKTOR / PERSONEL PANELİ ────────────────────
async function doktorPaneliniYukle() {
  if (!aktifKullanici || (aktifKullanici.rol !== 'Doktor' && aktifKullanici.rol !== 'Personel')) return;

  document.getElementById('doktor-panel-ad').textContent = aktifKullanici.adSoyad;
  document.getElementById('doktor-panel-unvan').textContent = aktifKullanici.unvan;

  await randevulariYukle();

  const doktorRandevulari = tumRandevular.filter(r => r.calisanKimlik === aktifKullanici.kimlik);
  const bekleyenler = doktorRandevulari.filter(r => r.durum === 'Beklemede');

  // Rozet
  const rozet = document.getElementById('doktor-bekleyen-rozet');
  if (rozet) {
    rozet.textContent = bekleyenler.length;
    rozet.classList.toggle('hidden', bekleyenler.length === 0);
  }
  document.getElementById('doktor-bekleyen-sayisi').textContent = `${bekleyenler.length} Talep`;

  // Bekleyenler Kartları
  const bekleyenContainer = document.getElementById('doktor-bekleyen-listesi');
  if (!bekleyenler.length) {
    bekleyenContainer.innerHTML = '<div class="text-slate-400 text-sm py-4 italic">Onay bekleyen randevu talebiniz bulunmuyor.</div>';
  } else {
    bekleyenContainer.innerHTML = bekleyenler.map(r => `
      <div class="bg-amber-50/60 border border-amber-200 rounded-xl p-4 flex flex-wrap items-center justify-between gap-4">
        <div>
          <div class="flex items-center gap-2">
            <span class="font-bold text-slate-900">${r.hastaAdSoyad}</span>
            <span class="text-xs text-slate-500 font-mono">(TC: ${r.hastaTcNo || '-'})</span>
          </div>
          <div class="text-xs text-slate-600 mt-1">
            <span class="font-semibold text-brand-700">${r.hizmetAdi}</span> • 
            <span>${r.tarihFormatli} (${r.sureDakika} dk)</span> • 
            <span class="text-emerald-700 font-bold">${r.ucret} ₺</span>
          </div>
        </div>
        <div class="flex items-center gap-2">
          <button onclick="doktorKarar(${r.id}, 'onayla')" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold rounded-lg shadow-sm transition">
            <i class="fa-solid fa-check mr-1"></i> Onayla
          </button>
          <button onclick="doktorKarar(${r.id}, 'reddet')" class="px-4 py-2 bg-rose-600 hover:bg-rose-700 text-white text-xs font-bold rounded-lg shadow-sm transition">
            <i class="fa-solid fa-xmark mr-1"></i> Reddet
          </button>
        </div>
      </div>
    `).join('');
  }

  // Geçmiş Tablosu
  const tablo = document.getElementById('doktor-gecmis-tablo');
  tablo.innerHTML = doktorRandevulari.map(r => `
    <tr class="hover:bg-slate-50">
      <td class="py-3 px-4 font-mono text-xs">#${r.id}</td>
      <td class="py-3 px-4 font-semibold text-slate-800">${r.hastaAdSoyad}</td>
      <td class="py-3 px-4">${r.hizmetAdi}</td>
      <td class="py-3 px-4 font-medium">${r.tarihFormatli}</td>
      <td class="py-3 px-4 text-emerald-600 font-bold">${r.ucret} ₺</td>
      <td class="py-3 px-4">
        <span class="px-2 py-0.5 rounded-full text-xs font-bold badge-${r.durum.toLowerCase()}">
          ${r.durum}
        </span>
      </td>
    </tr>
  `).join('');
}

async function doktorKarar(id, aksiyon) {
  try {
    const res = await apiFetch(`/api/randevular/${id}/${aksiyon}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({})
    });
    const data = await res.json();

    if (!res.ok || !data.basarili) {
      toast(data.mesaj || 'İşlem başarısız!', 'hata');
      return;
    }

    toast(`Randevu ${aksiyon === 'onayla' ? 'onaylandı' : 'reddedildi'}.`, 'basari');
    doktorPaneliniYukle();
  } catch (err) {
    toast('Hata: ' + err.message, 'hata');
  }
}

// ──────────────────────────────── YÖNETİCİ PANELİ ────────────────────────────
async function yoneticiPaneliniYukle() {
  if (!aktifKullanici || aktifKullanici.rol !== 'Yonetici') return;

  // Kurum listesini ve yönetim paneli açılır listelerini garanti tazele
  await kurumBilgileriniYukle();
  adminKurumDropdownlariniDoldur();

  // İstatistikleri yükle
  try {
    const res = await apiFetch('/api/istatistikler');
    const stat = await res.json();

    document.getElementById('stat-toplam').textContent = stat.toplamRandevu;
    document.getElementById('stat-bekleyen').textContent = stat.beklemede;
    document.getElementById('stat-onaylanan').textContent = stat.onaylandi;
    document.getElementById('stat-ciro').textContent = `${stat.onaylananCiro.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺`;
  } catch (err) {
    console.error('İstatistikler yüklenemedi:', err);
  }

  await randevulariYukle();
  yoneticiRandevulariFiltrele();
}

function yoneticiRandevulariFiltrele() {
  const filtre = document.getElementById('yonetici-filtre-durum').value;
  const tablo = document.getElementById('yonetici-randevular-tablo');

  const liste = (filtre === 'hepsi')
    ? tumRandevular
    : tumRandevular.filter(r => r.durum === filtre);

  if (!liste.length) {
    tablo.innerHTML = '<tr><td colspan="9" class="py-6 text-center text-slate-400">Kayıt bulunamadı.</td></tr>';
    return;
  }

  tablo.innerHTML = liste.map(r => `
    <tr class="hover:bg-slate-50 transition">
      <td class="py-3 px-4 font-mono text-xs">#${r.id}</td>
      <td class="py-3 px-4 font-semibold text-slate-800">${r.hastaAdSoyad}</td>
      <td class="py-3 px-4 text-xs text-slate-500">${r.kurumAdi} / ${r.subeAdi}</td>
      <td class="py-3 px-4 text-xs">${r.calisanAdSoyad}</td>
      <td class="py-3 px-4 text-xs">${r.hizmetAdi}</td>
      <td class="py-3 px-4 text-xs font-semibold">${r.tarihFormatli}</td>
      <td class="py-3 px-4 text-xs font-bold text-emerald-600">${r.ucret} ₺</td>
      <td class="py-3 px-4">
        <span class="px-2 py-0.5 rounded-full text-xs font-bold badge-${r.durum.toLowerCase()}">
          ${r.durum}
        </span>
      </td>
      <td class="py-3 px-4 text-right">
        ${r.durum === 'Beklemede' ? `
          <div class="flex items-center justify-end gap-1.5">
            <button onclick="yoneticiKarar(${r.id}, 'onayla')" title="Onayla" class="p-1.5 bg-emerald-100 hover:bg-emerald-200 text-emerald-700 rounded-lg text-xs">
              <i class="fa-solid fa-check"></i>
            </button>
            <button onclick="yoneticiKarar(${r.id}, 'reddet')" title="Reddet" class="p-1.5 bg-rose-100 hover:bg-rose-200 text-rose-700 rounded-lg text-xs">
              <i class="fa-solid fa-xmark"></i>
            </button>
          </div>
        ` : `
          <span class="text-xs text-slate-400">-</span>
        `}
      </td>
    </tr>
  `).join('');
}

async function yoneticiKarar(id, aksiyon) {
  try {
    const res = await apiFetch(`/api/randevular/${id}/${aksiyon}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({})
    });
    const data = await res.json();

    if (!res.ok || !data.basarili) {
      toast(data.mesaj || 'İşlem yapılamadı!', 'hata');
      return;
    }

    toast(`Randevu #${id} ${aksiyon === 'onayla' ? 'onaylandı' : 'reddedildi'}.`, 'basari');
    yoneticiPaneliniYukle();
  } catch (err) {
    toast('Hata: ' + err.message, 'hata');
  }
}

// ──────────────────────────────── YÖNETİCİ ALT SEKMELERİ & VERİ EKLEME ─────────
function adminAltSekme(altId, gecmiseEkle = true) {
  const alts = ['randevular', 'doktor-ekle', 'hizmet-ekle', 'bolum-ekle', 'kurum-ekle', 'sube-mesai'];
  if (aktifKullanici?.rol !== 'Yonetici') return;
  if (gecmiseEkle && alts.includes(altId)) {
    const mevcutDurum = history.state || {};
    if (mevcutDurum.appSekme !== 'yonetici-paneli' || mevcutDurum.adminAltSekme !== altId) {
      history.pushState(
        { appSekme: 'yonetici-paneli', adminAltSekme: altId },
        '',
        `${location.pathname}${location.search}#yonetici-paneli/${altId}`
      );
    }
  }

  alts.forEach(id => {
    const el = document.getElementById(`admin-panel-${id}`);
    const btn = document.getElementById(`admin-subnav-${id}`);
    if (el) el.classList.add('hidden');
    if (btn) {
      btn.className = 'admin-tab-btn px-3.5 py-2 rounded-xl text-xs font-bold bg-white text-slate-700 hover:bg-slate-100 border border-slate-200 transition flex items-center gap-2';
    }
  });

  const seciliEl = document.getElementById(`admin-panel-${altId}`);
  const seciliBtn = document.getElementById(`admin-subnav-${altId}`);
  if (seciliEl) seciliEl.classList.remove('hidden');
  if (seciliBtn) {
    seciliBtn.className = 'admin-tab-btn px-3.5 py-2 rounded-xl text-xs font-bold bg-brand-600 text-white shadow-sm transition flex items-center gap-2';
  }

  // Seçilen alt sekmeye göre açılır listeleri güncelle
  adminKurumDropdownlariniDoldur();
}

async function adminKurumDropdownlariniDoldur() {
  // Always refresh so newly created institutions are available in admin forms.
  try {
    const res = await fetch('/api/kurumlar', { cache: 'no-store' });
    if (res.ok) kurumlarListesi = await res.json();
  } catch (err) {
    console.error('Admin institution list could not be refreshed:', err);
  }

  const doktorKurum = document.getElementById('admin-doktor-kurum');
  const hizmetKurum = document.getElementById('admin-hizmet-kurum');
  const bolumKurum = document.getElementById('admin-bolum-kurum');
  const mesaiKurum = document.getElementById('admin-mesai-kurum');

  const options = '<option value="">Kurum Seçin...</option>' +
    (kurumlarListesi || []).map(k => `<option value="${k.ad}">${k.ad} (${k.tur})</option>`).join('');

  if (doktorKurum) {
    const prev = doktorKurum.value;
    doktorKurum.innerHTML = options;
    if (prev && Array.from(doktorKurum.options).some(o => o.value === prev)) {
      doktorKurum.value = prev;
    } else if (kurumlarListesi && kurumlarListesi.length > 0) {
      doktorKurum.selectedIndex = 1;
      adminDoktorKurumSecildi();
    }
  }

  if (hizmetKurum) {
    const prev = hizmetKurum.value;
    hizmetKurum.innerHTML = options;
    if (prev && Array.from(hizmetKurum.options).some(o => o.value === prev)) {
      hizmetKurum.value = prev;
    } else if (kurumlarListesi && kurumlarListesi.length > 0) {
      hizmetKurum.selectedIndex = 1;
      adminHizmetKurumSecildi();
    }
  }

  if (bolumKurum) {
    const prev = bolumKurum.value;
    bolumKurum.innerHTML = options;
    if (prev && Array.from(bolumKurum.options).some(o => o.value === prev)) {
      bolumKurum.value = prev;
    } else if (kurumlarListesi && kurumlarListesi.length > 0) {
      bolumKurum.selectedIndex = 1;
      adminBolumKurumSecildi();
    }
  }

  if (mesaiKurum) {
    const prev = mesaiKurum.value;
    mesaiKurum.innerHTML = options;
    if (prev && Array.from(mesaiKurum.options).some(o => o.value === prev)) {
      mesaiKurum.value = prev;
    } else if (kurumlarListesi && kurumlarListesi.length > 0) {
      mesaiKurum.selectedIndex = 1;
    }
    adminMesaiKurumSecildi();
  }
}

function adminMesaiKurumSecildi() {
  const kurumAd = document.getElementById('admin-mesai-kurum')?.value;
  const subeSelect = document.getElementById('admin-mesai-sube');
  if (!subeSelect) return;
  const kurum = kurumlarListesi.find(k => k.ad === kurumAd);
  const oncekiSube = subeSelect.value;
  subeSelect.innerHTML = '<option value="">Şube seçin...</option>' +
    (kurum?.subeler || []).map(s => `<option value="${s.ad}">${s.ad}</option>`).join('');
  subeSelect.disabled = !kurum?.subeler?.length;
  if (kurum?.subeler?.length) {
    subeSelect.value = kurum.subeler.some(s => s.ad === oncekiSube) ? oncekiSube : kurum.subeler[0].ad;
  }
  adminMesaiSubeSecildi();
}

function adminMesaiSubeSecildi() {
  const kurumAd = document.getElementById('admin-mesai-kurum')?.value;
  const subeAd = document.getElementById('admin-mesai-sube')?.value;
  const sube = kurumlarListesi.find(k => k.ad === kurumAd)?.subeler?.find(s => s.ad === subeAd);
  const gunler = Array.from(document.querySelectorAll('input[name="admin-mesai-gun"]'));
  gunler.forEach(cb => {
    cb.checked = false;
    document.getElementById(`admin-mesai-bas-${cb.value}`).value = '08:00';
    document.getElementById(`admin-mesai-bit-${cb.value}`).value = '18:00';
  });
  (sube?.calismaSaatleri || []).forEach(kayit => {
    const eslesme = kayit.match(/^([A-Za-z]+)\s+(\d{2}:\d{2})-(\d{2}:\d{2})$/);
    if (!eslesme) return;
    const cb = gunler.find(g => g.value === eslesme[1]);
    if (!cb) return;
    cb.checked = true;
    document.getElementById(`admin-mesai-bas-${cb.value}`).value = eslesme[2];
    document.getElementById(`admin-mesai-bit-${cb.value}`).value = eslesme[3];
  });
}

async function adminSubeMesaiKaydet(e) {
  e.preventDefault();
  const kurumAdi = document.getElementById('admin-mesai-kurum').value;
  const subeAdi = document.getElementById('admin-mesai-sube').value;
  const saatler = Array.from(document.querySelectorAll('input[name="admin-mesai-gun"]:checked')).map(cb => ({
    gun: cb.value,
    baslangicSaati: document.getElementById(`admin-mesai-bas-${cb.value}`).value,
    bitisSaati: document.getElementById(`admin-mesai-bit-${cb.value}`).value
  }));

  if (!kurumAdi || !subeAdi || !saatler.length) {
    toast('Lütfen kurum, şube ve en az bir çalışma günü seçin.', 'uyari');
    return;
  }
  if (saatler.some(s => !s.baslangicSaati || !s.bitisSaati || s.baslangicSaati >= s.bitisSaati)) {
    toast('Her gün için geçerli bir açılış ve kapanış saati girin.', 'uyari');
    return;
  }

  try {
    const res = await apiFetch('/api/yonetim/sube-calisma-saatleri', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ kurumAdi, subeAdi, saatler })
    });
    const data = await res.json();
    if (!res.ok || !data.basarili) {
      toast(data.mesaj || 'Çalışma saatleri kaydedilemedi.', 'hata');
      return;
    }
    toast(data.mesaj, 'basari');
    await kurumBilgileriniYukle();
  } catch (err) {
    toast('Hata: ' + err.message, 'hata');
  }
}

// 1. Doktor Ekleme Form Etkileşimleri
function adminDoktorKurumSecildi() {
  const kurumAd = document.getElementById('admin-doktor-kurum').value;
  const subeSelect = document.getElementById('admin-doktor-sube');
  const uzmanlikSelect = document.getElementById('admin-doktor-uzmanlik');
  const hizmetAlani = document.getElementById('admin-doktor-hizmetler-alani');

  if (!kurumAd) {
    subeSelect.innerHTML = '<option value="">Önce kurum seçin...</option>';
    subeSelect.disabled = true;
    uzmanlikSelect.innerHTML = '<option value="">Önce şube seçin...</option>';
    uzmanlikSelect.disabled = true;
    hizmetAlani.innerHTML = '<span>Lütfen yukarıdan kurum ve şube seçiniz. Şubeye ait resmi tıbbi işlemler burada listelenecektir.</span>';
    return;
  }

  const kurum = kurumlarListesi.find(k => k.ad === kurumAd);
  if (kurum && kurum.subeler && kurum.subeler.length > 0) {
    subeSelect.innerHTML = '<option value="">Şube Seçin...</option>' +
      kurum.subeler.map(s => `<option value="${s.ad}">${s.ad}</option>`).join('');
    subeSelect.disabled = false;
  } else {
    subeSelect.innerHTML = '<option value="">Bu kurumda henüz şube yok</option>';
    subeSelect.disabled = true;
  }

  uzmanlikSelect.innerHTML = '<option value="">Önce şube seçin...</option>';
  uzmanlikSelect.disabled = true;
  hizmetAlani.innerHTML = '<span>Lütfen şube seçiniz.</span>';
}

function adminDoktorSubeSecildi() {
  const kurumAd = document.getElementById('admin-doktor-kurum').value;
  const subeAd = document.getElementById('admin-doktor-sube').value;
  const uzmanlikSelect = document.getElementById('admin-doktor-uzmanlik');
  const hizmetAlani = document.getElementById('admin-doktor-hizmetler-alani');

  if (!subeAd) {
    uzmanlikSelect.innerHTML = '<option value="">Önce şube seçin...</option>';
    uzmanlikSelect.disabled = true;
    hizmetAlani.innerHTML = '<span>Lütfen şube seçiniz.</span>';
    return;
  }

  const kurum = kurumlarListesi.find(k => k.ad === kurumAd);
  const sube = kurum?.subeler?.find(s => s.ad === subeAd);

  // 1. Poliklinikleri Doldur
  const standartBolumler = [
    'Dahiliye (İç Hastalıkları)', 'Kardiyoloji', 'Göz Hastalıkları', 'Kulak Burun Boğaz (KBB)',
    'Ortopedi ve Travmatoloji', 'Dermatoloji (Cildiye)', 'Nöroloji', 'Genel Cerrahi',
    'Çocuk Sağlığı ve Hastalıkları', 'Kadın Hastalıkları ve Doğum', 'Üroloji', 'Göğüs Hastalıkları',
    'Fizik Tedavi ve Rehabilitasyon', 'Psikiyatri', 'Beslenme ve Diyetetik', 'Ağız ve Diş Sağlığı'
  ];

  const subeBolumleri = (sube && sube.bolumler && sube.bolumler.length > 0)
    ? sube.bolumler
    : standartBolumler;

  uzmanlikSelect.innerHTML = '<option value="">Uzmanlık / Bölüm Seçin...</option>' +
    subeBolumleri.map(b => `<option value="${b}">${b}</option>`).join('');
  uzmanlikSelect.disabled = false;

  // 2. Şubenin Tıbbi İşlemlerini Listele (Sadece bu kuruma ait işlemler)
  if (sube && sube.hizmetler && sube.hizmetler.length > 0) {
    hizmetAlani.innerHTML = sube.hizmetler.map(h => `
      <label class="flex items-center gap-2 p-2 bg-white rounded-lg border border-slate-200 cursor-pointer hover:border-brand-500 hover:bg-brand-50/30 transition shadow-xs">
        <input type="checkbox" name="admin-doktor-hizmet" value="${h.ad}" checked class="w-4 h-4 rounded text-brand-600 focus:ring-brand-500">
        <div>
          <span class="font-semibold text-slate-800 text-xs">${h.ad}</span>
          <span class="text-[11px] text-slate-500 ml-1">(${h.ucret} ₺ • ${h.sureDakika} dk)</span>
        </div>
      </label>
    `).join('');
  } else {
    hizmetAlani.innerHTML = `
      <div class="w-full p-3 bg-amber-50 border border-amber-200 rounded-xl text-amber-800 text-xs flex items-center justify-between">
        <span><i class="fa-solid fa-triangle-exclamation mr-1.5 text-amber-600"></i> Bu şubede henüz kayıtlı tıbbi işlem yok.</span>
        <button type="button" onclick="adminAltSekme('hizmet-ekle')" class="px-3 py-1 bg-amber-600 hover:bg-amber-700 text-white rounded-lg font-bold text-xs transition">
          İşlem Ekle
        </button>
      </div>
    `;
  }
}

async function adminYeniDoktorKaydet(e) {
  e.preventDefault();
  document.getElementById('admin-doktor-kayit-sonuc')?.classList.add('hidden');

  const kurumAdi = document.getElementById('admin-doktor-kurum').value;
  const subeAdi = document.getElementById('admin-doktor-sube').value;
  const ad = document.getElementById('admin-doktor-ad').value.trim();
  const soyad = document.getElementById('admin-doktor-soyad').value.trim();
  const rol = document.getElementById('admin-doktor-rol').value;
  const uzmanlikVeyaUnvan = document.getElementById('admin-doktor-uzmanlik').value;
  const sifre = document.getElementById('admin-doktor-sifre').value.trim();

  if (!kurumAdi || !subeAdi || !ad || !soyad || !uzmanlikVeyaUnvan) {
    toast('Lütfen tüm zorunlu alanları (Kurum, Şube, Ad, Soyad, Uzmanlık) doldurunuz.', 'uyari');
    return;
  }

  // Seçili Hizmetler (Yalnızca şubede tanımlı ve işaretlenen resmi işlemler)
  const seciliHizmetler = Array.from(document.querySelectorAll('input[name="admin-doktor-hizmet"]:checked'))
    .map(cb => cb.value.trim());

  if (seciliHizmetler.length === 0) {
    toast('Lütfen hekimin sunacağı en az bir tıbbi işlemi işaretleyiniz.', 'uyari');
    return;
  }

  // Seçili Günler
  const seciliGunler = Array.from(document.querySelectorAll('input[name="admin-gun"]:checked'))
    .map(cb => cb.value);

  const baslangicSaati = document.getElementById('admin-doktor-mesai-bas').value || '09:00';
  const bitisSaati = document.getElementById('admin-doktor-mesai-bit').value || '17:00';

  if (!seciliGunler.length) {
    toast('Lütfen en az bir çalışma günü seçin.', 'uyari');
    return;
  }

  const istek = {
    kurumAdi,
    subeAdi,
    ad,
    soyad,
    rol,
    uzmanlikVeyaUnvan,
    sifre,
    hizmetAdlari: seciliHizmetler,
    gunler: seciliGunler,
    baslangicSaati,
    bitisSaati
  };

  try {
    const res = await apiFetch('/api/yonetim/doktor-ekle', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(istek)
    });
    let data = null;
    try { data = await res.json(); } catch {}

    if (!res.ok || !data?.basarili) {
      if (res.status === 404) {
        toast('Sunucu güncel değil. Lütfen siyah konsol penceresini (calistir.bat) kapatıp tekrar açınız.', 'hata');
        return;
      }
      toast(data?.mesaj || 'Doktor kaydedilemedi!', 'hata');
      return;
    }

    document.getElementById('admin-doktor-kayit-kimlik').textContent = data.kimlik || '';
    document.getElementById('admin-doktor-kayit-sonuc').classList.remove('hidden');
    toast(`Dr. ${data.adSoyad} eklendi. Giriş kimliği: ${data.kimlik}`, 'basari');
    await kurumBilgileriniYukle();

    // Formu sıfırla
    e.target.reset();
    adminAltSekme('doktor-ekle');
  } catch (err) {
    toast('Hata oluştu: ' + err.message, 'hata');
  }
}

async function doktorKimliginiKopyala() {
  const kimlik = document.getElementById('admin-doktor-kayit-kimlik')?.textContent;
  if (!kimlik) return;
  try {
    await navigator.clipboard.writeText(kimlik);
    toast('Doktor giriş kimliği kopyalandı.', 'basari');
  } catch {
    toast(`Doktor giriş kimliği: ${kimlik}`, 'bilgi');
  }
}

// 2. Hizmet Ekleme Form Etkileşimleri
function adminHizmetKurumSecildi() {
  const kurumAd = document.getElementById('admin-hizmet-kurum').value;
  const subeSelect = document.getElementById('admin-hizmet-sube');
  const bolumSelect = document.getElementById('admin-hizmet-bolum');

  if (!kurumAd) {
    subeSelect.innerHTML = '<option value="">Önce kurum seçin...</option>';
    subeSelect.disabled = true;
    bolumSelect.innerHTML = '<option value="">Önce şube seçin...</option>';
    bolumSelect.disabled = true;
    return;
  }

  const kurum = kurumlarListesi.find(k => k.ad === kurumAd);
  if (kurum && kurum.subeler && kurum.subeler.length > 0) {
    subeSelect.innerHTML = '<option value="">Şube Seçin...</option>' +
      kurum.subeler.map(s => `<option value="${s.ad}">${s.ad}</option>`).join('');
    subeSelect.disabled = false;
  } else {
    subeSelect.innerHTML = '<option value="">Bu kurumda henüz şube yok</option>';
    subeSelect.disabled = true;
  }

  bolumSelect.innerHTML = '<option value="">Önce şube seçin...</option>';
  bolumSelect.disabled = true;
}

function adminHizmetSubeSecildi() {
  const kurumAd = document.getElementById('admin-hizmet-kurum').value;
  const subeAd = document.getElementById('admin-hizmet-sube').value;
  const bolumSelect = document.getElementById('admin-hizmet-bolum');

  if (!subeAd) {
    bolumSelect.innerHTML = '<option value="">Önce şube seçin...</option>';
    bolumSelect.disabled = true;
    return;
  }

  const kurum = kurumlarListesi.find(k => k.ad === kurumAd);
  const sube = kurum?.subeler?.find(s => s.ad === subeAd);

  const standartBolumler = [
    'Dahiliye (İç Hastalıkları)', 'Kardiyoloji', 'Göz Hastalıkları', 'Kulak Burun Boğaz (KBB)',
    'Ortopedi ve Travmatoloji', 'Dermatoloji (Cildiye)', 'Nöroloji', 'Genel Cerrahi'
  ];

  const bolumler = (sube && sube.bolumler && sube.bolumler.length > 0)
    ? sube.bolumler
    : standartBolumler;

  bolumSelect.innerHTML = '<option value="">Poliklinik Seçin (Opsiyonel)...</option>' +
    bolumler.map(b => `<option value="${b}">${b}</option>`).join('');
  bolumSelect.disabled = false;
}

async function adminYeniHizmetKaydet(e) {
  e.preventDefault();

  const kurumAdi = document.getElementById('admin-hizmet-kurum').value;
  const subeAdi = document.getElementById('admin-hizmet-sube').value;
  const bolumAdi = document.getElementById('admin-hizmet-bolum').value;
  const hizmetAdi = document.getElementById('admin-hizmet-ad').value.trim();
  const sureDakika = parseInt(document.getElementById('admin-hizmet-sure').value);
  const ucret = parseFloat(document.getElementById('admin-hizmet-ucret').value);

  if (!kurumAdi || !subeAdi || !hizmetAdi) {
    toast('Lütfen Kurum, Şube ve Tıbbi İşlem Adı alanlarını doldurunuz.', 'uyari');
    return;
  }

  const istek = {
    kurumAdi,
    subeAdi,
    bolumAdi: bolumAdi || null,
    hizmetAdi,
    sureDakika: isNaN(sureDakika) ? 30 : sureDakika,
    ucret: isNaN(ucret) ? 500 : ucret
  };

  try {
    const res = await apiFetch('/api/yonetim/hizmet-ekle', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(istek)
    });
    let data = null;
    try { data = await res.json(); } catch {}

    if (!res.ok || !data?.basarili) {
      if (res.status === 404) {
        toast('Sunucu güncel değil. Lütfen siyah konsol penceresini (calistir.bat) kapatıp tekrar açınız.', 'hata');
        return;
      }
      toast(data?.mesaj || 'Hizmet eklenemedi!', 'hata');
      return;
    }

    toast(`✓ '${hizmetAdi}' işlemi veritabanına ve ${subeAdi} şubesine eklendi!`, 'basari');
    await kurumBilgileriniYukle();

    e.target.reset();
    adminAltSekme('doktor-ekle');
    setTimeout(() => {
      const dKurum = document.getElementById('admin-doktor-kurum');
      if (dKurum) {
        dKurum.value = kurumAdi;
        adminDoktorKurumSecildi();
        const dSube = document.getElementById('admin-doktor-sube');
        if (dSube) {
          dSube.value = subeAdi;
          adminDoktorSubeSecildi();
          if (bolumAdi) {
            const dUzmanlik = document.getElementById('admin-doktor-uzmanlik');
            if (dUzmanlik) dUzmanlik.value = bolumAdi;
          }
        }
      }
    }, 100);
  } catch (err) {
    toast('Hata oluştu: ' + err.message, 'hata');
  }
}

// 3. Bölüm / Poliklinik Ekleme
function adminBolumKurumSecildi() {
  const kurumAd = document.getElementById('admin-bolum-kurum').value;
  const subeSelect = document.getElementById('admin-bolum-sube');

  if (!kurumAd) {
    subeSelect.innerHTML = '<option value="">Önce kurum seçin...</option>';
    subeSelect.disabled = true;
    return;
  }

  const kurum = kurumlarListesi.find(k => k.ad === kurumAd);
  if (kurum && kurum.subeler && kurum.subeler.length > 0) {
    subeSelect.innerHTML = '<option value="">Şube Seçin...</option>' +
      kurum.subeler.map(s => `<option value="${s.ad}">${s.ad}</option>`).join('');
    subeSelect.disabled = false;
  } else {
    subeSelect.innerHTML = '<option value="">Bu kurumda henüz şube yok</option>';
    subeSelect.disabled = true;
  }
}

async function adminYeniBolumKaydet(e) {
  e.preventDefault();

  const kurumAdi = document.getElementById('admin-bolum-kurum').value;
  const subeAdi = document.getElementById('admin-bolum-sube').value;
  const bolumAdi = document.getElementById('admin-bolum-ad').value.trim();

  if (!kurumAdi || !subeAdi || !bolumAdi) {
    toast('Lütfen Kurum, Şube ve Bölüm Adı alanlarını doldurunuz.', 'uyari');
    return;
  }

  const istek = { kurumAdi, subeAdi, bolumAdi };

  try {
    const res = await apiFetch('/api/yonetim/bolum-ekle', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(istek)
    });
    let data = null;
    try { data = await res.json(); } catch {}

    if (!res.ok || !data?.basarili) {
      if (res.status === 404) {
        toast('Sunucu güncel değil. Lütfen siyah konsol penceresini (calistir.bat) kapatıp tekrar açınız.', 'hata');
        return;
      }
      toast(data?.mesaj || 'Bölüm eklenemedi!', 'hata');
      return;
    }

    toast(`✓ '${bolumAdi}' polikliniği ${subeAdi} şubesine başarıyla eklendi!`, 'basari');
    await kurumBilgileriniYukle();

    document.getElementById('admin-bolum-ad').value = '';
    
    // Tıbbi İşlem Ekleme sekmesine geç ve eklenen kurum & şubeyi hazır seç
    adminAltSekme('hizmet-ekle');
    setTimeout(() => {
      const hKurum = document.getElementById('admin-hizmet-kurum');
      if (hKurum) {
        hKurum.value = kurumAdi;
        adminHizmetKurumSecildi();
        const hSube = document.getElementById('admin-hizmet-sube');
        if (hSube) {
          hSube.value = subeAdi;
          adminHizmetSubeSecildi();
          const hBolum = document.getElementById('admin-hizmet-bolum');
          if (hBolum) hBolum.value = bolumAdi;
        }
      }
    }, 100);
  } catch (err) {
    toast('Hata oluştu: ' + err.message, 'hata');
  }
}

// 4. Yeni Kurum & Şube Ekleme
async function adminYeniKurumKaydet(e) {
  e.preventDefault();

  const kurumAdi = document.getElementById('admin-yeni-kurum-ad').value.trim();
  const tur = document.getElementById('admin-yeni-kurum-tur').value;
  const subeAdi = document.getElementById('admin-yeni-sube-ad').value.trim();
  const adres = document.getElementById('admin-yeni-sube-adres').value.trim();

  if (!kurumAdi || !subeAdi) {
    toast('Lütfen Kurum Adı ve İlk Şube Adı alanlarını doldurunuz.', 'uyari');
    return;
  }

  const istek = { kurumAdi, tur, subeAdi, adres: adres || null };

  try {
    const res = await apiFetch('/api/yonetim/kurum-ekle', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(istek)
    });
    let data = null;
    try { data = await res.json(); } catch {}

    if (!res.ok || !data?.basarili) {
      if (res.status === 404) {
        toast('Sunucu güncel değil. Lütfen açık olan siyah konsol penceresini (calistir.bat) kapatıp tekrar açınız.', 'hata');
        return;
      }
      toast(data?.mesaj || 'Kurum eklenemedi!', 'hata');
      return;
    }

    toast(`✓ '${kurumAdi}' (${subeAdi}) veritabanına ve sisteme başarıyla kaydedildi!`, 'basari');
    await kurumBilgileriniYukle();

    e.target.reset();

    // Kurum eklendikten sonra doğrudan Poliklinik Ekleme sekmesine geç ve yeni kurumu seç
    adminAltSekme('bolum-ekle');
    await adminKurumDropdownlariniDoldur();
    const bKurum = document.getElementById('admin-bolum-kurum');
    const yeniKurum = kurumlarListesi.find(k =>
      k.ad.localeCompare(kurumAdi, 'tr', { sensitivity: 'base' }) === 0
    );
    if (bKurum && yeniKurum) {
      bKurum.value = yeniKurum.ad;
      adminBolumKurumSecildi();
      const bSube = document.getElementById('admin-bolum-sube');
      if (bSube) bSube.value = subeAdi;
    } else {
      toast('Kurum kaydedildi ancak seçim listesi yenilenemedi. Sayfayı yenileyip tekrar deneyin.', 'uyari');
    }
  } catch (err) {
    toast('Hata oluştu: ' + err.message, 'hata');
  }
}

