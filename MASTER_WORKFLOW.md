# OnAirCut — Master Development Workflow

> Interleaved build order for both Recorder and Render Server applications.
> Follow this file step by step. Each step tells you which app to work on and cross-references the detailed plan.

---

## MANDATORY RULES — Read Before Every Session

> **These rules apply to ALL steps. Never violate them.**

### Project Structure
```
D:\Code4\OnAirCut\
├── MASTER_WORKFLOW.md              ← YOU ARE HERE (always read first)
├── OnAirCut Recorder\
│   ├── OnAirCut Recorder.sln      ← Recorder solution file
│   ├── PLAN_RECORDER.md           ← Detailed recorder plan (read when working on [REC])
│   └── src\                       ← All recorder source code
│       ├── OnAirCut.Core\         ← Shared class library (referenced by both solutions)
│       └── OnAirCut.Recorder\     ← WPF recorder app
├── OnAirCut Server\
│   ├── OnAirCut Server.sln        ← Server solution file
│   ├── PLAN_RENDER_SERVER.md      ← Detailed server plan (read when working on [SRV])
│   └── src\                       ← All server source code
│       ├── OnAirCut.Core\         ← Same shared library (linked/referenced, NOT duplicated)
│       └── OnAirCut.RenderServer\ ← WPF render server app
```

### Code Standards (Must Follow Always)
1. **MVVM pattern is mandatory** — Every view has a ViewModel. No code-behind logic. Use `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`, `ObservableObject`).
2. **Modular code** — Each feature is its own folder/namespace with clear separation. Services behind interfaces. No god classes.
3. **Professional folder organization** — Follow this structure inside each WPF project:
   ```
   ProjectRoot\
   ├── App.xaml / App.xaml.cs
   ├── ViewModels\          ← All ViewModels
   ├── Views\               ← All XAML Views/Pages/Windows
   ├── Services\            ← Business logic services (behind interfaces)
   ├── Models\              ← Project-specific models (shared ones stay in Core)
   ├── Converters\          ← WPF value converters
   ├── Controls\            ← Custom/reusable WPF controls
   ├── Helpers\             ← Static utility helpers
   ├── Resources\           ← Styles, templates, assets
   └── Configuration\       ← Settings, DI registration
   ```
4. **Every step must produce a buildable solution** — After each step, `dotnet build` must succeed with zero errors.
5. **Dependency Injection everywhere** — Register services in DI container. Constructor injection. No `new Service()` in ViewModels.

### Reference Files
- **Always read this file first** for the current step and overall context.
- **Read `PLAN_RECORDER.md`** when the current step targets [REC] — it has full detail.
- **Read `PLAN_RENDER_SERVER.md`** when the current step targets [SRV] — it has full detail.
- **OnAirCut.Core is shared** — Both solutions reference the same Core project. The physical code lives in one place and is referenced by both `.sln` files via relative path.

