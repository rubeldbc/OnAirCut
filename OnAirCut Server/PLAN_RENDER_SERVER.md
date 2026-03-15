# OnAirCut Render Server Application - Implementation Plan

> Background processing WPF application that watches for incoming jobs, renders video with ad insertion using FFmpeg, extracts frames, performs Bangla OCR for auto-naming, and manages the output pipeline.

---

## Phase 0: Shared Infrastructure (Joint with Recorder)

> This phase is shared with the Recorder app. See PLAN_RECORDER.md Phase 0 for full details.

### Summary of shared components needed:
- Solution structure: `OnAirCut.Core` class library + `OnAirCut.RenderServer` WPF app
- Shared models: `JobFile`, `AdSetConfig`, `OcrProfile`, `ProcessedStory`, enums
- Shared utilities: `TitleSanitizer`, `JobFileHelper`, `SharedFolderValidator`
- Shared folder structure definition and initializer

---

## Phase 1: Application Shell, Settings & SQLite Setup

### 1.1 WPF App Bootstrap with Material Design
- Configure `App.xaml` with Material Design theme (match Recorder app styling)
- Create `MainWindow.xaml` with:
  - Title bar: "OnAirCut Render Server", clock, shared folder status, CPU/RAM usage
  - Navigation: tab-based or sidebar layout
  - Sections: Dashboard, Queue, Job Detail, History/Search, Settings, Logs

### 1.2 MVVM Infrastructure
- Same pattern as Recorder: CommunityToolkit.Mvvm, DI container
- Register all services and view models

### 1.3 NuGet Packages (Render Server)
- `MaterialDesignThemes` + `MaterialDesignColors`
- `CommunityToolkit.Mvvm`
- `Microsoft.Data.Sqlite` — SQLite read/write
- `Serilog` + `Serilog.Sinks.File`
- `System.Text.Json`
- `CliWrap` — Clean FFmpeg/FFprobe process management
- `Tesseract` (Tesseract OCR .NET wrapper) — For Bangla OCR
  - Or `PaddleOCRSharp` as alternative for better Bangla support
- `SixLabors.ImageSharp` — Image cropping, preprocessing for OCR
- Optional: `Microsoft.ML.OnnxRuntime` — If using ONNX-based OCR model

### 1.4 Settings System
- `RenderServerSettings` model:
  - SharedFolderPath
  - FFmpegPath (full path to ffmpeg.exe)
  - FFprobePath (full path to ffprobe.exe)
  - OcrEnginePath (Tesseract data path or PaddleOCR model path)
  - OcrLanguage (default: "ben" for Bengali)
  - LocalDatabasePath (local disk path for SQLite, e.g., `C:\OnAirCut\onaircut.db`)
  - TempWorkingFolder (local fast disk preferred)
  - MaxConcurrentRenders (default: 1, max: CPU cores - 1)
  - MaxConcurrentFrameExtractions (default: 2)
  - JobPollIntervalMs (default: 2000)
  - FileReadyCheckIntervalMs (default: 1000)
  - FileReadyStableSeconds (default: 3)
  - OutputVideoCodec (default: "libx264")
  - OutputVideoPreset (default: "fast")
  - OutputVideoCRF (default: 18)
  - OutputAudioCodec (default: "aac")
  - OutputAudioBitrate (default: "192k")
  - CleanupWorkingFolderAfterDays (default: 7)
  - FrameExtractionCount (default: 20)
  - OcrMultiFrameCount (default: 5, how many frames to OCR for consensus)
- Settings stored as JSON: `%AppData%\OnAirCut\renderserver_settings.json`
- Settings GUI page with all fields, validation, and save/reset

### 1.5 SQLite Database Setup
- Database file location: configurable local path (NOT on shared folder)
- Default: `C:\OnAirCut\Data\onaircut.db`
- On startup:
  - Create database file if not exists
  - Run migration/schema creation
  - Verify schema version

