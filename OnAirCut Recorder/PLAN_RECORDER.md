# OnAirCut Recorder Application - Implementation Plan

> Operator-facing WPF application for live/recorded news feed monitoring, manual story clipping, ad set selection, and job submission to the render pipeline.

---

## Phase 0: Project Scaffolding & Shared Infrastructure

### 0.1 Solution Structure Setup
- Create .NET 8 WPF solution `OnAirCut.sln` with the following projects:
  - `OnAirCut.Recorder` — WPF app (startup project for recorder)
  - `OnAirCut.RenderServer` — WPF app (startup project for render server)
  - `OnAirCut.Core` — Shared class library (models, interfaces, utilities, constants)
- Target framework: `net8.0-windows`
- Enable nullable reference types across all projects

### 0.2 NuGet Packages (Recorder)
- `MaterialDesignThemes` — Material Design in XAML Toolkit
- `MaterialDesignColors` — Color palette
- `CommunityToolkit.Mvvm` — MVVM source generators (ObservableObject, RelayCommand, ObservableProperty)
- `LibVLCSharp` + `LibVLCSharp.WPF` — Video playback and capture card preview
- `NAudio` — Audio level metering, headphone output control, device enumeration
- `Microsoft.Data.Sqlite` — SQLite read access (for history/search)
- `Serilog` + `Serilog.Sinks.File` — Structured logging
- `System.Text.Json` — Job file serialization
- `YoutubeDL.Sharp` or `CliWrap` — For yt-dlp integration (YouTube source)

### 0.3 Shared Core Library (OnAirCut.Core)
Define shared models and contracts used by both Recorder and Render Server:

**Models:**
- `JobFile` — Serializable job definition (jobId, rawClipPath, sourceType, sourceName, clipStart, clipEnd, adSetName, overlaySetName, ocrProfileName, submittedBy, submittedAt)
- `AdSetConfig` — Ad set definition (name, tvcPath, overlayPath, insertMode, insertAtSec, overlayStartSec, overlayEndSec, outputWidth, outputHeight)
- `OcrProfile` — OCR crop region definition (profileName, sourceName, cropX, cropY, cropWidth, cropHeight, resizeScale, thresholdMode, isActive)
- `ProcessedStory` — Read model for history display (id, jobId, titleRaw, titleNormalized, safeFolderName, sourceType, onAirDateTime, status, outputPath, etc.)
- `JobStatus` enum — Pending, Processing, Rendering, ExtractingFrames, RunningOcr, Completed, Failed
- `SourceType` enum — LiveFeed, LocalFile, YouTubeUrl

**Constants:**
- `FolderNames` — Standardized subfolder names (Ingest/RawClips, Jobs/Pending, Jobs/Processing, Jobs/Done, Jobs/Failed, Assets/AdSets, Assets/OcrProfiles, Working, Output, Frames, Logs)
- `FileExtensions` — Supported video formats, image formats

**Utilities:**
- `TitleSanitizer` — Convert raw OCR text to filesystem-safe folder/file name (remove invalid chars, collapse whitespace, enforce max length, handle duplicates)
- `JobFileHelper` — Read/write/move job JSON files with atomic operations
- `SharedFolderValidator` — Verify shared folder structure exists and is accessible

**Interfaces:**
- `IVideoSource` — Abstraction over all input types (Connect, Disconnect, StartPreview, StopPreview, StartRecording, StopRecording, GetCurrentPosition, IsHealthy, SourceType)
- `IAdSetProvider` — Load and list available ad sets from shared folder
- `IOcrProfileProvider` — Load and list OCR profiles

### 0.4 Shared Folder Structure Initializer
- Utility in Core that creates the full shared folder tree if it doesn't exist:
```
{SharedRoot}\
  Assets\AdSets\
  Assets\OcrProfiles\
  Ingest\RawClips\{date}\
  Jobs\Pending\
  Jobs\Processing\
  Jobs\Done\
  Jobs\Failed\
  Working\
  Output\{date}\
  Frames\
  Logs\
```
- Run from both Recorder and Render Server on startup (idempotent)

