# OnAirCut — User Guide

> Semi-real-time newsroom clipping and ad-insertion pipeline.
> Produces upload-ready news clips within 2-3 minutes of on-air broadcast.

---

## System Overview

OnAirCut consists of two applications:

| Application | Purpose | Runs On |
|---|---|---|
| **OnAirCut Recorder** | Operator monitors live/recorded news feed, clips stories, selects ads, submits jobs | Recorder PC |
| **OnAirCut Render Server** | Automatically processes clips: renders with ads, extracts frames, OCR names files | Render PC |

Both PCs must be on the same network with access to a **shared network folder**.

---

## Prerequisites

### System Requirements
- **OS**: Windows 10/11 (64-bit)
- **.NET 8 Runtime**: Download from https://dotnet.microsoft.com/download/dotnet/8.0
- **Shared Network Folder**: Accessible from both Recorder and Render PCs (read/write)
- **FFmpeg**: Bundled in `lib/ffmpeg/` (included)
- **Tesseract OCR Data**: Bundled in `lib/tessdata/` (Bengali + English included)

### Optional
- **Blackmagic Decklink** or compatible capture card (for live feed input)
- **yt-dlp**: For YouTube live source (place in `lib/yt-dlp/yt-dlp.exe`)

---

## Installation

### Step 1: Set Up Shared Network Folder

Create a shared folder accessible from both PCs. Example: `\\MediaServer\OnAirCut`

The application will automatically create these subfolders on first run:
```
\\MediaServer\OnAirCut\
  Assets\AdSets\        ← Ad set configurations
  Assets\OcrProfiles\   ← OCR crop region profiles
  Ingest\RawClips\      ← Recorded raw clips (by date)
  Jobs\Pending\         ← New job queue
  Jobs\Processing\      ← Currently processing
  Jobs\Done\            ← Completed jobs
  Jobs\Failed\          ← Failed jobs
  Working\              ← Temp render workspace
  Output\               ← Final output (by date, by title)
  Logs\                 ← Shared log files
```

### Step 2: Install Recorder (Operator PC)

1. Copy the `OnAirCut Recorder` folder to the Recorder PC
2. Ensure these exist inside:
   - `lib/ffmpeg/ffmpeg.exe` and `ffprobe.exe`
   - `lib/tessdata/ben.traineddata` and `eng.traineddata`
3. Run `OnAirCut.Recorder.exe`
4. On first launch, go to **Settings** and configure:
   - **Shared Folder Path**: UNC path to the shared folder
   - **Operator Name**: Your name (for tracking)
   - **Render Server API URL**: `http://{render-pc-ip}:5123`

### Step 3: Install Render Server (Render PC)

1. Copy the `OnAirCut Server` folder to the Render PC
2. Ensure these exist inside:
   - `lib/ffmpeg/ffmpeg.exe` and `ffprobe.exe`
   - `lib/tessdata/ben.traineddata` and `eng.traineddata`
3. Run `OnAirCut.RenderServer.exe`
4. On first launch, go to **Settings** and configure:
   - **Shared Folder Path**: Same UNC path as Recorder
   - **FFmpeg/FFprobe Paths**: Should auto-detect from `lib/ffmpeg/`
   - **OCR Engine Path**: Should auto-detect from `lib/tessdata/`
   - **Database Path**: Local path (default: `AppData/onaircut.db`)
   - **API Port**: 5123 (default)

### Step 4: Prepare Ad Sets

Create ad set folders in the shared folder:
```
\\MediaServer\OnAirCut\Assets\AdSets\
  Set_A\
    tvc.mp4              ← 6-10 second TVC video
    overlay.png          ← 1920x1080 PNG with transparency
    overlay.mov          ← OR transparent MOV video overlay
    config.json          ← Ad set configuration
  Set_B\
    ...
```

Example `config.json`:
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

**Insert Modes:**
- `None` — No TVC insertion, overlay only
- `FixedTimestamp` — Insert TVC at `insertAtSec` seconds
- `Midpoint` — Insert TVC at the midpoint of the clip

**Overlay Types:**
- `.png` — Static image overlay (transparent areas preserved)
- `.mov` — Video overlay with alpha channel (ProRes 4444)
- `.webm` — Video overlay with alpha (VP9 with alpha)

---

## Using the Recorder

### Main Interface Layout