### Two Separate Solutions
- **`OnAirCut Recorder.sln`** at `D:\Code4\OnAirCut\OnAirCut Recorder\` — contains OnAirCut.Core + OnAirCut.Recorder
- **`OnAirCut Server.sln`** at `D:\Code4\OnAirCut\OnAirCut Server\` — contains OnAirCut.Core + OnAirCut.RenderServer
- They are separate solutions that can be opened and built independently.
- OnAirCut.Core code lives physically under `OnAirCut Recorder\src\OnAirCut.Core\` and the Server solution references it via relative path `..\..\OnAirCut Recorder\src\OnAirCut.Core\OnAirCut.Core.csproj`.

---

## How to Read This Document

- **[REC]** = Work on `OnAirCut Recorder` — see `OnAirCut Recorder\PLAN_RECORDER.md`
- **[SRV]** = Work on `OnAirCut RenderServer` — see `OnAirCut Server\PLAN_RENDER_SERVER.md`
- **[CORE]** = Work on `OnAirCut.Core` shared library — referenced in both plans
- **[BOTH]** = Work touches both apps or shared infrastructure
- **[TEST]** = Integration test checkpoint — stop and verify before moving on
- Each step has a **"Done when"** criteria — do not move to the next step until satisfied

---

## Step 01 — Project Scaffolding & Shared Core
**Target: [BOTH]** | Ref: REC Phase 0, SRV Phase 0

### What to build
1. Create two separate .NET 8 solutions:

   **Solution A — Recorder:**
   - File: `D:\Code4\OnAirCut\OnAirCut Recorder\OnAirCut Recorder.sln`
   - Projects:
     - `src\OnAirCut.Core\` — Shared class library
     - `src\OnAirCut.Recorder\` — WPF app (startup project)

   **Solution B — Server:**
   - File: `D:\Code4\OnAirCut\OnAirCut Server\OnAirCut Server.sln`
   - Projects:
     - Reference to `..\..\OnAirCut Recorder\src\OnAirCut.Core\OnAirCut.Core.csproj` (shared, not duplicated)
     - `src\OnAirCut.RenderServer\` — WPF app (startup project)

2. Configure all projects: `net8.0-windows`, nullable enabled, implicit usings
3. Both WPF apps reference `OnAirCut.Core`
4. Follow the mandatory folder structure (ViewModels, Views, Services, Models, etc.) from the start

### Shared Core contents to build now
**Models:**
- `JobFile` — All fields for job JSON serialization
- `AdSetConfig` — Ad set definition (name, tvcPath, overlayPath, insertMode, insertAtSec, overlayStartSec, overlayEndSec, outputWidth, outputHeight)
- `OcrProfile` — Crop region definition (profileName, sourceName, cropX, cropY, cropWidth, cropHeight, resizeScale, thresholdMode, isActive)
- `ProcessedStory` — Read model for history/search display
- `JobStatus` enum — Pending, Processing, Rendering, ExtractingFrames, RunningOcr, Completed, Failed
- `SourceType` enum — LiveFeed, LocalFile, YouTubeUrl
- `RecordingResult` — filePath, duration, startTime, endTime

**Constants:**
- `FolderNames` — All standardized subfolder names
- `FileExtensions` — Supported formats

**Utilities:**
- `TitleSanitizer` — Raw text to filesystem-safe name (invalid chars, whitespace, max length, duplicate suffix)
- `JobFileHelper` — Read/write/move job JSON with atomic temp-file-then-rename
- `SharedFolderValidator` — Check folder structure exists and is writable
- `SharedFolderInitializer` — Create full folder tree (idempotent)

**Interfaces:**
- `IVideoSource` — Connect, Disconnect, StartPreview, StopPreview, StartRecording, StopRecording, IsHealthy, CurrentPosition
- `IAdSetProvider` — List and load ad sets from shared folder
- `IOcrProfileProvider` — List and load OCR profiles

### NuGet packages to install now
**Core:** `System.Text.Json`, `Microsoft.Data.Sqlite`
**Recorder:** `MaterialDesignThemes`, `MaterialDesignColors`, `CommunityToolkit.Mvvm`, `Serilog`, `Serilog.Sinks.File`
**Server:** `MaterialDesignThemes`, `MaterialDesignColors`, `CommunityToolkit.Mvvm`, `Serilog`, `Serilog.Sinks.File`, `CliWrap`, `Microsoft.Data.Sqlite`

### Done when
- [ ] Solution builds with zero errors
- [ ] All shared models serialize/deserialize correctly (quick unit test)
- [ ] `SharedFolderInitializer` creates the full folder tree on a test path
- [ ] `JobFileHelper` can write a job JSON to Pending and move it to Processing
- [ ] `TitleSanitizer` handles edge cases (empty, special chars, long strings)

---

## Step 02 — Proof of Concept: FFmpeg + OCR + Queue Round-Trip
**Target: [BOTH]** | Ref: SRV Plan critical PoC items

> **CRITICAL**: Do this before building any GUI. If these fail, the whole project needs redesign.

### What to prove
**PoC-A: FFmpeg render with TVC insertion + overlay**
- Take a sample news clip (30-60 seconds)
- Take a sample TVC (6-10 seconds)
- Take a sample overlay PNG (1920x1080 with transparency)
- Manually run FFmpeg commands from command line:
  1. Split clip at a fixed point
  2. Concat part1 + TVC + part2
  3. Apply overlay on the concatenated result
  4. Measure: render time vs clip duration (target: under 30 seconds for a 2-min clip)
- Document the exact working FFmpeg commands

**PoC-B: Frame extraction + Bangla OCR**
- Extract 20 frames from the sample clip using FFmpeg
- Crop the headline region from one frame using ImageSharp (or any tool)
- Run Tesseract with `ben.traineddata` on the cropped image
- Evaluate: Is the OCR output readable and correct?
- If Tesseract fails, test PaddleOCRSharp as alternative
- Document which engine works and what preprocessing helps

**PoC-C: File-based queue round-trip**
- From one console app, write a job JSON to `Jobs\Pending\`
- From another console app, detect it, move to `Jobs\Processing\`, read contents
- Verify atomic rename works over a network share (test with actual UNC path if possible)

### Done when
- [ ] FFmpeg commands produce a correct final video with TVC + overlay
- [ ] Render time is acceptable (< 1x clip duration with `-preset fast`)
- [ ] At least one OCR engine reads Bangla headline text from a broadcast frame
- [ ] File queue round-trip works (write → detect → move → read)
- [ ] All working commands documented in a `poc_results.md` file for future reference

---

## Step 03 — Recorder: Application Shell & Settings
**Target: [REC]** | Ref: REC Phase 1

### What to build
1. **App.xaml** — Material Design theme setup (BundledTheme, primary/secondary colors, dark mode)
2. **MainWindow.xaml** — Skeleton layout with:
   - Title bar: "OnAirCut Recorder" + clock + placeholder status indicators
   - Navigation structure (sidebar or tabs)
   - Status bar at bottom
   - Empty placeholder panels for: source, preview, ad sets, controls, history
3. **MVVM infrastructure:**
   - DI container setup in `App.xaml.cs` (Microsoft.Extensions.DependencyInjection)
   - Base ViewModel pattern with `CommunityToolkit.Mvvm`
   - ViewModelLocator or DataTemplate-based view resolution
4. **Settings system:**
   - `RecorderSettings` model with all fields (SharedFolderPath, AudioDevice, OperatorName, etc.)
   - JSON persistence to `%AppData%\OnAirCut\recorder_settings.json`
   - Settings page with shared folder path browser + validation
5. **Shared folder health monitor:**
   - Background service checking folder accessibility every 5 seconds
   - Status indicator in title bar (green/yellow/red dot)
6. **Serilog setup:**
   - File sink to `%AppData%\OnAirCut\Logs\recorder_{date}.log`

### Done when
- [ ] Recorder app launches with Material Design themed window
- [ ] Settings page saves/loads shared folder path
- [ ] Shared folder status indicator works (connect/disconnect the share to test)
- [ ] Logs are written to the correct file
- [ ] Empty layout skeleton visible with all panel areas reserved

---

## Step 04 — Server: Application Shell, Settings & SQLite
**Target: [SRV]** | Ref: SRV Phase 1

### What to build
1. **App.xaml** — Material Design theme (match Recorder styling)
2. **MainWindow.xaml** — Skeleton layout with:
   - Title bar: "OnAirCut Render Server" + clock + status indicators
   - Tab navigation: Dashboard, Queue, History, Settings, Logs
   - Empty placeholder content per tab
3. **MVVM + DI** — Same pattern as Recorder
4. **Settings system:**
   - `RenderServerSettings` model (SharedFolderPath, FFmpegPath, FFprobePath, OcrEnginePath, LocalDatabasePath, all render settings)
   - JSON persistence to `%AppData%\OnAirCut\renderserver_settings.json`
   - Settings page with path browsers and validation
   - "Test FFmpeg" button that runs `ffmpeg -version`
5. **SQLite setup:**
   - Create database at configured local path
   - Run full schema creation (ProcessedStories, JobLog, OcrResults, AdSetUsage tables)
   - Enable WAL mode
   - Schema version tracking
6. **SqliteRepository class:**
   - `InsertStory`, `UpdateStoryStatus`, `UpdateStoryOcr`, `UpdateStoryOutput`
   - `SearchStories`, `GetRecentStories`, `GetStoryByJobId`, `GetTodayStats`
   - `InsertJobLog`, `InsertOcrResult`
   - All parameterized queries
7. **Shared folder health monitor** (same as Recorder)
8. **Serilog setup**

### Done when
- [ ] Server app launches with Material Design themed window
- [ ] Settings save/load correctly
- [ ] SQLite database is created with all tables
- [ ] Repository CRUD operations work (write a quick test: insert a story, query it back, update status)
- [ ] "Test FFmpeg" button shows ffmpeg version output

---

## Step 05 — Recorder: Video Playback & Local File Source
**Target: [REC]** | Ref: REC Phase 2

### What to build
1. **Install:** `LibVLCSharp` + `LibVLCSharp.WPF` + `NAudio`
2. **LocalFileSource** implementing `IVideoSource`:
   - Open local video file via LibVLCSharp
   - Play / pause / seek
   - Get current position and duration
   - Audio output to selected device
3. **Preview player UI** (center panel):
   - `VideoView` control showing the video
   - Timecode display (HH:MM:SS)
   - Play/Pause button
   - Seek slider (bound to position/duration)
   - Speed control buttons (0.5x, 1x, 1.5x, 2x)
4. **Source panel UI** (left panel):
   - Source type selector: three buttons/tabs (Live / File / YouTube) — only File active for now
   - "Browse File" button + file path display
   - "Connect" / "Disconnect" button
   - Source status label
5. **Audio monitoring:**
   - NAudio device enumeration
   - Volume slider (0-100%)
   - Mute/unmute button
   - Audio level meter (VU meter — green/yellow/red bars)
   - Headphone output device selector

### Done when
- [ ] Can browse and open a local .mp4 file
- [ ] Video plays in the preview area with audio
- [ ] Seek slider works, timecode updates
- [ ] Speed control changes playback speed
- [ ] Volume slider controls audio level
- [ ] Audio meter shows real-time levels
- [ ] Can select different audio output devices

---

## Step 06 — Recorder: Recording Controls
**Target: [REC]** | Ref: REC Phase 3

### What to build
1. **Recording state machine:**
   - States: Idle → ReadyToRecord → Recording → ClipComplete
   - State drives which buttons are enabled/disabled
2. **Record Start:**
   - Generate filename: `clip_{yyyyMMdd}_{HHmmss}.mp4`
   - Output to: `{SharedFolder}\Ingest\RawClips\{date}\{filename}`
   - Use FFmpeg (via CliWrap) to extract from current playback position:
     `ffmpeg -i input.mp4 -ss {currentPos} -c copy {output}`
   - Or use LibVLC recording if simpler
   - Start elapsed time counter
3. **Record Stop:**
   - Stop the FFmpeg extraction / LibVLC recording
   - Receive file path and duration
   - Transition to ClipComplete state
4. **Cancel clip:**
   - Confirmation dialog
   - Delete partial file
   - Return to ReadyToRecord
5. **Controls bar UI:**
   - Record button (red circle → pulsing red square when recording)
   - Stop button
   - Cancel button
   - Elapsed duration display
   - Clip info panel (filename, size, status)
6. **Keyboard shortcuts:**
   - F5 = Record Start
   - F6 = Record Stop
   - Escape = Cancel

### Done when
- [ ] Can start recording while playing a local file
- [ ] Recording creates a valid .mp4 file in the shared folder's RawClips directory
- [ ] Stop recording produces a complete, playable clip
- [ ] Duration display updates during recording
- [ ] Cancel deletes the partial clip
- [ ] Keyboard shortcuts work
- [ ] State machine correctly enables/disables buttons

---

## ✓ MILESTONE A — Manual Clip Extraction Works
> At this point you can open a file, play it, listen with headphones, and extract clips to the shared folder.
> **Test**: Open a 1-hour news recording, extract 3 stories manually, verify all clips are valid.

---

## Step 07 — Server: Job Queue Watcher & File Ready Detection
**Target: [SRV]** | Ref: SRV Phase 2

### What to build
1. **JobWatcherService** (background worker):
   - FileSystemWatcher on `Jobs\Pending\` for `.json` files
   - Polling fallback every 2 seconds (FSW can miss events over network)
   - On detecting a job file:
     1. Read + deserialize `JobFile`
     2. Validate required fields
     3. Atomic move from `Pending\` to `Processing\`
     4. Insert into SQLite (status: Processing)
     5. Enqueue to internal queue
2. **FileReadyChecker:**
   - Check: file exists → size > 0 → size stable (check twice, 3-sec gap) → not locked
   - Optional: ffprobe integrity check
   - Retry every 1 second, timeout after 60 seconds
   - On timeout: mark job Failed
3. **Internal queue:**
   - `Channel<JobFile>` for async producer/consumer
   - Single worker consuming jobs (configurable concurrency later)
   - Queue depth exposed as observable property for GUI
4. **Job locking:**
   - Create `.lock` file in Processing folder
   - Lock file contains: hostname, PID, timestamp
   - Remove on completion/failure
5. **Update Dashboard tab** to show:
   - Pending count, Processing count
   - Current job ID being processed
   - Basic log output of events

### Done when
- [ ] Manually place a job JSON in `Jobs\Pending\` — server detects and picks it up
- [ ] Job moves from Pending → Processing folder
- [ ] SQLite record created with status "Processing"
- [ ] FileReadyChecker correctly waits for raw clip file to be stable
- [ ] Lock file created and cleaned up
- [ ] Dashboard shows job pickup events

---

## Step 08 — Recorder: Ad Set Selection & Job Submission
**Target: [REC]** | Ref: REC Phase 4

### What to build
1. **AdSetProvider service:**
   - Scan `{SharedFolder}\Assets\AdSets\` for subfolders
   - Read `config.json` from each to get `AdSetConfig`
   - FileSystemWatcher to refresh when ad sets added/removed
2. **Ad set selection UI** (right panel):
   - List of ad sets as radio button cards showing: name, TVC file, overlay file, insert mode
   - "No Ad" option
   - Selected set highlighted
   - Quick overlay thumbnail preview
3. **Job submission:**
   - "Submit Job" button (enabled when clip complete + ad set chosen)
   - Build `JobFile` with all metadata
   - Atomic write to `{SharedFolder}\Jobs\Pending\{jobId}.json`
   - Show "Submitted" confirmation
   - Return to Idle state
4. **Job ID generation:**
   - Format: `JOB-{yyyyMMdd}-{HHmmss}-{sequence}`

### Prepare test ad sets
- Create at least two sample ad set folders in `Assets\AdSets\`:
  - `Set_Demo_A\` with a sample TVC video + overlay PNG + `config.json`
  - `Set_Demo_B\` with different content + `config.json`

### Done when
- [ ] Ad sets load from shared folder and display in the right panel
- [ ] Can select an ad set (radio buttons work)
- [ ] After recording a clip + selecting ad set, "Submit Job" creates a valid job JSON in Pending
- [ ] Server (from Step 07) picks up the submitted job automatically
- [ ] Full round-trip works: Record clip → Select ad → Submit → Server detects

---

## ✓ MILESTONE B — Recorder-to-Server Handoff Works
> The Recorder can produce clips and submit jobs. The Server detects and picks them up.
> **Test**: Record 3 clips with different ad sets, verify all 3 jobs appear in Server's Processing queue.

---

## Step 09 — Server: Frame Extraction
**Target: [SRV]** | Ref: SRV Phase 3

### What to build
1. **FFprobe integration:**
   - Probe input file: duration, resolution, fps, codec, audio info
   - Validate: has video stream, duration > 0, not corrupt
   - Store metadata in job context object
2. **FrameExtractionService:**
   - Calculate frame interval: `duration / 20` (evenly spaced)
   - FFmpeg command via CliWrap:
     ```
     ffmpeg -i {input} -vf "fps=1/{interval_seconds}" -frames:v 20 -q:v 2 {workDir}\frames\frame_%03d.jpg
     ```
   - Output to: `{SharedFolder}\Working\{jobId}\frames\`
   - Verify: count extracted files, log if fewer than 20
   - Report progress

### Done when
- [ ] Given a raw clip, FFprobe extracts correct metadata
- [ ] 20 frames extracted as JPEG files, evenly spaced across clip duration
- [ ] Frames are visually correct (not black, not corrupted)
- [ ] Service reports progress and handles errors

---

## Step 10 — Server: FFmpeg Render Pipeline
**Target: [SRV]** | Ref: SRV Phase 5

### What to build
1. **FfmpegCommandBuilder:**
   - Build command string based on ad set config's `insertMode`
   - Mode A (no TVC, overlay only): single ffmpeg pass with overlay filter
   - Mode B (TVC at fixed timestamp): split → concat → overlay
   - Mode C (TVC at midpoint): same as B with auto-calculated insert point
   - Handle: codec, preset, CRF, audio codec, bitrate from settings
2. **FfmpegRenderService:**
   - Execute via CliWrap with real-time stderr capture
   - Parse progress: `time=`, `speed=`, calculate percentage from known duration
   - Output to temp path: `{Working}\{jobId}\temp_output.mp4`
   - Check exit code (0 = success)
   - Save stderr log to `{Working}\{jobId}\ffmpeg.log`
3. **Resolution/format normalization:**
   - If source resolution differs from output target, add scale filter
   - Ensure consistent frame rate across segments
   - Audio: normalize to 48kHz stereo

### Done when
- [ ] Mode A works: clip + overlay = correct output
- [ ] Mode B works: clip split at fixed point + TVC inserted + overlay = correct output
- [ ] Mode C works: TVC inserted at midpoint
- [ ] Progress percentage updates during render
- [ ] Render completes in acceptable time
- [ ] Error cases: bad input file, missing TVC file — handled gracefully

---

## Step 11 — Server: OCR Pipeline
**Target: [SRV]** | Ref: SRV Phase 4

### What to build
1. **Install:** `Tesseract` NuGet (or `PaddleOCRSharp`) + `SixLabors.ImageSharp`
2. **OCR preprocessing pipeline** (per frame):
   - Load JPEG with ImageSharp
   - Crop using OcrProfile's region (x, y, width, height)
   - Resize (upscale 2x)
   - Convert to grayscale
   - Apply threshold (configurable: none / binary / adaptive)
   - Save preprocessed image to temp (for debugging)
   - Run through OCR engine
   - Return: raw text + confidence
3. **Multi-frame consensus:**
   - Select 5 frames from the middle 60% of the 20 extracted
   - Run OCR on each
   - Store all results in `OcrResults` table
   - Consensus: most frequent result (Levenshtein grouping), highest confidence as tiebreaker
4. **Title processing:**
   - `TitleRaw` — exact OCR output
   - `TitleNormalized` — trimmed, Unicode NFC, collapsed spaces
   - `SafeFolderName` — filesystem-safe (invalid chars removed, underscores, max 100 chars)
5. **Fallback:**
   - If all confidence < 30% or no text: use `Story_{yyyyMMdd}_{HHmmss}`
   - Mark in DB with OcrConfidence = 0

### Done when
- [ ] OCR reads Bangla text from a cropped broadcast frame
- [ ] Multi-frame consensus picks the best result from 5 frames
- [ ] Title normalization and sanitization produce correct filesystem-safe names
- [ ] Fallback naming works when OCR fails
- [ ] All per-frame results stored in OcrResults table

---

## Step 12 — Server: Output Organization & Naming
**Target: [SRV]** | Ref: SRV Phase 6

### What to build
1. **Output folder creation:**
   ```
   {SharedFolder}\Output\{yyyy-MM-dd}\{SafeFolderName}\
     ├── {SafeFolderName}.mp4
     ├── metadata.json
     └── frames\
         ├── frame_001.jpg ... frame_020.jpg
   ```
2. **Organization steps:**
   - Create date folder + story folder
   - Move rendered video from Working → Output, rename to title
   - Move frames from Working → Output\frames
   - Generate metadata.json (jobId, title, onAirDateTime, duration, source, adSet, ocrConfidence, renderDuration, etc.)
   - Update SQLite with all final paths and status = Completed
   - Move job file from Processing → Done
3. **Duplicate title handling:**
   - If folder exists: append `_002`, `_003`, etc.
   - Check SQLite for same title + same date
4. **Cleanup:**
   - Delete Working\{jobId} folder on success
   - Keep on failure for debugging

### Done when
- [ ] After render + OCR, final output appears in correctly named folder
- [ ] Video file named with sanitized OCR title
- [ ] 20 frames present in frames subfolder
- [ ] metadata.json contains all expected fields
- [ ] SQLite updated with correct paths and Completed status
- [ ] Job file moved to Done folder
- [ ] Duplicate title gets a numbered suffix

---

## Step 13 — Server: Pipeline Orchestrator (Wire Everything Together)
**Target: [SRV]** | Ref: SRV Phase 10

### What to build
1. **JobPipelineOrchestrator:**
   - Takes a `JobFile` and runs the full sequence:
     1. Validate job
     2. Wait for file ready
     3. Update SQLite → Processing
     4. FFprobe input
     5. Start frame extraction (parallel)
     6. Start FFmpeg render (parallel with frame extraction)
     7. Wait for frame extraction → run OCR
     8. Wait for render completion
     9. Wait for OCR completion
     10. Organize output (needs both render + OCR results)
     11. Update SQLite → Completed
     12. Move job to Done, cleanup Working
2. **Step logging:**
   - Each step logs to SQLite JobLog table (step name, start, end, duration)
   - Each step wrapped in try-catch
3. **Failure handling:**
   - Per-step retry: file ready (60s), frame extraction (1 retry), OCR (1 retry then fallback), render (1 retry), organize (1 retry)
   - Final failure: job → Failed folder, SQLite error logged, Working kept
4. **Cancellation:**
   - CancellationToken threaded through all steps
   - Kill FFmpeg gracefully on cancel
5. **Connect to JobWatcherService** from Step 07:
   - Watcher feeds jobs into orchestrator via Channel
   - Orchestrator processes one at a time (queue)

### Done when
- [ ] Full end-to-end pipeline: job JSON in Pending → auto pickup → file check → probe → frames + render (parallel) → OCR → organize → Done
- [ ] A single clip goes from raw to final output folder with correct name, video, frames, metadata
- [ ] Failed jobs land in Failed folder with error in SQLite
- [ ] Step-by-step log visible in JobLog table
- [ ] Multiple jobs queue correctly (submit 3 rapidly, they process sequentially)

---

## ✓ MILESTONE C — Full Pipeline Works End-to-End
> From Recorder: open file → record clip → select ad → submit.
> Server automatically: picks up → extracts frames → renders with TVC + overlay → OCR names it → organizes output.
> **Test**: Process 5 different news clips. Verify all 5 have correct output folders, videos, frames, and OCR-based names.

---

## Step 14 — Server: Dashboard & Queue GUI
**Target: [SRV]** | Ref: SRV Phase 7

### What to build
1. **Dashboard screen:**
   - 4 stat cards: Pending, Active, Completed (today), Failed (today)
   - Current job progress: job ID, current step, progress bar, speed, ETA
   - Recent activity list: last 10 events with timestamp, job ID, status, title
   - System health: CPU usage, disk space, shared folder status
2. **Queue screen:**
   - DataGrid: JobId, Title, Source, AdSet, Status, CurrentStep, Elapsed, Error
   - Color-coded rows by status
   - Auto-refresh every 2 seconds
   - Context menu: View Details, Retry, Cancel, Open Output Folder
3. **Job detail screen:**
   - All job metadata
   - Processing timeline (each step with duration)
   - OCR results (per-frame + consensus)
   - Frame thumbnails grid
   - FFmpeg command and log
   - Error details
   - Action buttons: Retry, Open Output, Open Raw

### Done when
- [ ] Dashboard shows real-time stats and current job progress
- [ ] Queue screen lists all jobs with correct status colors
- [ ] Job detail screen shows complete processing information
- [ ] Can retry a failed job from the GUI
- [ ] Progress bar updates in real-time during render

---

## Step 15 — Server: Data API for Recorder
**Target: [SRV]** | Ref: SRV Phase 9

### What to build
1. **Minimal HTTP API** hosted inside Render Server (ASP.NET Core Minimal API):
   - Binds to `http://0.0.0.0:5123` (configurable in settings)
   - Endpoints:
     - `GET /api/health` — status + pending count
     - `GET /api/stories/search?q=&dateFrom=&dateTo=&status=&limit=&offset=` — search
     - `GET /api/stories/recent?count=` — latest N stories
     - `GET /api/stories/{jobId}` — single story detail
     - `GET /api/jobs/stats` — counts by status
     - `GET /api/adsets` — list available ad sets