### 1.6 Database Schema Creation
```sql
-- Schema version tracking
CREATE TABLE IF NOT EXISTS SchemaVersion (
    Version INTEGER PRIMARY KEY,
    AppliedAt TEXT NOT NULL
);

-- Main processed stories table
CREATE TABLE IF NOT EXISTS ProcessedStories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    JobId TEXT NOT NULL UNIQUE,
    TitleRaw TEXT,
    TitleNormalized TEXT,
    SafeFolderName TEXT,
    SourceType TEXT NOT NULL,
    SourceName TEXT,
    OnAirDateTime TEXT NOT NULL,
    ClipStartTime TEXT,
    ClipEndTime TEXT,
    DurationSeconds REAL,
    AdSetName TEXT,
    OverlaySetName TEXT,
    RawClipPath TEXT NOT NULL,
    OutputFolderPath TEXT,
    OutputVideoPath TEXT,
    FramesPath TEXT,
    OcrConfidence REAL,
    OcrProfileUsed TEXT,
    SubmittedBy TEXT,
    SubmittedAt TEXT NOT NULL,
    ProcessingStartedAt TEXT,
    ProcessedAt TEXT,
    Status TEXT NOT NULL DEFAULT 'Pending',
    ErrorMessage TEXT,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Indexes for search
CREATE INDEX IF NOT EXISTS idx_stories_title ON ProcessedStories(TitleNormalized);
CREATE INDEX IF NOT EXISTS idx_stories_date ON ProcessedStories(OnAirDateTime);
CREATE INDEX IF NOT EXISTS idx_stories_folder ON ProcessedStories(SafeFolderName);
CREATE INDEX IF NOT EXISTS idx_stories_status ON ProcessedStories(Status);
CREATE INDEX IF NOT EXISTS idx_stories_jobid ON ProcessedStories(JobId);

-- Job processing log (detailed step tracking)
CREATE TABLE IF NOT EXISTS JobLog (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    JobId TEXT NOT NULL,
    Step TEXT NOT NULL,
    Status TEXT NOT NULL,
    Message TEXT,
    StartedAt TEXT,
    CompletedAt TEXT,
    DurationMs INTEGER,
    FOREIGN KEY (JobId) REFERENCES ProcessedStories(JobId)
);

-- OCR results (detailed per-frame results for debugging)
CREATE TABLE IF NOT EXISTS OcrResults (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    JobId TEXT NOT NULL,
    FrameIndex INTEGER NOT NULL,
    FramePath TEXT,
    RawText TEXT,
    Confidence REAL,
    ProfileUsed TEXT,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (JobId) REFERENCES ProcessedStories(JobId)
);

-- Ad sets used (for tracking/reporting)
CREATE TABLE IF NOT EXISTS AdSetUsage (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    JobId TEXT NOT NULL,
    AdSetName TEXT NOT NULL,
    TvcPath TEXT,
    OverlayPath TEXT,
    InsertMode TEXT,
    UsedAt TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (JobId) REFERENCES ProcessedStories(JobId)
);
```

### 1.7 SQLite Repository
- `SqliteRepository` class with methods:
  - `InsertStory(ProcessedStory)` — Insert new record when job starts
  - `UpdateStoryStatus(jobId, status, errorMessage?)` — Update status at each step
  - `UpdateStoryOcr(jobId, titleRaw, titleNormalized, safeFolderName, confidence)` — After OCR
  - `UpdateStoryOutput(jobId, outputFolderPath, outputVideoPath, framesPath, processedAt)` — After render complete
  - `SearchStories(query, dateFrom?, dateTo?, status?, source?, limit, offset)` — Full text search
  - `GetRecentStories(count)` — Last N processed stories
  - `GetStoryByJobId(jobId)` — Single story detail
  - `GetTodayStats()` — Counts by status for dashboard
  - `InsertJobLog(jobId, step, status, message)` — Step-level logging
  - `InsertOcrResult(jobId, frameIndex, rawText, confidence)` — Per-frame OCR result
- Use parameterized queries everywhere (no string concatenation)
- WAL mode enabled for better concurrent read performance:
  `PRAGMA journal_mode=WAL;`

---

## Phase 2: Job Queue Watcher & File Ready Detection

