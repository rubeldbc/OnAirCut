<div align="center">

# OnAirCut

**Semi-Real-Time Newsroom Clipping & Ad-Insertion Pipeline**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-0078D4?logo=windows)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![FFmpeg](https://img.shields.io/badge/Video-FFmpeg-007808?logo=ffmpeg)](https://ffmpeg.org/)
[![EasyOCR](https://img.shields.io/badge/OCR-EasyOCR-FF6F00?logo=python)](https://github.com/JaidedAI/EasyOCR)
[![License](https://img.shields.io/badge/License-Proprietary-red)]()

*Transform on-air TV news broadcasts into upload-ready digital clips with automated ad insertion and Bengali OCR titling — within 2-3 minutes of live broadcast.*

</div>

---

## Overview

OnAirCut is a production pipeline designed for TV newsrooms to rapidly produce digital-ready video clips from live broadcasts. It captures news stories in real-time, inserts advertisements (TVC mid-rolls + overlay graphics), extracts Bengali headline text via AI-powered OCR for automatic file naming, and delivers upload-ready videos for YouTube, Facebook, and TikTok.

The system consists of two applications that work together over a shared network folder:

| Application | Purpose | Runs On |
|:---|:---|:---|
| **OnAirCut Recorder** | Operator monitors live feed, clips stories, captures text, selects ads, submits render jobs | Recorder PC |
| **OnAirCut Render Server** | Automatically processes clips: FFmpeg rendering, frame extraction, OCR naming, output organization | Render PC |

<div align="center">

```
[Recorder PC]                         [Render PC]
 OnAirCut Recorder                   OnAirCut Render Server
      │                                     │
      │      Shared Network Folder          │
      ├───── (\\MediaShare\OnAirCut) ──────┤
      │   clips, jobs, ads, output          │
      │                                     │
      │       REST API (port 5123)          │
      └───── history/search/stats ─────────┘
```

</div>

## Key Features

### Recorder
- **3 Input Sources** — Live capture card (Blackmagic/DeckLink), local video file, YouTube live URL
- **Real-Time Preview** — LibVLC-powered video player with audio monitoring and VU meters
- **Manual Story Clipping** — Record start/stop with instant clip extraction
- **Bengali OCR Text Capture** — One-click headline capture using EasyOCR with persistent Python server (~1-2 sec per capture)
- **Ad Set Selection** — Choose TVC mid-roll + overlay per clip from shared folder
- **Pending Clips Queue** — Record next story immediately without waiting for job submission
- **OCR Profile Management** — Visual marquee selection for OCR crop region, shared profiles between apps
- **History & Search** — View processed stories with status, search by title/date

### Render Server
- **Automatic Job Processing** — File-based queue with FileSystemWatcher + polling
- **FFmpeg Render Pipeline** — TVC insertion (fixed/midpoint), overlay (PNG/MOV/WebM), codec configuration
- **Frame Extraction** — 20 evenly-spaced thumbnail frames per clip
- **Multi-Frame OCR** — Bengali text detection for automatic file/folder naming
- **Output Organization** — Date-based folders, OCR-named files, metadata.json, thumbnail frames
- **SQLite Database** — Metadata storage with full search capability
- **Dashboard & Monitoring** — Real-time stats, queue management, job detail view, logs

### Shared
- **Material Design UI** — Dark theme, fullscreen, professional layout
- **MVVM Architecture** — CommunityToolkit.Mvvm with dependency injection
- **Dependency Manager** — Built-in downloader for FFmpeg, yt-dlp, Python, EasyOCR, Tesseract
- **Portable Deployment** — Copy folder to any Windows 10/11 PC, no installation required
- **Embedded Bengali Font** — Noto Sans Bengali bundled for consistent text display

## Screenshots

<details>
<summary>Click to expand screenshots</summary>

### Recorder — Main Interface
```
+------------------------------------------------------------------+
|  OnAirCut Recorder  [⚙]  [● Folder:OK]  [📹 Source]  [🕐 Clock] |
+------------------------------------------------------------------+
|  Source    |                            |  Ad Set              |
|  [Live]   |      Video Preview         |  (○) No Ad           |
|  [File]   |      + VU Meters           |  (○) Set_A           |
|  [YouTube]|      + Transport Controls  |  (○) Set_B           |
|  [OCR ▼]  |                            |  Pending Clips (3)   |
+-----------|---[●REC] [T] বাংলা টেক্সট--|--00:01:45---[⏳2]----+
|  History: Search | Date | Status                                |
+------------------------------------------------------------------+
```

### Render Server — Dashboard
```
+------------------------------------------------------------------+
|  OnAirCut Render Server  [Folder:OK]  [Pending: 3]  [Clock]     |
+------------------------------------------------------------------+
| 📊 Dashboard | ▶ Queue | 📜 History | ⚙ Settings | 📝 Logs     |
+------------------------------------------------------------------+
|  [Pending: 3] [Active: 1] [Done: 47] [Failed: 2]               |
|  Current: JOB-001 — Rendering (62%) — Speed: 1.8x — ETA: 12s   |
|  [========================>              ] 62%                    |
+------------------------------------------------------------------+
```

</details>

## Architecture

```
OnAirCut/
├── OnAirCut Recorder/
│   ├── OnAirCut Recorder.sln
│   └── src/
│       ├── OnAirCut.Core/          # Shared models, interfaces, utilities
│       │   ├── Constants/          # FolderNames, FileExtensions, AppPaths
│       │   ├── Enums/              # JobStatus, SourceType, InsertMode
│       │   ├── Interfaces/         # IVideoSource, IAdSetProvider, IOcrProfileProvider
│       │   ├── Models/             # JobFile, AdSetConfig, OcrProfile, ProcessedStory
│       │   └── Utilities/          # TitleSanitizer, JobFileHelper, SharedFolderValidator
│       └── OnAirCut.Recorder/      # WPF Recorder application
│           ├── ViewModels/         # MVVM ViewModels
│           ├── Views/              # XAML Views
│           ├── Services/           # Video sources, OCR, job submission
│           ├── Models/             # RecorderSettings, DependencyItem
│           ├── Converters/         # WPF value converters
│           ├── Resources/          # Icons, embedded fonts
│           └── lib/                # Runtime dependencies
│               ├── ffmpeg/         # FFmpeg + FFprobe
│               ├── python/         # Portable Python + EasyOCR
│               ├── ocr/            # OCR scripts + Bengali models
│               ├── tessdata/       # Tesseract fallback data
│               └── yt-dlp/         # YouTube stream resolver
│
├── OnAirCut Server/
│   ├── OnAirCut Server.sln
│   └── src/
│       └── OnAirCut.RenderServer/  # WPF Render Server application
│           ├── ViewModels/         # Dashboard, Queue, History, Settings
│           ├── Views/              # Tab-based UI
│           ├── Services/           # FFmpeg, OCR, Queue, Pipeline orchestrator
│           └── lib/                # Same dependency structure as Recorder
│
├── USER_GUIDE.md                   # English user guide
├── OnAirCut_User_Guide_Bengali.pdf # Bengali user guide (বাংলা)
├── MASTER_WORKFLOW.md              # 25-step development plan
└── poc_results.md                  # FFmpeg/OCR command reference
```

## Tech Stack

| Layer | Technology |
|:---|:---|
| **Framework** | .NET 8, WPF |
| **UI** | Material Design in XAML Toolkit (Dark Theme) |
| **MVVM** | CommunityToolkit.Mvvm (Source Generators) |
| **Video Playback** | LibVLCSharp + LibVLC |
| **Video Processing** | FFmpeg (encoding, TVC insertion, overlay, frame extraction) |
| **Audio Monitoring** | NAudio |
| **OCR (Primary)** | EasyOCR (Python, Bengali + English, persistent server) |
| **OCR (Fallback)** | Tesseract 5 (C# NuGet) |
| **Database** | SQLite (Microsoft.Data.Sqlite) |
| **YouTube** | yt-dlp (stream URL resolution) |
| **Process Management** | CliWrap |
| **Logging** | Serilog |
| **Font** | Noto Sans Bengali (embedded) |

## Getting Started

### Prerequisites

- **OS**: Windows 10/11 (64-bit)
- **.NET 8 Runtime**: [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Python 3.8+**: Required for EasyOCR (or use the built-in Dependency Manager to install portable Python)

### Build from Source

```bash
# Clone
git clone https://github.com/rubeldbc/OnAirCut.git
cd OnAirCut

# Build Recorder
cd "OnAirCut Recorder"
dotnet build "OnAirCut Recorder.sln"

# Build Server
cd "../OnAirCut Server"
dotnet build "OnAirCut Server.sln"
```

### Install Dependencies

Launch either app → **Settings** → **General** tab → **Dependencies** section:

Click **"Download All Missing"** to automatically download:
| Dependency | Size | Purpose |
|:---|:---|:---|
| FFmpeg + FFprobe | ~190 MB | Video recording & rendering |
| yt-dlp | ~18 MB | YouTube live stream support |
| Tesseract Data (Bengali) | ~11 MB | Fallback OCR |
| Tesseract Data (English) | ~15 MB | Mixed text OCR |
| Python + EasyOCR | ~1.3 GB | High-accuracy Bengali OCR |

### Quick Start

1. **Set up shared folder**: Settings → Shared Folder → Browse → **Create Folder Structure**
2. **Connect a source**: Select YouTube URL → paste a live news link → Connect
3. **Configure OCR region**: Settings → OCR Region → Capture Frame → draw rectangle over headline area → Save Region
4. **Start clipping**: Press **Record** (F5) when story starts, **Stop** (F6) when it ends
5. **Capture text**: Press the **T** button to OCR the headline
6. **Submit**: Select an ad set → Submit to render queue

## Shared Folder Structure

```
\\MediaShare\OnAirCut\
├── Assets/
│   ├── AdSets/              # TVC videos + overlay graphics + config.json
│   └── OcrProfiles/         # OCR crop region profiles (shared)
├── Ingest/RawClips/{date}/  # Recorded raw clips
├── Jobs/
│   ├── Pending/             # New render jobs (JSON)
│   ├── Processing/          # Currently rendering
│   ├── Done/                # Completed
│   └── Failed/              # Errors
├── Output/{date}/{title}/   # Final videos + frames + metadata
├── Working/                 # Temp render workspace
└── Logs/                    # Shared logs
```

## Render Pipeline

```
Job Submitted → File Ready Check → FFprobe Metadata
     ↓
Frame Extraction (20 frames) ──→ OCR (Bengali text detection)
     ↓                                    ↓
FFmpeg Render ────────────────→ Output Organization
  • Split at TVC insertion point           • OCR-named folder
  • Concat: part1 + TVC + part2           • Renamed video file
  • Apply overlay (PNG/MOV)                • 20 thumbnail frames
  • Encode (h264 + AAC)                    • metadata.json
     ↓
SQLite Database Update → Job Complete
```

## Keyboard Shortcuts (Recorder)

| Key | Action |
|:---|:---|
| `F5` | Start Recording |
| `F6` | Stop Recording |
| `Escape` | Cancel Current Clip |
| `F8` | Submit Job |
| `Ctrl+F` | Focus Search |
| `Space` | Play/Pause |
| `Click` captured text | Copy to clipboard |
| `Ctrl+Click` captured text | Open file in Explorer |

## Configuration

### Ad Set (config.json)

```json
{
  "name": "Set_A",
  "tvcFile": "tvc.mp4",
  "overlayFile": "overlay.png",
  "insertMode": "Midpoint",
  "insertAtSec": 0,
  "overlayStartSec": 0,
  "overlayEndSec": 9999,
  "outputWidth": 1920,
  "outputHeight": 1080
}
```

**Insert Modes**: `None` (overlay only), `FixedTimestamp`, `Midpoint`
**Overlay Types**: `.png` (static), `.mov` (ProRes 4444 alpha), `.webm` (VP9 alpha)

## Documentation

- [English User Guide](USER_GUIDE.md)
- [Bengali User Guide (বাংলা)](OnAirCut_User_Guide_Bengali.pdf)
- [Development Workflow](MASTER_WORKFLOW.md)
- [FFmpeg/OCR Reference](poc_results.md)

## Contributing

This is a proprietary newsroom production tool. For feature requests or bug reports, please open an issue.

## License

Proprietary — All rights reserved.

---

<div align="center">

**Built for newsrooms that need speed.**

*On-air to upload-ready in under 3 minutes.*

</div>