2. **Wire to SqliteRepository** — each endpoint calls the appropriate repo method
3. **Basic security:** LAN-only, optional API key header
4. **Add to Server settings:** API port, API key, enable/disable toggle

### Done when
- [ ] API starts with the Server app
- [ ] Can call `/api/health` from browser and get JSON response
- [ ] Search endpoint returns correct filtered results
- [ ] Recent stories endpoint returns last N processed items
- [ ] Stats endpoint matches what the Dashboard shows

---

## Step 16 — Recorder: History & Search Panel
**Target: [REC]** | Ref: REC Phase 5

### What to build
1. **API client service:**
   - HttpClient calling Server's API endpoints
   - Configurable base URL in Recorder settings (e.g., `http://render-pc:5123`)
   - Fallback: direct SQLite read-only access via network path (if API unreachable)
2. **History panel UI** (bottom section of main window):
   - DataGrid: Title, On-Air Date/Time, Duration, Source, Ad Set, Status
   - Status color coding: Blue=Pending, Yellow=Processing, Green=Completed, Red=Failed
   - Double-click row → open output folder in Explorer
   - Context menu: Open folder, Copy title, View details
3. **Search bar:**
   - Text input (searches title, folder name)
   - Date range picker (from / to)
   - Source/channel dropdown filter
   - Status dropdown filter
   - Search button + Enter key
   - "Today" quick filter
   - "Clear filters" button