### 2.1 Job Queue Scanner Service
- `JobWatcherService` — Background service (runs on dedicated thread or Task)
- Poll `{SharedFolder}\Jobs\Pending\` at configured interval (default 2 seconds)
- On detecting new `.json` job file:
  1. Read and deserialize `JobFile`
  2. Validate required fields
  3. Attempt to move file from `Pending\` to `Processing\` (atomic rename)
     - If rename fails (another instance got it first), skip
  4. Insert record into SQLite with status "Processing"
  5. Enqueue into internal processing queue
- FileSystemWatcher as primary trigger, polling as fallback:
  - `FileSystemWatcher` on `Jobs\Pending\` for `.json` files
  - Poll every N seconds as safety net (FSW can miss events over network)

### 2.2 File Ready Checker
- `FileReadyChecker` — Verifies raw clip file is fully written before processing
- Check strategy:
  1. File exists at `rawClipPath`
  2. File size > 0
  3. File size stable: check twice with N-second gap, sizes match
  4. File is not locked: attempt to open with `FileShare.Read`
  5. Optional: run `ffprobe` to verify container integrity
- Retry logic:
  - Check every `FileReadyCheckIntervalMs` (default 1 second)
  - Max wait: 60 seconds
  - If timeout: mark job as Failed with "Raw clip file not ready"
- Log each check attempt for debugging

### 2.3 Internal Processing Queue
- `ConcurrentQueue<JobFile>` or `Channel<JobFile>` for job dispatch
- Worker pool: configurable number of concurrent workers (default 1)
- Each worker processes one job at a time through the full pipeline
- Queue depth visible in GUI

### 2.4 Job Locking
- When job moves from Pending to Processing, create a `.lock` file:
  `{SharedFolder}\Jobs\Processing\{jobId}.lock`
- Lock file contains: hostname, PID, timestamp
- On completion/failure, lock file removed
- On startup, check for orphaned lock files from previous crashes:
  - If lock file exists but process is dead, recover the job

---

## Phase 3: Frame Extraction Pipeline

### 3.1 Frame Extraction Service
- `FrameExtractionService` — Extracts N still frames from raw clip
- Uses FFmpeg via `CliWrap`:
  ```
  ffmpeg -i {input} -vf "select='not(mod(n\,{interval}))',setpts=N/FRAME_RATE/TB"
         -frames:v 20 -q:v 2 {outputPattern}
  ```
  - Where `interval` = total_frames / 20 (evenly spaced)
- Alternative approach for time-based extraction:
  ```
  ffmpeg -i {input} -vf "fps=1/{interval_seconds}" -q:v 2 {outputPattern}
  ```
- Output to: `{SharedFolder}\Working\{jobId}\frames\frame_%03d.jpg`
- After extraction, count files to verify all 20 extracted

### 3.2 Frame Extraction Timing
- Frame extraction starts in parallel with render preparation (not after render)
- Runs as separate FFmpeg instance from the render process
- Reports progress: X of 20 frames extracted

### 3.3 FFprobe Integration
- Before frame extraction, probe the input file:
  ```
  ffprobe -v quiet -print_format json -show_format -show_streams {input}
  ```
- Extract metadata:
  - Duration
  - Resolution (width x height)
  - Frame rate
  - Codec
  - Audio channels and sample rate
- Store in job context for downstream use (render, OCR)
- Validate: reject files with no video stream, zero duration, or corrupt headers

---

## Phase 4: OCR Pipeline

### 4.1 OCR Engine Integration
- Primary engine: **Tesseract OCR** with Bengali (ben) trained data
  - `Tesseract` NuGet package (wrapper for tesseract C++ engine)
  - Download `ben.traineddata` and `eng.traineddata` (for mixed text)
  - Store in configurable data path
- Alternative engine: **PaddleOCRSharp** (better for complex scripts)
  - Evaluate during Phase 0 proof-of-concept
  - May provide better accuracy for styled broadcast text

### 4.2 OCR Preprocessing Pipeline
For each frame going through OCR:
1. **Load frame** — ImageSharp loads the extracted JPEG
2. **Crop** — Apply OCR profile's crop region (x, y, width, height)
3. **Resize** — Upscale by configured factor (default 2x) for better OCR accuracy
4. **Convert to grayscale** — Remove color noise
5. **Apply threshold** — Based on profile setting:
   - None: skip
   - Binary: Otsu threshold
   - Adaptive: adaptive Gaussian threshold
6. **Sharpen** — Optional mild sharpening
7. **Save preprocessed image** — To temp folder for debugging
8. **Run OCR** — Feed to Tesseract/PaddleOCR
9. **Return result** — Raw text + confidence score

### 4.3 Multi-Frame Consensus OCR
- Don't rely on a single frame — broadcast text may have animation or transition artifacts
- Strategy:
  1. From the 20 extracted frames, select `OcrMultiFrameCount` frames (default 5)
     - Selection: evenly spaced from the middle 60% of the clip (avoid intro/outro)
  2. Run OCR on each selected frame
  3. Store all results in `OcrResults` table
  4. Consensus logic:
     - Trim whitespace from all results
     - Remove empty/very short results (< 3 characters)
     - Group identical or near-identical results (Levenshtein distance < 3)
     - Pick the most frequent result
     - If tie, pick the one with highest confidence
     - If no consensus (all different), pick highest confidence
  5. Log all individual results and the chosen winner

### 4.4 Title Processing Pipeline
After consensus OCR result:
1. `TitleRaw` — Store the exact OCR output
2. `TitleNormalized` — Clean up:
   - Trim whitespace
   - Remove control characters
   - Normalize Unicode (NFC form)
   - Collapse multiple spaces into single space
3. `SafeFolderName` — Filesystem-safe version:
   - Replace invalid path characters (`\ / : * ? " < > |`) with underscore
   - Replace spaces with underscores
   - Remove leading/trailing dots and spaces
   - Truncate to 100 characters max
   - If empty after sanitization, use fallback: `Untitled_{jobId}`

