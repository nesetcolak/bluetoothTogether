<div align="center">

<img src="app_icon.ico" width="80" height="80" alt="BluetoothTogether Icon"/>

# BluetoothTogether

[![Release](https://img.shields.io/github/v/release/nesetcolak/bluetoothTogether?style=flat-square&color=00d4ff)](https://github.com/nesetcolak/bluetoothTogether/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue?style=flat-square)](https://github.com/nesetcolak/bluetoothTogether)
[![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)](LICENSE)

<br/>

🇹🇷 [Türkçe](#türkçe) &nbsp;|&nbsp; 🇺🇸 [English](#english)

</div>

---

<br/>

# Türkçe

**Aynı anda birden fazla ses cihazına müzik yönlendirin.**

Windows'ta tek bir ses kaynağını birden fazla Bluetooth kulaklığa veya hoparlöre aynı anda aktarın.

## Ne İşe Yarar?

Windows normalde sesi aynı anda tek bir cihaza gönderebilir. BluetoothTogether bu sınırı aşar:

- 🎧 İki Bluetooth kulaklığa aynı anda ses gönder
- 🔊 Kulaklık + hoparlör kombinasyonu kullan
- 🔇 Windows ses seviyesini değiştirince tüm cihazlar senkronize olur
- 🔁 Uygulama kapatılınca ses otomatik eski cihaza geri döner

## Kurulum

### ✅ Hazır .exe ile (Önerilen)

1. **[Releases](https://github.com/nesetcolak/bluetoothTogether/releases)** sayfasına git
2. En son sürümün altındaki `BluetoothTogether.exe` dosyasını indir
3. **VB-Audio Virtual Cable** kur → [vb-audio.com/Cable](https://vb-audio.com/Cable/) *(ücretsiz)*
4. `BluetoothTogether.exe` dosyasını çalıştır

> ⚠️ Windows SmartScreen uyarısı çıkabilir — "More info" → "Run anyway" de. Açık kaynak kodlu uygulama olduğu için imzasız çalışır.

### 🛠️ Kaynak Koddan Derle

1. Repoyu klonla:
   ```bash
   git clone https://github.com/nesetcolak/bluetoothTogether.git
   ```
2. Visual Studio 2022 ile `bluetoothTogetherForms.sln` dosyasını aç
3. NuGet paketlerini yükle: `NAudio`, `AudioSwitcher.AudioApi.CoreAudio`
4. `Release` modunda derle

**Gereksinimler:** .NET 8, Windows 10/11, Visual Studio 2022

## Kullanım

1. VB-Audio Virtual Cable'ın kurulu olduğundan emin ol
2. BluetoothTogether'ı aç
3. Listeden sesi göndermek istediğin cihazları seç ✓
4. **BAŞLAT** butonuna bas
5. Sağ alt tepsi ikonundan arka planda çalışmaya devam eder — kapatmak için sağ tık → **Çıkış**

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

## Gereksinimler

| Gereksinim | Açıklama |
|---|---|
| Windows 10/11 | x64 |
| .NET 8 Runtime | [İndir](https://dotnet.microsoft.com/download/dotnet/8.0) |
| VB-Audio Virtual Cable | [İndir](https://vb-audio.com/Cable/) — Ücretsiz |

## SSS

**Ses gecikmesi var, ne yapmalıyım?**
Bluetooth kulaklıkların kendi doğal gecikmeleri vardır. Uygulama kaynaklı gecikme minimumdur (20ms buffer).

**VB-Audio Virtual Cable ücretli mi?**
Hayır, tamamen ücretsizdir.

**Uygulama kapatılınca ses nereye gider?**
Otomatik olarak başlamadan önceki varsayılan ses cihazına geri döner.

**İkiden fazla cihaza gönderebilir miyim?**
Evet, listeden istediğin kadar cihaz seçebilirsin.

## Katkı

Pull request'ler açıktır. Büyük değişiklikler için önce bir issue açmanı öneririm.

## Yapım Süreci

Bu uygulama tek satır kod yazmadan, yapay zeka ile geliştirilmiştir.

- 💬 Fikir, yönlendirme ve tasarım kararları: [Neşet Çolak](https://github.com/nesetcolak)
- 🤖 Kod yazımı: [Google Gemini](https://gemini.google.com) & [Anthropic Claude](https://claude.ai)

> Bir insan ne isteyeceğini bilirse, yapay zeka nasıl yapılacağını bilir.

<br/>

---

<br/>

# English

**Route audio to multiple devices simultaneously.**

Send audio from a single source to multiple Bluetooth headphones or speakers at the same time on Windows.

## What Does It Do?

Windows normally only sends audio to one device at a time. BluetoothTogether removes that limitation:

- 🎧 Stream to two Bluetooth headphones simultaneously
- 🔊 Use headphones + speaker at the same time
- 🔇 Windows volume changes sync across all devices
- 🔁 Audio automatically returns to the original device on exit

## Installation

### ✅ Ready-to-use .exe (Recommended)

1. Go to the **[Releases](https://github.com/nesetcolak/bluetoothTogether/releases)** page
2. Download `BluetoothTogether.exe` from the latest release
3. Install **VB-Audio Virtual Cable** → [vb-audio.com/Cable](https://vb-audio.com/Cable/) *(free)*
4. Run `BluetoothTogether.exe`

> ⚠️ Windows SmartScreen may show a warning — click "More info" → "Run anyway". The app is unsigned because it's open source.

### 🛠️ Build from Source

1. Clone the repo:
   ```bash
   git clone https://github.com/nesetcolak/bluetoothTogether.git
   ```
2. Open `bluetoothTogetherForms.sln` in Visual Studio 2022
3. Install NuGet packages: `NAudio`, `AudioSwitcher.AudioApi.CoreAudio`
4. Build in `Release` mode

**Requirements:** .NET 8, Windows 10/11, Visual Studio 2022

## Usage

1. Make sure VB-Audio Virtual Cable is installed
2. Open BluetoothTogether
3. Select the output devices you want to route audio to ✓
4. Press **START**
5. The app runs in the system tray — right click the tray icon → **Exit** to close completely

## How It Works

```
Windows Audio System
        │
        ▼
 VB-Audio Virtual Cable  ◄── BluetoothTogether listens here
        │
        ├──► Bluetooth Headphones 1
        ├──► Bluetooth Headphones 2
        └──► Speaker / Other devices
```

## Requirements

| Requirement | Details |
|---|---|
| Windows 10/11 | x64 |
| .NET 8 Runtime | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| VB-Audio Virtual Cable | [Download](https://vb-audio.com/Cable/) — Free |

## FAQ

**There's audio latency, what should I do?**
Bluetooth headphones have inherent latency by nature. App-side latency is minimal (20ms buffer).

**Is VB-Audio Virtual Cable free?**
Yes, completely free. You can optionally donate to the developer.

**Where does audio go when I close the app?**
It automatically returns to whatever your default audio device was before.

**Can I route to more than two devices?**
Yes, you can select as many devices as you want from the list.

## Contributing

Pull requests are welcome. For major changes, please open an issue first.

## Development Process

This application was built entirely with AI — not a single line of code was written by hand.

- 💬 Idea, direction and design decisions: [Neşet Çolak](https://github.com/nesetcolak)
- 🤖 Code generation: [Google Gemini](https://gemini.google.com) & [Anthropic Claude](https://claude.ai)

> If a human knows what to build, AI knows how to build it.

<br/>

---

<div align="center">
  <sub>Built for Windows · Powered by VB-Audio Virtual Cable · Made with Google Gemini & Anthropic Claude</sub>
</div>