4. **Auto-refresh** — poll every 10 seconds for status updates
5. **Duplicate indicator** — when recording, check recent titles for potential duplicates

### Done when
- [ ] Recorder shows processed stories from Server's database
- [ ] Search by title text works
- [ ] Search by date range works
- [ ] Status filter works
- [ ] Double-click opens output folder
- [ ] Operator can see which stories are already processed before recording

---

## Step 17 — Recorder: OCR Region Configuration
**Target: [REC]** | Ref: REC Phase 6

### What to build
1. **OCR Profile Editor screen** (accessible from Settings or nav):
   - "Capture Frame" button: grab current preview frame as still image
   - "Load Sample Image" button: load from file
   - Full-resolution image display with zoom/pan
2. **Rectangle drawing tool:**
   - Click + drag to create rectangle on the image
   - Corner/edge handles to resize
   - Drag body to reposition
   - Real-time coordinate display: X, Y, Width, Height (pixels)
   - Semi-transparent colored overlay with border
3. **Profile management:**
   - Profile name + source/channel name
   - Crop region coordinates
   - ResizeScale (default 2.0)
   - ThresholdMode (none / binary / adaptive)
   - Save to `{SharedFolder}\Assets\OcrProfiles\{name}.json`
   - List all profiles with edit/delete/duplicate