### 4.5 Fallback Strategy
OCR will fail sometimes. Handle gracefully:
- If all frame OCR confidence < 30%: use fallback name
- If no text detected in any frame: use fallback name
- Fallback naming pattern: `Story_{yyyyMMdd}_{HHmmss}`
- Mark story in DB with `OcrConfidence = 0` and flag for manual review
- GUI shows "OCR failed" indicator — operator can manually rename later

### 4.6 OCR Profile Loading
- Read OCR profiles from `{SharedFolder}\Assets\OcrProfiles\*.json`
- Match profile to job by `ocrProfileName` field in job file
- If profile not found, try default profile
- If no profile exists, skip OCR and use fallback naming

---

## Phase 5: FFmpeg Render Pipeline

### 5.1 Render Service Architecture
- `FfmpegRenderService` — Orchestrates the final video render
- Input: raw clip + ad set config
- Output: final video with TVC inserted and overlay applied
- All FFmpeg operations via `CliWrap` for clean process management

### 5.2 Render Strategy Decision
Based on `AdSetConfig.InsertMode`:

**Mode A: No Ad (passthrough with overlay only)**
```
ffmpeg -i {rawClip} -i {overlay}
       -filter_complex "[0:v][1:v]overlay=0:0:enable='between(t,{start},{end})'[v]"
       -map "[v]" -map "0:a" -c:v {codec} -preset {preset} -crf {crf}
       -c:a {audioCodec} -b:a {audioBitrate} {output}
```

**Mode B: TVC at fixed timestamp**
```
Step 1: Split raw clip at insertion point
  ffmpeg -i {rawClip} -t {insertAtSec} -c copy part1.mp4
  ffmpeg -i {rawClip} -ss {insertAtSec} -c copy part2.mp4

Step 2: Concat part1 + TVC + part2 with overlay
  Create concat list file:
    file 'part1.mp4'
    file 'tvc.mp4'
    file 'part2.mp4'

  ffmpeg -f concat -safe 0 -i list.txt -i {overlay}
         -filter_complex "[0:v][1:v]overlay=0:0:enable='between(t,{oStart},{oEnd})'[v]"
         -map "[v]" -map "0:a" -c:v {codec} -preset {preset} -crf {crf}
         -c:a {audioCodec} -b:a {audioBitrate} {output}
```

**Mode C: TVC at midpoint**
- Same as Mode B but `insertAtSec = clipDuration / 2`

### 5.3 FFmpeg Command Builder
- `FfmpegCommandBuilder` class that constructs the full command based on:
  - Input file path
  - Ad set config (TVC path, overlay path, insertion rules)
  - Output settings (codec, preset, CRF, resolution)
  - Temp file paths for intermediate files
- Returns complete argument string
- Logs the full FFmpeg command for debugging/reproduction

### 5.4 Render Execution
- Execute FFmpeg via CliWrap:
  - Capture stdout and stderr in real-time
  - Parse progress from stderr: `frame=`, `time=`, `speed=`
  - Calculate and report percentage complete
  - Timeout: configurable (default 10 minutes per job)