---

## Phase 1: Application Shell & Settings

### 1.1 WPF App Bootstrap with Material Design
- Configure `App.xaml` with Material Design theme (BundledTheme, dark/light toggle)
- Set up primary and secondary color palette
- Create `MainWindow.xaml` with:
  - Title bar with app name "OnAirCut Recorder", clock, shared folder status indicator
  - Navigation: sidebar or tab-based layout
  - Status bar at bottom showing connection state, current recording status, shared folder health

### 1.2 MVVM Infrastructure
- Base classes using CommunityToolkit.Mvvm:
  - ViewModels inherit `ObservableObject`
  - Use `[ObservableProperty]` for bindable properties
  - Use `[RelayCommand]` for commands
- Set up ViewModelLocator or DI container (Microsoft.Extensions.DependencyInjection)
- Register all services and view models in `App.xaml.cs`

### 1.3 Settings System
- `RecorderSettings` model:
  - SharedFolderPath (UNC path like `\\MediaShare`)
  - DefaultSourceType (LiveFeed / LocalFile / YouTubeUrl)
  - DefaultAdSet
  - RecordingFormat (mp4/mkv)
  - RecordingCodec (h264, copy)
  - AudioDevice (headphone output device name)
  - MonitorVolume (0-100)
  - OperatorName
  - FFmpegPath (optional, for local recording if needed)
  - YtDlpPath (for YouTube source)
  - AutoSubmitOnRecordStop (bool)
  - OcrProfilesPath (derived from shared folder)
- Settings stored as JSON in `%AppData%\OnAirCut\recorder_settings.json`
- Settings page in GUI with:
  - Shared folder path browser with validation (check folder exists, check subfolders)
  - Audio device dropdown
  - Source defaults
  - Save / Reset buttons

### 1.4 Shared Folder Health Monitor
- Background service that periodically checks (every 5 seconds):
  - Shared folder is accessible
  - Required subfolders exist
  - Disk space available
- Exposes `IsHealthy`, `LastCheckTime`, `ErrorMessage` as observable properties
- Status indicator in title bar (green dot = healthy, red = error, yellow = warning)

---

## Phase 2: Video Source Abstraction & Local File Playback

### 2.1 IVideoSource Interface Implementation
```csharp
public interface IVideoSource : IDisposable
{
    SourceType SourceType { get; }
    string SourceName { get; }
    bool IsConnected { get; }
    bool IsRecording { get; }
    bool IsHealthy { get; }
    TimeSpan CurrentPosition { get; }
    TimeSpan? Duration { get; } // null for live

    Task<bool> ConnectAsync(string sourceUri);
    Task DisconnectAsync();
    void StartPreview(IntPtr hwnd); // or pass WPF control
    void StopPreview();
    Task<string> StartRecordingAsync(string outputPath);
    Task<RecordingResult> StopRecordingAsync();

    event EventHandler<SourceHealthChangedEventArgs> HealthChanged;
    event EventHandler<SourcePositionChangedEventArgs> PositionChanged;
}
```

### 2.2 Local File Source
- Implement `LocalFileSource : IVideoSource`
- Use LibVLCSharp for playback:
  - Open local file
  - Play/pause/seek
  - Preview in WPF VideoView control
  - Audio output to selected device
- Recording means: extract segment between start/stop timestamps
  - Use FFmpeg CLI (via `Process.Start` or `CliWrap`) to extract segment:
    `ffmpeg -i input.mp4 -ss {start} -to {end} -c copy output.mp4`
  - Or use LibVLC's recording capability
- Return output file path on `StopRecordingAsync`

### 2.3 Preview Player UI
- Center panel of main screen:
  - `VideoView` control from LibVLCSharp.WPF
  - Current timecode display (HH:MM:SS.fff)
  - Play / Pause / Seek bar (for local file mode)
  - Source name and type label
  - Recording indicator (red dot + duration when recording)