4. **Test OCR button:**
   - Crop current frame with current rectangle
   - Run local OCR test (Tesseract)
   - Show result text + confidence in a popup
5. **Multi-region support:**
   - Primary region (used for filename)
   - Optional secondary region (metadata only)

### Done when
- [ ] Can capture a frame from video preview
- [ ] Can draw and resize a rectangle on the image
- [ ] Coordinates update in real-time
- [ ] Profile saves correctly as JSON to shared folder
- [ ] "Test OCR" shows recognized text from the cropped region
- [ ] Multiple profiles can be created for different channels
- [ ] Server's OCR pipeline reads these profile files correctly

---

## ✓ MILESTONE D — Complete Core Workflow
> Recorder: record, select ad, configure OCR region, submit, see history.
> Server: full auto-processing pipeline, GUI dashboard, API for recorder.
> **Test**: Simulate a full 1-hour news session — extract 10+ clips, verify all process correctly with OCR names.

---

## Step 18 — Server: History, Search & Logs GUI
**Target: [SRV]** | Ref: SRV Phase 8

### What to build
1. **History/Search screen:**
   - Full-text search (title, folder name)
   - Date range picker
   - Source/channel, Status, Ad set filters
   - Duration range filter (min/max seconds)
   - Results grid: all ProcessedStories columns, sortable, paginated
   - Export to CSV button
   - Row actions: Open output, Open video player, Copy title, View details