- Check exit code:
  - 0 = success
  - Non-zero = failure, capture stderr as error message
- Output to temp location first: `{Working}\{jobId}\temp_output.mp4`
- Only move to final location on success

### 5.5 Render Progress Tracking
- Parse FFmpeg stderr output for progress:
  ```
  frame= 1234 fps= 45 q=28.0 size= 12345kB time=00:01:23.45 speed=1.5x
  ```
- Extract: current time, speed, estimated remaining time
- Update SQLite job status with current step info
- Update GUI in real-time

### 5.6 Output Resolution & Format Normalization
- Before rendering, check if source matches target output specs
- If source is different resolution than ad set's `outputWidth x outputHeight`:
  - Scale source to match
  - Or letterbox/pillarbox
- Ensure consistent frame rate across all segments (raw clip + TVC)
- Audio: normalize sample rate to 48kHz, channels to stereo

---

## Phase 6: Output Organization & Naming

### 6.1 Output Folder Structure Creation
After render completes and OCR title is available:
```
{SharedFolder}\Output\{date}\{SafeFolderName}\
  ├── {SafeFolderName}.mp4          (final rendered video)
  ├── metadata.json                  (job metadata)
  └── frames\
      ├── frame_001.jpg
      ├── frame_002.jpg
      └── ... (20 frames)
```