- Transport controls below player:
  - Play/Pause button (local file and YouTube modes)
  - Seek slider (local file mode only)
  - Speed control: 0.5x, 1x, 1.5x, 2x (local file mode only)

### 2.4 Audio Monitoring
- NAudio integration:
  - Enumerate audio output devices
  - Route preview audio to selected headphone device
  - Volume slider (0-100%)
  - Mute/unmute toggle button
  - Real-time audio level meter (VU meter style, left + right channels)
  - Peak indicator
- Audio meter widget: vertical bar with green/yellow/red zones

---

## Phase 3: Recording Controls & Clip Management

### 3.1 Recording State Machine
Define clear states:
```
Idle -> ReadyToRecord (source connected)
ReadyToRecord -> Recording (Record Start pressed)
Recording -> ClipComplete (Record Stop pressed)
ClipComplete -> Idle (after job submission or cancel)
```

### 3.2 Record Start/Stop Controls
- Prominent "Record" button:
  - Idle state: grayed out (no source)
  - Ready state: red circle icon, enabled
  - Recording state: pulsing red square (stop icon), shows elapsed time
- Keyboard shortcut: `F5` to start, `F6` to stop (configurable)
- On Record Start:
  - Generate clip filename: `clip_{yyyyMMdd}_{HHmmss}.mp4`
  - Set output path: `{SharedFolder}\Ingest\RawClips\{date}\{filename}`
  - Call `source.StartRecordingAsync(outputPath)`
  - Start elapsed time counter
  - Log event
- On Record Stop:
  - Call `source.StopRecordingAsync()`
  - Receive `RecordingResult` (filePath, duration, startTime, endTime)
  - Transition to ClipComplete state
  - Enable ad set selection and job submission

### 3.3 Cancel Clip
- "Cancel" button available during or after recording
- Asks confirmation dialog
- Deletes the partial/complete clip file
- Returns to ReadyToRecord state

### 3.4 Current Clip Info Panel
- Small panel showing during/after recording:
  - Clip filename
  - Duration
  - Start timestamp
  - File size (live updating during recording)
  - Status (Recording / Complete / Submitting)

---

## Phase 4: Ad Set Selection & Job Submission