2. **Logs screen:**
   - Real-time log viewer tailing Serilog output
   - Color-coded by level (Debug=gray, Info=white, Warning=yellow, Error=red)
   - Filter by level and by job ID
   - Search within logs
   - Auto-scroll toggle
   - Log file browser: list by date, open in external editor
3. **Settings screen polish:**
   - All settings from Step 04 exposed with proper labels and validation
   - Sections: Paths, Rendering, OCR, Queue, Cleanup
   - Path fields with browse buttons
   - "Test FFmpeg", "Test OCR", "Validate Shared Folder" buttons

### Done when
- [ ] History search returns correct filtered results
- [ ] Can export search results to CSV
- [ ] Log viewer shows real-time processing logs
- [ ] Can filter logs by job ID to troubleshoot a specific job
- [ ] All settings are editable and persist correctly

---

## Step 19 — Recorder: Live Feed Source (Capture Card)
**Target: [REC]** | Ref: REC Phase 7

### What to build
1. **LiveFeedSource** implementing `IVideoSource`:
   - Enumerate DirectShow/DeckLink capture devices
   - Connect to selected device via LibVLCSharp (or FFmpeg dshow input)
   - Preview live video
   - Record to file on start/stop
2. **Capture device settings UI:**
   - Device selection dropdown + refresh button
   - Input format info: resolution, frame rate
   - Audio input: embedded or separate device
   - Signal status indicator: Connected / No Signal / Signal Lost
3. **Recording from live feed:**
   - Begin writing immediately on Record Start
   - Track wall-clock time as on-air timestamp
   - On Record Stop: finalize file (moov atom, flush)
   - Verify integrity: duration > 0, file size > threshold