### 6.2 Output Organization Steps
1. Create date folder: `{SharedFolder}\Output\{yyyy-MM-dd}\`
2. Create story folder: `{date}\{SafeFolderName}\`
   - If folder already exists (duplicate title), append suffix: `{SafeFolderName}_002`
3. Move rendered video from Working to Output folder
4. Rename video file to `{SafeFolderName}.mp4`
5. Move extracted frames from Working to `{Output}\frames\`
6. Generate `metadata.json`:
   ```json
   {
     "jobId": "JOB-20260315-201530-001",
     "title": "Raw OCR title here",
     "titleNormalized": "Cleaned title",
     "onAirDateTime": "2026-03-15T20:15:30",
     "duration": 92.5,
     "source": "Channel 24",
     "adSet": "Set_A",
     "ocrConfidence": 87.5,
     "renderedAt": "2026-03-15T20:17:45",
     "renderDuration": 15.3,
     "frameCount": 20,
     "outputResolution": "1920x1080",
     "outputCodec": "h264"
   }
   ```
7. Update SQLite with final paths
8. Move job file from `Processing\` to `Done\`

### 6.3 Duplicate Title Handling
- Check if folder already exists before creating
- If exists, try numbered suffix: `_002`, `_003`, etc.
- Also check SQLite for same title + same date
- Log warning when duplicate detected
- Recorder GUI should show duplicate indicator

### 6.4 Cleanup Working Directory
- After successful output organization, delete Working folder for the job
- Failed jobs: keep Working folder for debugging, clean up after N days
- Configurable cleanup policy in settings

---

## Phase 7: Render Server GUI — Dashboard & Queue

### 7.1 Dashboard Screen
Main overview visible when app starts:
```
+------------------------------------------------------------------+
| OnAirCut Render Server        [Clock]  [Shared:OK]  [CPU: 45%]  |
+------------------------------------------------------------------+
|                                                                   |
|  +----------+  +----------+  +----------+  +----------+          |
|  | PENDING  |  | ACTIVE   |  | DONE     |  | FAILED   |          |
|  |    3     |  |    1     |  |   47     |  |    2     |          |
|  +----------+  +----------+  +----------+  +----------+          |
|                                                                   |
|  Current Job: JOB-20260315-201530-001                            |
|  Step: Rendering (62%) - Speed: 1.8x - ETA: 00:00:12            |
|  [==========================>              ] 62%                   |
|                                                                   |
|  Recent Activity:                                                 |
|  20:17:45  JOB-001  Completed  "Economy report..."  [1:32]      |
|  20:16:12  JOB-002  Rendering  "Sports update..."   [2:10]      |
|  20:15:01  JOB-003  Pending    (waiting)                         |
|                                                                   |
+------------------------------------------------------------------+
```

### 7.2 Dashboard Components
- **Stat Cards** — 4 cards showing today's counts: Pending, Active, Completed, Failed
- **Current Job Progress** — Active render with:
  - Job ID
  - Current processing step (FileCheck / FrameExtraction / OCR / Rendering / Organizing)
  - Progress bar with percentage
  - Speed indicator
  - ETA
- **Recent Activity List** — Last 10 events with timestamp, job ID, status, title preview
- **System Health** — CPU usage, disk space on output drive, shared folder status

### 7.3 Queue Screen
- DataGrid showing all jobs in all states:
  - Columns: JobId, Title (if OCR done), Source, Ad Set, Status, Current Step, Elapsed, Error
  - Color-coded rows by status
  - Sortable columns
  - Auto-refresh every 2 seconds
- Actions per job (context menu or buttons):
  - View Details
  - Retry (for failed jobs)
  - Cancel (for pending jobs)
  - Open Output Folder (for completed jobs)
  - Copy FFmpeg Command (for debugging)

### 7.4 Job Detail Screen
- Accessible by double-clicking a job in queue
- Shows all job information:
  - Job metadata (all fields from job file)
  - Processing timeline (each step with start/end time and duration)
  - OCR results: all per-frame results + chosen consensus
  - Crop region visualization (show where OCR looked on the frame)
  - Frame thumbnails grid (20 frames as clickable thumbnails)
  - FFmpeg command used
  - FFmpeg log output (scrollable)
  - Error details (if failed)
  - Output file info (size, format, paths)
- Action buttons: Retry, Open Output, Open Raw Clip, Open Working Folder

---

## Phase 8: Render Server GUI — History, Search & Logs

### 8.1 History/Search Screen
- Same search capability as Recorder's history panel but more detailed
- Search inputs:
  - Full-text search (title, folder name)
  - Date range picker
  - Source/channel filter
  - Status filter (multi-select)
  - Ad set filter
  - Duration range (min/max seconds)
- Results grid:
  - All ProcessedStories columns
  - Sortable, paginated
  - Export to CSV button
- Row actions:
  - Open output folder
  - Open in default video player
  - Copy title
  - Copy output path
  - View job details

### 8.2 Logs Screen
- Real-time log viewer:
  - Tailing Serilog output
  - Color-coded by level (Debug=gray, Info=white, Warning=yellow, Error=red)
  - Filter by level
  - Filter by job ID
  - Search within logs
  - Auto-scroll toggle
  - Clear display button (doesn't delete log files)
- Log file browser:
  - List of log files by date
  - Open in external editor

### 8.3 Settings Screen (Render Server)
- All settings from Phase 1.4 exposed in GUI
- Sections:
  - **Paths**: Shared folder, FFmpeg, FFprobe, OCR engine, database, temp folder
  - **Rendering**: Codec, preset, CRF, audio settings, max concurrent renders
  - **OCR**: Engine selection, language, multi-frame count, confidence threshold
  - **Queue**: Poll interval, file ready check interval, stable seconds
  - **Cleanup**: Working folder retention days, log retention
- Path fields with browse buttons
- "Test FFmpeg" button — runs `ffmpeg -version` and shows output
- "Test OCR" button — runs OCR on a sample image and shows result
- "Validate Shared Folder" button — checks all required subfolders
- Save / Reset / Apply buttons

---

## Phase 9: Data API for Recorder Access

### 9.1 Lightweight Read API
- Minimal HTTP API running inside the Render Server app
- Framework: ASP.NET Core Minimal API (hosted in-process via `WebApplication`)
- Binds to configurable port (default: `http://0.0.0.0:5123`)
- Endpoints:

```
GET /api/health
  Response: { "status": "ok", "pendingJobs": 3, "activeJob": "JOB-..." }

GET /api/stories/search?q={text}&dateFrom={date}&dateTo={date}&status={status}&limit={n}&offset={n}
  Response: { "items": [...], "total": 152 }

GET /api/stories/recent?count={n}
  Response: [ { "jobId": ..., "title": ..., "status": ... }, ... ]

GET /api/stories/{jobId}
  Response: { full ProcessedStory object }

GET /api/jobs/stats
  Response: { "pending": 3, "processing": 1, "completed": 47, "failed": 2, "todayTotal": 53 }

GET /api/adsets
  Response: [ { "name": "Set_A", "tvcFile": "tvc.mp4", ... }, ... ]
```