### 4.1 Ad Set Loader
- `AdSetProvider` service:
  - Scans `{SharedFolder}\Assets\AdSets\` for subfolders
  - Each subfolder is an ad set (e.g., `Set_A`, `Set_B`)
  - Reads `config.json` from each subfolder to get `AdSetConfig`
  - Watches folder for changes (FileSystemWatcher) to refresh list
  - Returns list of available ad sets with preview info

### 4.2 Ad Set Selection UI (Right Panel)
- List of available ad sets as radio button cards:
  - Ad set name
  - TVC filename + duration
  - Overlay filename (thumbnail preview if image)
  - Insert mode description (e.g., "Insert at 18s" or "Insert at midpoint")
- Radio button selection — one ad set per clip
- "No Ad" option for clips without ad insertion
- Selected ad set highlighted with accent color
- Quick preview: click to preview TVC video or overlay image in a popup

### 4.3 Job Submission
- "Submit Job" button (enabled only when clip is complete + ad set selected or "No Ad" chosen)
- On submit:
  1. Build `JobFile` object with all metadata
  2. Serialize to JSON
  3. Write to `{SharedFolder}\Jobs\Pending\{jobId}.json`
     - Use atomic write: write to `.tmp` file first, then rename
  4. Update UI status to "Submitted"
  5. Add to local history list
  6. Log event
  7. Return to Idle/ReadyToRecord state
- Auto-submit option: if enabled in settings, automatically submit on record stop with last-used ad set

### 4.4 Job ID Generation
- Format: `JOB-{yyyyMMdd}-{HHmmss}-{3-digit-sequence}`
- Example: `JOB-20260315-201530-001`
- Sequence resets daily, increments per submission

---

## Phase 5: History & Search Panel

### 5.1 Data Access for History
- Two data source strategies (configurable in settings):

  **Option A — Direct SQLite read (simpler, default for v1):**
  - Open SQLite DB from render PC via shared path (read-only)
  - Path configured in settings: `{SharedFolder}\Database\onaircut.db` or render PC local path mapped
  - Use `Microsoft.Data.Sqlite` with `Mode=ReadOnly`

  **Option B — HTTP API (future, more robust):**
  - Render server exposes minimal REST API
  - Recorder queries: `/api/stories/search`, `/api/jobs/recent`, `/api/jobs/{id}/status`

### 5.2 History Panel UI (Bottom Section)
- DataGrid/ListView showing processed stories:
  - Columns: Title, On-Air Date/Time, Duration, Source, Ad Set, Status, Actions
  - Status with color coding:
    - Blue: Pending
    - Yellow: Processing/Rendering
    - Green: Completed
    - Red: Failed
  - Row double-click: open output folder in Explorer
  - Context menu: Open folder, Copy title, View details

### 5.3 Search Functionality
- Search bar above history grid with:
  - Text input (searches title, folder name, file name)
  - Date range picker (from/to)
  - Source/channel filter dropdown
  - Status filter dropdown
  - Search button + Enter key trigger
- Results update the history grid
- "Today" quick filter button
- "Clear filters" button

### 5.4 Duplicate Detection
- When operator starts recording, optionally check:
  - Is there a story with similar title processed recently?
  - Show warning badge if potential duplicate found
- This requires OCR result feedback — only works after first clip of same story is processed
- Simple substring match on recent titles (last 24 hours)

---

## Phase 6: OCR Region Configuration

### 6.1 OCR Profile Editor Screen
- Accessed from Settings or dedicated nav item
- Flow:
  1. Select source or load a sample frame
  2. Display frame as full image
  3. Draw rectangle overlay on the image (drag to create, handles to resize)
  4. Rectangle defines the OCR crop region (x, y, width, height relative to frame size)
  5. Preview cropped region in a side panel
  6. Save as OCR profile with name and associated source/channel

### 6.2 Frame Capture for Configuration
- "Capture Frame" button: grabs current preview frame as a still image
- Or "Load Sample Image" button: load a saved screenshot
- Frame displayed at full resolution with zoom/pan capability

### 6.3 Rectangle Drawing Tool
- Mouse-based rectangle drawing on the image:
  - Click and drag to create rectangle
  - Corner/edge handles to resize
  - Drag rectangle body to reposition
  - Display pixel coordinates and dimensions in real-time
  - Snap to pixel boundaries
- Rectangle rendered as semi-transparent colored overlay with border
- Coordinates shown: X, Y, Width, Height (in pixels, relative to source resolution)

### 6.4 OCR Profile Management
- `OcrProfile` includes:
  - ProfileName (e.g., "Channel24_Headline")
  - SourceName (e.g., "Channel 24")
  - CropX, CropY, CropWidth, CropHeight (pixels)
  - ResizeScale (upscale factor for better OCR, default 2.0)
  - ThresholdMode (none, binary, adaptive — for preprocessing)
  - IsActive (bool)
- Profiles saved as JSON files in `{SharedFolder}\Assets\OcrProfiles\{profileName}.json`
- List view of all profiles with edit/delete/duplicate/test buttons
- "Test OCR" button: crops the region from current frame, runs local OCR test, shows result

### 6.5 Multi-Region Support
- Allow multiple crop regions per profile (for channels with headline + subheadline):
  - Primary region (used for filename)
  - Secondary region (optional, stored as additional metadata)
- Each region independently configurable

---

## Phase 7: Live Feed Source (Capture Card)

### 7.1 Capture Card Integration
- Implement `LiveFeedSource : IVideoSource`
- Use LibVLCSharp's DirectShow/DeckLink input:
  - Enumerate available capture devices
  - Connect to selected device
  - Preview live feed
  - Record to file
- Alternative: FFmpeg-based capture via `Process.Start`:
  - `ffmpeg -f dshow -i video="DeckLink":audio="DeckLink" -c:v libx264 -preset ultrafast output.mp4`
  - Or copy codec if hardware supports it

### 7.2 Capture Device Settings
- Device selection dropdown (refresh button to re-enumerate)
- Input format: resolution, frame rate, pixel format
- Audio input: embedded audio or separate audio device
- Signal status indicator:
  - Connected / No Signal / Signal Lost
  - Current detected resolution and frame rate
- Auto-reconnect on signal loss with configurable timeout

### 7.3 Recording from Live Feed
- On Record Start:
  - Begin writing to output file immediately
  - Use segment recording (FFmpeg `-f segment` or LibVLC record)
  - Track wall-clock start time as on-air timestamp
- On Record Stop:
  - Finalize file (close moov atom, flush buffers)
  - **Critical**: Ensure file is fully written before job submission
  - Wait for file handle release
  - Verify file integrity (duration > 0, file size > threshold)

### 7.4 Signal Health Monitoring
- Continuous monitoring:
  - Frame arrival rate
  - Audio level presence
  - Resolution consistency
- If signal drops during recording:
  - Visual warning (flashing border)
  - Audio alert (optional)
  - Continue recording (don't auto-stop)
  - Log gap duration

---

## Phase 8: YouTube Live Source

### 8.1 YouTube URL Input
- Implement `YouTubeSource : IVideoSource`
- UI: text input field for YouTube URL + "Connect" button
- Use `yt-dlp` to resolve stream URL:
  - `yt-dlp -g --no-playlist {url}` to get direct stream URL
  - Feed resolved URL to LibVLCSharp for preview
- Handle URL types:
  - Live stream: `youtube.com/watch?v=...` (live)
  - Channel live: `youtube.com/@channel/live`

### 8.2 Stream Handling
- Preview: LibVLCSharp plays the resolved HLS/DASH URL
- Recording:
  - Option A: `yt-dlp` direct download segment
  - Option B: FFmpeg reads stream URL, writes to file between start/stop
  - Option C: LibVLC recording
- Preferred: FFmpeg with stream URL for consistency with other sources

### 8.3 YouTube-Specific Challenges
- Stream URL expires — re-resolve periodically (every 30 minutes)
- Network buffering — show buffer indicator
- Latency — display "live delay" indicator
- Connection drops — auto-reconnect with backoff
- Error display — show clear message if URL is invalid, stream ended, or geo-blocked

---

## Phase 9: Main Window Layout Assembly

### 9.1 Final Layout Structure
```
+------------------------------------------------------------------+
|  [OnAirCut Recorder]  [Clock]  [Shared:OK]  [Source:Connected]   |
+------------------------------------------------------------------+
|  Source    |                            |  Ad Sets               |
|  Panel     |      Video Preview         |  +-----------+         |
|            |                            |  | ( ) Set_A |         |
|  [Live]    |      [Timecode: 00:15:32]  |  | ( ) Set_B |         |
|  [File]    |                            |  | ( ) Set_C |         |
|  [YouTube] |      [Audio Meter ||||| ]  |  | (x) No Ad |         |
|            |                            |  +-----------+         |
|  [Connect] |      [Volume: ====o=== ]   |                        |
|            |                            |  Overlay Preview       |
|            +----------------------------+  [thumbnail]           |
|            |  [<< |> >>]  Seek bar      |                        |
+------------+----------------------------+------------------------+
|  [ * RECORD ]  [ STOP ]  [Cancel]  Duration: 00:01:45  [Submit] |
+------------------------------------------------------------------+
|  Search: [___________] [Date: from-to] [Source: v] [Status: v]   |
|  +--------------------------------------------------------------+|
|  | Title              | Date       | Dur  | Source | Status     ||
|  | Economy report...  | 2026-03-15 | 1:30 | Live   | Completed  ||
|  | Sports update...   | 2026-03-15 | 2:10 | Live   | Rendering  ||
|  +--------------------------------------------------------------+|
+------------------------------------------------------------------+
|  [Status bar: Ready | Shared folder OK | 15 clips today]         |
+------------------------------------------------------------------+
```

### 9.2 Responsive Layout
- Use `Grid` with proportional sizing
- Left panel: fixed width (~200px)
- Center: star (fills remaining)
- Right panel: fixed width (~250px)
- Bottom history: fixed height (~250px) with splitter to resize
- All panels scrollable where needed

### 9.3 Keyboard Shortcuts
- `F5` — Start Recording
- `F6` — Stop Recording
- `Escape` — Cancel current clip
- `F8` — Submit job
- `Ctrl+F` — Focus search box
- `Space` — Play/Pause (local file mode)
- `Ctrl+S` — Open settings

---

## Phase 10: Polish, Error Handling & Edge Cases

### 10.1 Error Handling Strategy
- All async operations wrapped in try-catch with user-friendly error display
- Non-blocking toast notifications for transient errors (Material Design Snackbar)
- Modal dialogs for critical errors (source disconnected during recording, shared folder lost)
- All errors logged via Serilog to `%AppData%\OnAirCut\Logs\recorder_{date}.log`

### 10.2 Edge Cases to Handle
- Record stop when source disconnects mid-recording:
  - Save whatever was captured
  - Mark clip as "partial" in job metadata
  - Warn operator before submission
- Shared folder becomes unavailable during job submit:
  - Queue locally and retry when folder returns
  - Show pending local queue count
- Duplicate job submission prevention:
  - Disable submit button after first submit
  - Check if job file already exists before writing
- Application crash recovery:
  - On startup, check for orphaned recording temp files
  - Offer to recover or delete
- Very short clips (< 3 seconds):
  - Warn operator, require confirmation to submit
- Very long clips (> 10 minutes):
  - Warn operator (unusual for a single news story)

### 10.3 Logging
- Log levels: Debug, Info, Warning, Error
- Log events:
  - Source connect/disconnect
  - Record start/stop with timestamps
  - Job submission with jobId
  - Shared folder health changes
  - Settings changes
  - Errors with stack traces

### 10.4 Configuration Validation
- On startup:
  - Validate shared folder path exists and is writable
  - Validate FFmpeg/yt-dlp paths if configured
  - Check audio device availability
  - Show setup wizard if first run or critical config missing

### 10.5 Application Lifecycle
- Graceful shutdown:
  - If recording in progress, warn and offer to stop + save
  - Flush all logs
  - Release all media resources
  - Close file handles
- Single instance enforcement (Mutex) — prevent running two recorders accidentally
- Minimize to system tray option

---

## Phase Summary & Dependencies

```
Phase 0: Scaffolding ──────────────────────────────────────┐
Phase 1: App Shell & Settings ─────────────────────────────┤
Phase 2: Video Source & Local File ────────────────────────┤
Phase 3: Recording Controls ───────────── depends on P2 ──┤
Phase 4: Ad Set & Job Submission ──────── depends on P3 ──┤
Phase 5: History & Search ─────────────── depends on P1 ──┤
Phase 6: OCR Region Config ───────────── depends on P2 ──┤
Phase 7: Live Feed Source ─────────────── depends on P2 ──┤
Phase 8: YouTube Source ───────────────── depends on P2 ──┤
Phase 9: Layout Assembly ─────────────── depends on all ──┤
Phase 10: Polish & Error Handling ─────── depends on all ─┘
```

**Recommended build order:** 0 → 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10

Phases 5, 6, 7, 8 can be developed in parallel after Phase 4 is complete.