4. **Signal health monitoring:**
   - Frame arrival rate check
   - Audio level presence check
   - Visual warning on signal drop (flashing border)
   - Continue recording through gaps (don't auto-stop)

### Done when
- [ ] Capture card devices appear in dropdown
- [ ] Live feed previews in the player area
- [ ] Recording produces a valid clip in the shared folder
- [ ] Signal loss shows warning but recording continues
- [ ] Full workflow works: live feed → record → select ad → submit → server processes

---

## Step 20 — Recorder: YouTube Live Source
**Target: [REC]** | Ref: REC Phase 8

### What to build
1. **Install:** `CliWrap` (if not already) for yt-dlp integration
2. **YouTubeSource** implementing `IVideoSource`:
   - Text input for YouTube URL + Connect button
   - Resolve stream URL via yt-dlp: `yt-dlp -g --no-playlist {url}`
   - Feed resolved URL to LibVLCSharp for preview
   - Record via FFmpeg reading the stream URL
3. **YouTube-specific handling:**
   - URL validation
   - Stream URL re-resolution every 30 minutes (URLs expire)
   - Network buffering indicator
   - Live delay indicator
   - Auto-reconnect on drop with backoff
   - Clear error messages for: invalid URL, stream ended, geo-blocked
4. **Add yt-dlp path to Recorder settings**

### Done when
- [ ] Can paste a YouTube live news URL and see preview
- [ ] Recording produces a valid clip
- [ ] Reconnects if stream drops briefly
- [ ] Error messages are clear and helpful
- [ ] Full workflow works: YouTube → record → select ad → submit → server processes

---

## ✓ MILESTONE E — All Three Input Sources Working
> Live feed, local file, and YouTube — all three produce clips that flow through the full pipeline.
> **Test**: Process at least 2 clips from each source type.

---

## Step 21 — Server: Error Recovery & Robustness
**Target: [SRV]** | Ref: SRV Phase 11

### What to build
1. **Crash recovery on startup:**
   - Scan `Jobs\Processing\` for orphaned jobs
   - Check lock files: if owning process dead → move job back to Pending (or Failed if retried)
   - Check `Working\` for incomplete directories without matching active jobs
2. **Shared folder resilience:**
   - If folder becomes unavailable: pause job scanning, show disconnected status
   - Retry file operations with exponential backoff
   - Auto-resume when folder returns
3. **FFmpeg crash handling:**
   - Non-zero exit: capture stderr, save log, store error in SQLite
   - Clean up partial output files
   - Retry once with modified settings if applicable
4. **Disk space management:**
   - Check available space before starting any job (shared folder + local temp)
   - If below 5 GB: pause queue, show warning in GUI
   - Update space indicator after each job
5. **Graceful shutdown:**
   - Warn if render in progress: "Cancel and exit" vs "Wait for completion"
   - Flush logs, close SQLite properly

### Done when
- [ ] Kill server during a render → restart → orphaned job recovered
- [ ] Disconnect shared folder during processing → server pauses → reconnect → resumes
- [ ] FFmpeg crash → error logged, job marked failed, retry works
- [ ] Low disk space → queue paused, warning shown
- [ ] Graceful shutdown waits for or cancels active render

---

## Step 22 — Server: Performance Optimization
**Target: [SRV]** | Ref: SRV Phase 12

### What to build
1. **Render speed options:**
   - Settings: preset selection (ultrafast / fast / medium)
   - Hardware acceleration detection: check for NVENC, QSV
   - If available: offer as codec option (`h264_nvenc` with appropriate flags)
   - Smart re-encode detection: use `-c copy` when no overlay/TVC needed
2. **Queue priority:**
   - Add `priority` field to JobFile (Normal / High)
   - Recorder UI: "Priority" checkbox for breaking news
   - High-priority jobs jump to front of queue
   - Server GUI: drag-and-drop or up/down buttons to reorder queue
3. **Resource management:**
   - FFmpeg process priority: BelowNormal
   - `-threads` flag based on settings
   - Memory: process frames one at a time, don't hold all in memory

### Done when
- [ ] Hardware-accelerated render works (if GPU available) and is noticeably faster
- [ ] High-priority jobs process before normal ones
- [ ] Server GUI responsive even during heavy rendering (FFmpeg runs at lower priority)

---

## Step 23 — Recorder: Final Layout Assembly
**Target: [REC]** | Ref: REC Phase 9

### What to build
1. **Assemble all panels into final layout:**
   ```
   +------------------------------------------------------------------+
   |  [OnAirCut Recorder]  [Clock]  [Shared:OK]  [Source:Connected]   |
   +------------------------------------------------------------------+
   |  Source    |                            |  Ad Sets               |
   |  Panel     |      Video Preview         |  [radio cards]         |
   |  [Live]    |      [Timecode]            |                        |
   |  [File]    |      [Audio Meter]         |  Overlay Preview       |
   |  [YouTube] |      [Volume]              |  [thumbnail]           |
   |  [Connect] |      [Transport controls]  |                        |
   +------------+----------------------------+------------------------+
   |  [RECORD]  [STOP]  [Cancel]  Duration: 00:01:45       [Submit]  |
   +------------------------------------------------------------------+
   |  Search: [___] [Date] [Source] [Status]                          |
   |  [History DataGrid]                                              |
   +------------------------------------------------------------------+
   |  [Status bar]                                                     |
   +------------------------------------------------------------------+
   ```
2. **Responsive layout:**
   - Grid with proportional sizing
   - Left panel: ~200px fixed
   - Center: star (fills)
   - Right panel: ~250px fixed
   - Bottom history: ~250px with GridSplitter to resize
3. **Verify all keyboard shortcuts work:**
   - F5=Record, F6=Stop, Escape=Cancel, F8=Submit, Ctrl+F=Search, Space=Play/Pause, Ctrl+S=Settings
4. **Smooth transitions between source types** — switching Live/File/YouTube updates relevant controls

### Done when
- [ ] All panels fit together in a cohesive layout
- [ ] Resizing the window works correctly
- [ ] GridSplitter lets user resize history panel
- [ ] All keyboard shortcuts functional
- [ ] Switching source types updates UI correctly
- [ ] Looks professional with Material Design theming

---

## Step 24 — Recorder: Polish & Error Handling
**Target: [REC]** | Ref: REC Phase 10

### What to build
1. **Error handling:**
   - All async ops in try-catch with user-friendly messages
   - Transient errors: Material Design Snackbar (toast at bottom)
   - Critical errors: modal dialog (source disconnect during recording, shared folder lost)
2. **Edge cases:**
   - Source disconnect mid-recording: save what's captured, mark as partial, warn before submit
   - Shared folder unavailable on submit: queue locally, retry when available
   - Duplicate submission prevention: disable submit after first click, check file exists
   - Crash recovery: check for orphaned temp files on startup
   - Very short clips (< 3s): warning + confirmation
   - Very long clips (> 10min): warning
3. **Configuration validation on startup:**
   - Validate shared folder writable
   - Validate FFmpeg/yt-dlp paths
   - Check audio device availability
   - First-run setup wizard if critical config missing
4. **Lifecycle:**
   - Graceful shutdown: warn if recording in progress
   - Single instance enforcement (Mutex)
   - Minimize to system tray option

### Done when
- [ ] No unhandled exceptions in normal usage
- [ ] Shared folder disconnect during recording → graceful handling
- [ ] First-run experience works (setup wizard or settings prompt)
- [ ] Only one instance can run at a time
- [ ] Minimize to tray works

---

## Step 25 — Server: Polish, Notifications & Monitoring
**Target: [SRV]** | Ref: SRV Phase 13

### What to build
1. **Windows toast notifications:**
   - Job completed (with title)
   - Job failed (with error summary)
   - Queue empty
   - Shared folder disconnected
   - Disk space warning
2. **System tray integration:**
   - Minimize to tray
   - Icon color: Green=idle, Blue=processing, Yellow=warning, Red=error
   - Context menu: Open, Pause Queue, Resume Queue, Exit
   - Tooltip: "OnAirCut Render - 2 pending, 1 rendering"
3. **Daily statistics on dashboard:**
   - Total clips today, average render time, total output duration
   - OCR success rate, failure rate
   - Disk space used today
4. **Logging finalization:**
   - Serilog: file sink (shared folder + local), log rotation (30 days)
   - Structured logging with job context

### Done when
- [ ] Toast notifications appear for completed/failed jobs
- [ ] Tray icon reflects current status
- [ ] Dashboard shows daily statistics
- [ ] Logs rotate correctly and are searchable

---

## ✓ MILESTONE F — Production Ready
> Both applications are feature-complete, polished, and handle errors gracefully.
> **Final Test Checklist:**
> - [ ] Full session: process 20+ clips from a 1-hour news recording
> - [ ] Live feed source works end-to-end
> - [ ] YouTube source works end-to-end
> - [ ] OCR correctly names at least 80% of clips
> - [ ] Failed OCR falls back to timestamp naming
> - [ ] Ad insertion (TVC + overlay) produces correct output
> - [ ] Queue handles burst of 5+ rapid submissions
> - [ ] Server crash recovery works
> - [ ] Shared folder disconnect/reconnect handled
> - [ ] History/search works from both Recorder and Server
> - [ ] Average time from record-stop to final output < 3 minutes

---

## Appendix A: Step Dependency Map

```
Step 01  [BOTH]  Scaffolding ─────────────────────────────────────────────┐
Step 02  [BOTH]  PoC: FFmpeg + OCR + Queue ───────────────────────────────┤
Step 03  [REC]   App Shell & Settings ────────── needs 01 ────────────────┤
Step 04  [SRV]   App Shell, Settings, SQLite ─── needs 01 ────────────────┤
Step 05  [REC]   Video Playback & Local File ─── needs 03 ────────────────┤
Step 06  [REC]   Recording Controls ──────────── needs 05 ────────────────┤
                                                                           │
         ✓ MILESTONE A — Manual clip extraction works                      │
                                                                           │
Step 07  [SRV]   Job Queue Watcher ───────────── needs 04 ────────────────┤
Step 08  [REC]   Ad Set Selection & Submit ───── needs 06, 07 ────────────┤
                                                                           │
         ✓ MILESTONE B — Recorder-to-Server handoff works                  │
                                                                           │
Step 09  [SRV]   Frame Extraction ────────────── needs 07 ────────────────┤
Step 10  [SRV]   FFmpeg Render Pipeline ──────── needs 07, 02 ────────────┤
Step 11  [SRV]   OCR Pipeline ───────────────── needs 09, 02 ────────────┤
Step 12  [SRV]   Output Organization ─────────── needs 10, 11 ────────────┤
Step 13  [SRV]   Pipeline Orchestrator ───────── needs 09-12 ─────────────┤
                                                                           │
         ✓ MILESTONE C — Full pipeline works end-to-end                    │
                                                                           │
Step 14  [SRV]   Dashboard & Queue GUI ───────── needs 13 ────────────────┤
Step 15  [SRV]   Data API for Recorder ───────── needs 04 ────────────────┤
Step 16  [REC]   History & Search Panel ──────── needs 15 ────────────────┤
Step 17  [REC]   OCR Region Config ───────────── needs 05 ────────────────┤
                                                                           │
         ✓ MILESTONE D — Complete core workflow                            │
                                                                           │
Step 18  [SRV]   History, Search & Logs GUI ──── needs 14 ────────────────┤
Step 19  [REC]   Live Feed Source ────────────── needs 06 ────────────────┤
Step 20  [REC]   YouTube Source ──────────────── needs 06 ────────────────┤
                                                                           │
         ✓ MILESTONE E — All three input sources working                   │
                                                                           │
Step 21  [SRV]   Error Recovery ──────────────── needs 13 ────────────────┤
Step 22  [SRV]   Performance Optimization ────── needs 13 ────────────────┤
Step 23  [REC]   Final Layout Assembly ───────── needs 16-20 ─────────────┤
Step 24  [REC]   Polish & Error Handling ─────── needs 23 ────────────────┤
Step 25  [SRV]   Polish & Monitoring ─────────── needs 18, 21, 22 ────────┘

         ✓ MILESTONE F — Production ready
```

## Appendix B: Cross-Reference to Individual Plans

| Step | App    | Individual Plan Reference                |
|------|--------|------------------------------------------|
| 01   | BOTH   | REC Phase 0 + SRV Phase 0                |
| 02   | BOTH   | SRV Plan — Critical PoC Items            |
| 03   | REC    | REC Phase 1                              |
| 04   | SRV    | SRV Phase 1                              |
| 05   | REC    | REC Phase 2                              |
| 06   | REC    | REC Phase 3                              |
| 07   | SRV    | SRV Phase 2                              |
| 08   | REC    | REC Phase 4                              |
| 09   | SRV    | SRV Phase 3                              |
| 10   | SRV    | SRV Phase 5                              |
| 11   | SRV    | SRV Phase 4                              |
| 12   | SRV    | SRV Phase 6                              |
| 13   | SRV    | SRV Phase 10                             |
| 14   | SRV    | SRV Phase 7                              |
| 15   | SRV    | SRV Phase 9                              |
| 16   | REC    | REC Phase 5                              |
| 17   | REC    | REC Phase 6                              |
| 18   | SRV    | SRV Phase 8                              |
| 19   | REC    | REC Phase 7                              |
| 20   | REC    | REC Phase 8                              |
| 21   | SRV    | SRV Phase 11                             |
| 22   | SRV    | SRV Phase 12                             |
| 23   | REC    | REC Phase 9                              |
| 24   | REC    | REC Phase 10                             |
| 25   | SRV    | SRV Phase 13                             |

## Appendix C: App Switching Pattern

Visual overview of when you switch between apps:

```
Step:  01  02  03  04  05  06  ||  07  08  ||  09  10  11  12  13  ||  14  15  16  17  ||  18  19  20  ||  21  22  23  24  25
App:   B   B   R   S   R   R   A   S   R   B   S   S   S   S   S   C   S   S   R   R   D   S   R   R   E   S   S   R   R   S
                                                                                                         ─────────────────────
Legend: B=Both, R=Recorder, S=Server, A/B/C/D/E=Milestones                                              Final hardening phase
```

The natural rhythm: build Recorder features until Server needs to consume them, then build Server processing, then return to Recorder for display/polish.