```
+------------------------------------------------------------------+
|  OnAirCut Recorder  [Settings]  [Folder:OK]  [Source]  [Clock]   |
+------------------------------------------------------------------+
|  Source    |        Video Preview           |  Ad Sets            |
|  Panel     |        + Audio Meter           |  ( ) Set_A          |
|            |        + Transport Controls    |  ( ) Set_B          |
|            |                                |  (x) No Ad          |
+------------+--------------------------------+---------------------+
|  [RECORD]  [STOP]  [Cancel]   Duration      [SUBMIT JOB]        |
+------------------------------------------------------------------+
|  Search: [____]  [Date]  [Status]                                |
|  [History Grid — previously processed stories]                   |
+------------------------------------------------------------------+
```

### Workflow: Recording a News Story

1. **Connect a source** (left panel):
   - **Local File**: Click "Browse", select a recorded news file, click "Connect"
   - **Live Feed**: Select capture device from dropdown, click "Connect"
   - **YouTube**: Paste live URL, click "Connect"

2. **Monitor the feed**: Watch/listen through headphones. The preview shows video with audio level meters.

3. **Start recording**: When the presenter begins a story, press **Record** (or F5).
   - The record button turns into a pulsing indicator
   - Duration counter starts

4. **Stop recording**: When the story + video report ends, press **Stop** (or F6).
   - The clip is saved to `{SharedFolder}\Ingest\RawClips\{date}\`

5. **Select an ad set** (right panel):
   - Choose which TVC + overlay to apply
   - Or select "No Ad" for a clean clip

6. **Submit the job**: Press **Submit Job** (or F8).
   - A job JSON is created in `Jobs\Pending\`
   - The Render Server will automatically pick it up

7. **Repeat** for the next story.

### Keyboard Shortcuts

| Key | Action |
|---|---|
| F5 | Start Recording |
| F6 | Stop Recording |
| Escape | Cancel Current Clip |
| F8 | Submit Job |
| Ctrl+F | Focus Search Box |
| Space | Play/Pause (Local File mode) |
| Ctrl+S | Toggle Settings |

### Configuring OCR Region

The OCR region determines where the application looks for the headline text on the video frame.

1. Go to **Settings** > **OCR Region Editor** (right panel)
2. Click **Capture Frame** to grab the current video frame
3. **Draw a rectangle** over the headline/title area
4. Set the profile name and source/channel name
5. Click **Save Profile**
6. Use **Test OCR** to verify it reads the text correctly

**Tips:**
- Draw the rectangle tightly around the text area
- Avoid including logos or moving tickers
- Each channel may need its own profile (different title positions)

### History Panel

The bottom panel shows previously processed stories:
- **Green** = Completed
- **Yellow** = Processing/Rendering
- **Blue** = Pending
- **Red** = Failed

Use the search bar to filter by title, date range, or status.
Double-click any row to open the output folder.

---

## Using the Render Server

### Dashboard Tab

Shows real-time status:
- **Stat Cards**: Pending, Active, Completed, Failed counts for today
- **Current Job**: Progress bar, render speed, ETA
- **Recent Activity**: Last 10 processing events

### Queue Tab

Shows all jobs in all states:
- **Pending**: Waiting to be processed (FIFO order)
- **Processing**: Currently being worked on
- **Completed**: Successfully finished
- **Failed**: Error occurred (can retry)

**Actions**: Right-click a job to Retry, Cancel, or Open Output Folder.

### History Tab

Search all processed stories:
- Full-text search by title
- Filter by date range, status, source
- Export results to CSV

### Settings Tab

Configure all server parameters:
- **Paths**: FFmpeg, tessdata, database, shared folder
- **Rendering**: Codec, preset, CRF, audio settings
- **OCR**: Language, multi-frame count, confidence threshold
- **Queue**: Poll interval, file stability check
- **API**: Port and enable/disable toggle

**Test buttons**:
- "Test FFmpeg" — verifies FFmpeg is accessible
- "Test OCR" — runs a test OCR on a sample
- "Validate Shared Folder" — checks folder structure

### Logs Tab

Real-time log viewer:
- Filter by log level (Debug, Info, Warning, Error)
- Filter by Job ID for troubleshooting
- Search within logs
- Auto-scroll toggle

---

## How the Pipeline Works

When a job is submitted, the Render Server processes it automatically:

```
1. DETECT      Job appears in Jobs\Pending\
2. CLAIM       Move to Jobs\Processing\ (lock file created)
3. FILE CHECK  Wait for raw clip to be fully written
4. PROBE       FFprobe reads input metadata (duration, resolution)
5. EXTRACT     FFmpeg extracts 20 evenly-spaced frames
6. RENDER      FFmpeg renders final video:
               - Split clip at TVC insertion point
               - Concatenate: part1 + TVC + part2
               - Apply overlay (PNG or MOV)
               - Encode to h264 + AAC