### 9.2 API Security (Minimal)
- LAN-only access (not exposed to internet)
- Optional: simple API key in header (`X-Api-Key`)
- CORS allowed for same network
- Rate limiting: basic, to prevent accidental flooding

### 9.3 Recorder Integration
- Recorder app uses `HttpClient` to call these endpoints
- Falls back to direct SQLite read if API is unreachable
- Configurable in Recorder settings: API URL or direct DB path

---

## Phase 10: Job Processing Pipeline Orchestrator

### 10.1 Pipeline Orchestrator
- `JobPipelineOrchestrator` — Coordinates the full processing flow for a single job
- Steps executed sequentially for each job:

```
1. Validate job file
2. Wait for raw clip file ready
3. Insert/update SQLite record (status: Processing)
4. Probe input file (FFprobe)
5. Start frame extraction (parallel, non-blocking)
6. Start OCR pipeline (after frame extraction completes)
7. Start FFmpeg render (can overlap with OCR if independent)
8. Wait for render completion
9. Wait for OCR completion
10. Organize output (create folders, move files, rename)
11. Update SQLite (status: Completed, paths, title)
12. Move job file to Done folder
13. Clean up working directory
```

### 10.2 Step Execution with Logging
- Each step wrapped in try-catch
- On step start: log + update SQLite JobLog
- On step complete: log + update SQLite JobLog with duration
- On step failure: log error + update SQLite + decide: retry or fail job

### 10.3 Failure Handling
- Per-step retry policy:
  - File ready check: retry up to 60 seconds
  - Frame extraction: retry once
  - OCR: retry once, then fallback to default naming
  - FFmpeg render: retry once with different settings (lower quality), then fail
  - Output organization: retry once
