<div align="center">

<img src="app_icon.ico" width="80" height="80" alt="BluetoothTogether Icon"/>

# BluetoothTogether

**Aynı anda birden fazla ses cihazına müzik yönlendirin.**

Windows'ta tek bir ses kaynağını birden fazla Bluetooth kulaklığa veya hoparlöre aynı anda aktarın.

[![Release](https://img.shields.io/github/v/release/nesetcolak/bluetoothTogether?style=flat-square&color=00d4ff)](https://github.com/nesetcolak/bluetoothTogether/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue?style=flat-square)](https://github.com/nesetcolak/bluetoothTogether)
[![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)](LICENSE)

</div>

---

## Ne İşe Yarar?

Windows normalde sesi aynı anda tek bir cihaza gönderebilir. BluetoothTogether bu sınırı aşar:

- 🎧 İki Bluetooth kulaklığa aynı anda ses gönder
- 🔊 Kulaklık + hoparlör kombinasyonu kullan
- 🔇 Windows ses seviyesini değiştirince tüm cihazlar senkronize olur
- 🔁 Uygulama kapatılınca ses otomatik eski cihaza geri döner

---

## Kurulum

### ✅ Hazır .exe ile (Önerilen)

Kod yazmak istemiyorsan direkt çalıştırılabilir dosyayı indir:

1. **[Releases](https://github.com/nesetcolak/bluetoothTogether/releases)** sayfasına git
2. En son sürümün altındaki `BluetoothTogether.exe` dosyasını indir
3. **VB-Audio Virtual Cable** kur → [vb-audio.com/Cable](https://vb-audio.com/Cable/) *(ücretsiz)*
4. `BluetoothTogether.exe` dosyasını çalıştır

> ⚠️ Windows SmartScreen uyarısı çıkabilir — "More info" → "Run anyway" de. Açık kaynak kodlu uygulama olduğu için imzasız çalışır.

---

### 🛠️ Kaynak Koddan Derle

1. Bu repoyu klonla:
   ```bash
   git clone https://github.com/nesetcolak/bluetoothTogether.git
   ```

2. Visual Studio 2022 ile `bluetoothTogetherForms.sln` dosyasını aç

3. NuGet paketlerini yükle (otomatik gelir, yoksa):
   ```
   NAudio
   AudioSwitcher.AudioApi.CoreAudio
   ```

4. `Release` modunda derle → `bin/Release/net8.0-windows/` klasöründe `.exe` oluşur

**Gereksinimler:** .NET 8, Windows 10/11, Visual Studio 2022

---

## Kullanım

1. **VB-Audio Virtual Cable**'ın kurulu olduğundan emin ol
2. BluetoothTogether'ı aç
3. Listeden sesi göndermek istediğin cihazları seç ✓
4. **BAŞLAT** butonuna bas
5. Artık Windows üzerinden çalan her ses seçili tüm cihazlara gider

Uygulamayı kapattığında ses otomatik olarak orijinal cihazına geri döner.

> Sağ alt köşedeki sistem tepsisinde çalışmaya devam eder. Tamamen kapatmak için tepsi ikonuna sağ tık → **Çıkış**.

---

## Nasıl Çalışır?

```
Windows Ses Sistemi
        │
        ▼
 VB-Audio Virtual Cable  ◄── BluetoothTogether bunu dinler
        │
        ├──► Bluetooth Kulaklık 1
        ├──► Bluetooth Kulaklık 2
        └──► Hoparlör / Diğer cihazlar
```

BluetoothTogether, **VB-Audio Virtual Cable**'ı varsayılan ses çıkışı olarak ayarlar. Bu sanal cihazdan gelen sesi gerçek zamanlı olarak seçtiğin tüm fiziksel cihazlara kopyalar.

---

## Gereksinimler

| Gereksinim | Açıklama |
|---|---|
| Windows 10/11 | x64 |
| .NET 8 Runtime | [İndir](https://dotnet.microsoft.com/download/dotnet/8.0) |
| VB-Audio Virtual Cable | [İndir](https://vb-audio.com/Cable/) — Ücretsiz |

---

## Sık Sorulan Sorular

**Ses gecikmesi var, ne yapmalıyım?**
Bluetooth kulaklıkların kendi doğal gecikmeleri vardır. Uygulama kaynaklı gecikme minimumdur (20ms buffer).

**VB-Audio Virtual Cable ücretli mi?**
Hayır, tamamen ücretsizdir. İsteğe bağlı bağış yapabilirsin.

**Uygulama kapatılınca ses nereye gider?**
Otomatik olarak başlamadan önceki varsayılan ses cihazına geri döner.

**İkiden fazla cihaza gönderebilir miyim?**
Evet, listeden istediğin kadar cihaz seçebilirsin.

---

## Katkı

Pull request'ler açıktır. Büyük değişiklikler için önce bir issue açmanı öneririm.

---

<div align="center">
  <sub>Windows için yapıldı · VB-Audio Virtual Cable ile çalışır</sub>
</div>