7. OCR         Crop headline region from 5 frames
               - Preprocess: grayscale, upscale, threshold
               - Tesseract OCR with Bengali model
               - Multi-frame consensus picks best title
8. ORGANIZE    Create output folder named by OCR title
               - Move rendered video
               - Move extracted frames (for thumbnails)
               - Generate metadata.json
9. DATABASE    Update SQLite with title, paths, status
10. DONE       Move job file to Jobs\Done\
```

**Average processing time**: 15-45 seconds per 2-minute clip (depends on hardware and encoding preset).

---

## Output Structure

After processing, each story gets its own folder:

```
\\MediaServer\OnAirCut\Output\2026-03-15\
  Story_Title_From_OCR\
    Story_Title_From_OCR.mp4    ← Final video with TVC + overlay
    metadata.json                ← Job details and timestamps
    frames\
      frame_001.jpg              ← 20 thumbnail candidates
      frame_002.jpg
      ...
      frame_020.jpg
```

The video is ready to upload to YouTube, Facebook, or TikTok.
The frames can be used as video thumbnails.

---

## Troubleshooting

### Recorder won't connect to source
- **Local File**: Ensure the file format is supported (.mp4, .mkv, .mov, .avi, .ts)
- **Live Feed**: Check that the capture card drivers are installed and the device appears in Windows
- **YouTube**: Ensure yt-dlp is present in `lib/yt-dlp/` and the URL is a valid live stream

### Shared folder shows "Disconnected"
- Verify the UNC path is correct in Settings
- Check network connectivity between PCs
- Ensure both PCs have read/write permissions

### Jobs stuck in "Pending"
- Verify Render Server is running
- Check Render Server's shared folder path matches Recorder's
- Look at Render Server Logs tab for errors

### OCR returns wrong or no title
- Reconfigure the OCR region in Settings > OCR Region Editor
- Ensure the headline text is visible and stable in the frame
- Try increasing the resize scale (default 2.0)
- Check that `ben.traineddata` exists in `lib/tessdata/`

### Render fails
- Check FFmpeg path in Server Settings
- Look at the error message in Queue > Job Detail
- Try with a simpler ad set (No Ad) to isolate the issue
- Check disk space on both shared folder and render PC

### Video has sync issues
- Ensure the TVC video has the same frame rate as the source
- Use `-preset fast` or `-preset medium` instead of `-preset ultrafast`
- Re-encode the TVC to match source format: `ffmpeg -i tvc_orig.mp4 -r 25 -c:v libx264 tvc.mp4`

---

## File Locations

### Recorder PC
```
OnAirCut Recorder\
  OnAirCut.Recorder.exe         ← Main executable
  AppData\settings.json          ← Recorder settings
  AppData\Logs\                  ← Log files
  lib\ffmpeg\                    ← FFmpeg binaries
  lib\tessdata\                  ← OCR training data
```

### Render PC
```
OnAirCut Server\
  OnAirCut.RenderServer.exe     ← Main executable
  AppData\settings.json          ← Server settings
  AppData\onaircut.db            ← SQLite database
  AppData\Logs\                  ← Log files
  lib\ffmpeg\                    ← FFmpeg binaries
  lib\tessdata\                  ← OCR training data
```

---

## Network Architecture

```
[Recorder PC]                    [Render PC]
  OnAirCut Recorder               OnAirCut Render Server
       |                                |
       |  HTTP API (port 5123)          |
       |  ← Search/History/Stats ──────|
       |                                |
       |          \\MediaShare          |
       |  ──── Shared Folder ──────    |
       |  (clips, jobs, ads, output)    |
```

- **Job submission**: Recorder writes JSON to `Jobs\Pending\` on shared folder
- **Data query**: Recorder calls Server's REST API for history/search
- **Render Server API** runs on port 5123 (configurable)

---

## Tips for Best Results

1. **Record clean segments**: Start recording slightly before the story begins, stop slightly after
2. **Use consistent OCR profiles**: Create one profile per channel and reuse it
3. **Keep TVC duration short**: 6-10 seconds works best for news clips
4. **Monitor the queue**: If jobs pile up, the 2-3 minute target may slip
5. **Use hardware encoding**: If the render PC has an NVIDIA GPU, switch to `h264_nvenc` in settings for 3-5x faster rendering
6. **Check history before recording**: Avoid duplicating stories already processed
7. **Backup the database**: Periodically copy `AppData/onaircut.db` to a safe location