- On final failure:
  - Move job file to `Failed\` folder
  - Keep Working directory for debugging
  - Update SQLite with error message
  - Log full error with stack trace
  - GUI shows failure notification

### 10.4 Cancellation Support
- Each step checks a CancellationToken
- GUI can cancel active jobs
- FFmpeg process killed gracefully (send 'q' to stdin, then kill if timeout)
- Partial output cleaned up

### 10.5 Parallel Pipeline Optimization
- For a single job, some steps can overlap:
  ```
  Timeline:
  [File Ready] -> [FFprobe] -> [Frame Extract + Render Start]
                                     |
                                     v
                              [OCR (after frames ready)]
                                     |
                                     v
                              [Output Organization (after both render + OCR done)]
  ```
- Frame extraction and render can start simultaneously from the same input
- OCR depends on extracted frames
- Output organization depends on both render result and OCR result

---

## Phase 11: Error Recovery & Robustness

### 11.1 Crash Recovery
- On startup, check for interrupted jobs:
  - Scan `Jobs\Processing\` for any job files
  - Check if lock file exists and if owning process is still alive
  - If orphaned: move back to `Pending\` for reprocessing
  - Or move to `Failed\` if already retried
- Check `Working\` for incomplete working directories
  - If matching job is in Failed, keep for debugging
  - If no matching job, flag for manual cleanup

### 11.2 Shared Folder Resilience
- If shared folder becomes unavailable during processing:
  - Pause job scanning
  - Keep active job in memory, retry file operations with backoff
  - Show "Shared folder disconnected" in GUI
  - Auto-resume when folder returns
- Health check every 5 seconds
- Log all connectivity events

### 11.3 FFmpeg Crash Handling
- If FFmpeg process crashes (non-zero exit, killed, timeout):
  - Capture stderr output
  - Save FFmpeg log to `{Working}\{jobId}\ffmpeg_error.log`
  - Store error in SQLite
  - Clean up partial output files
  - Retry once with different settings if applicable

### 11.4 Disk Space Management
- Before starting any job, check available disk space:
  - On shared folder (for output)
  - On local temp drive (for working files)
- If below threshold (default 5 GB): pause queue, show warning
- After each completed job, update space indicator

### 11.5 Graceful Shutdown
- On app close:
  - If render is in progress, ask: "Render in progress. Cancel and exit, or wait?"
  - If waiting: block shutdown until current job completes
  - If canceling: kill FFmpeg, mark job for retry, then exit
  - Flush all logs
  - Close SQLite connection properly

---

## Phase 12: Performance Optimization

### 12.1 Render Speed Optimization
- FFmpeg preset selection based on deadline pressure:
  - Default: `-preset fast` (good balance)
  - Rush mode: `-preset ultrafast` (lower quality, faster)
  - Quality mode: `-preset medium` (higher quality, slower)
- Hardware acceleration detection:
  - Check for NVENC: `ffmpeg -encoders | grep nvenc`
  - Check for QSV: `ffmpeg -encoders | grep qsv`
  - If available, offer as option in settings
  - NVENC example: `-c:v h264_nvenc -preset p4 -cq 20`
- Avoid unnecessary re-encoding:
  - If source codec matches output codec and no overlay needed, use `-c copy`
  - Smart detection of when re-encode is truly needed

### 12.2 Queue Priority
- Job priority levels:
  - Normal: FIFO order
  - High: jump to front of queue (for breaking news)
- Priority set by Recorder operator via job file field
- GUI allows re-ordering queue manually (drag-and-drop or up/down buttons)

### 12.3 Resource Management
- FFmpeg process priority: Below Normal (don't starve the GUI)
- Limit FFmpeg thread count: `-threads {n}` based on settings
- Memory-conscious frame handling: process and discard, don't hold all 20 frames in memory

---

## Phase 13: Polish, Notifications & Monitoring

### 13.1 Notifications
- Windows toast notifications for:
  - Job completed successfully (with title)
  - Job failed (with error summary)
  - Queue empty (all jobs done)
  - Shared folder disconnected
  - Disk space warning
- In-app notification center: list of recent notifications with dismiss

### 13.2 System Tray Integration
- Minimize to system tray
- Tray icon shows status:
  - Green: idle, all jobs done
  - Blue: processing
  - Yellow: warning (disk space, folder issue)
  - Red: error (jobs failing)
- Tray context menu: Open, Pause Queue, Resume Queue, Exit
- Tray tooltip: "OnAirCut Render - 2 pending, 1 rendering"

### 13.3 Daily Statistics
- Dashboard section showing:
  - Total clips processed today
  - Average render time
  - Total output duration (sum of all clip durations)
  - OCR success rate
  - Failure rate
  - Disk space used today
- Simple bar chart of hourly processing volume (optional, can be text-only for v1)

### 13.4 Application Logging
- Serilog configuration:
  - File sink: `{SharedFolder}\Logs\renderserver_{date}.log` (shared, for debugging)
  - Local file sink: `%AppData%\OnAirCut\Logs\renderserver_{date}.log`
  - Console sink (debug builds only)
- Log rotation: keep last 30 days
- Structured logging with job context: `Log.ForContext("JobId", jobId)`

---

## Phase Summary & Dependencies

```
Phase 0:  Shared Infrastructure ──────────────────────────────────┐
Phase 1:  Shell, Settings & SQLite ───────────────────────────────┤
Phase 2:  Job Queue & File Ready ──────── depends on P0,P1 ──────┤
Phase 3:  Frame Extraction ────────────── depends on P2 ──────────┤
Phase 4:  OCR Pipeline ───────────────── depends on P3 ──────────┤
Phase 5:  FFmpeg Render ───────────────── depends on P2 ──────────┤
Phase 6:  Output Organization ─────────── depends on P4,P5 ──────┤
Phase 7:  GUI — Dashboard & Queue ─────── depends on P1,P2 ──────┤
Phase 8:  GUI — History, Search, Logs ─── depends on P7 ──────────┤
Phase 9:  Data API for Recorder ───────── depends on P1 ──────────┤
Phase 10: Pipeline Orchestrator ───────── depends on P2-P6 ──────┤
Phase 11: Error Recovery ─────────────── depends on P10 ──────────┤
Phase 12: Performance Optimization ────── depends on P10 ──────────┤
Phase 13: Polish & Monitoring ─────────── depends on all ─────────┘
```

**Recommended build order:** 0 → 1 → 2 → 3+5 (parallel) → 4 → 6 → 10 → 7 → 8 → 9 → 11 → 12 → 13

**Critical proof-of-concept items (do first, before any GUI work):**
1. FFmpeg render with TVC insertion + overlay — verify command works and speed is acceptable
2. Frame extraction + Bangla OCR — verify accuracy on real broadcast frames
3. File-based queue round-trip — verify Recorder can write, Render can pick up
