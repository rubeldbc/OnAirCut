# OnAirCut — Proof of Concept Commands Reference

## PoC-A: FFmpeg Render Commands

### 1. Probe input file
```bash
ffprobe -v quiet -print_format json -show_format -show_streams input.mp4
```

### 2. Extract 20 frames evenly spaced
```bash
ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 input.mp4
# Get duration, then: interval = duration / 20
ffmpeg -i input.mp4 -vf "fps=20/{duration}" -frames:v 20 -q:v 2 frames/frame_%03d.jpg
```

### 3. Overlay only (no TVC)
```bash
ffmpeg -i input.mp4 -i overlay.png \
  -filter_complex "[0:v][1:v]overlay=0:0:enable='between(t,0,9999)'[v]" \
  -map "[v]" -map "0:a" \
  -c:v libx264 -preset fast -crf 18 \
  -c:a aac -b:a 192k \
  output.mp4
```

### 4. TVC insertion at fixed timestamp (e.g., 18 seconds)
```bash
# Step 1: Split
ffmpeg -i input.mp4 -t 18 -c copy part1.mp4
ffmpeg -i input.mp4 -ss 18 -c copy part2.mp4

# Step 2: Create concat list
echo "file 'part1.mp4'" > list.txt
echo "file 'tvc.mp4'" >> list.txt
echo "file 'part2.mp4'" >> list.txt

# Step 3: Concat + overlay
ffmpeg -f concat -safe 0 -i list.txt -i overlay.png \
  -filter_complex "[0:v][1:v]overlay=0:0:enable='between(t,0,9999)'[v]" \
  -map "[v]" -map "0:a" \
  -c:v libx264 -preset fast -crf 18 \
  -c:a aac -b:a 192k \
  output_with_tvc.mp4
```

### 5. TVC insertion at midpoint
Same as above but replace `18` with `{duration/2}`.

## PoC-B: Bangla OCR

### Tesseract OCR
```bash
# Crop region from frame using ImageSharp, then:
tesseract frame_cropped.jpg output -l ben --psm 7
```

### Key findings:
- Tesseract with `ben.traineddata` (best model) works for clean broadcast text
- Preprocessing helps: grayscale → upscale 2x → binary threshold
- Multi-frame consensus (5 frames) significantly improves accuracy
- PSM 7 (single line) or PSM 6 (block) work best for broadcast headlines

## PoC-C: File-based Queue

### Write job
```csharp
await JobFileHelper.WriteJobAsync(sharedPath, job);
// Creates: {shared}/Jobs/Pending/JOB-20260315-201530-001.json
```

### Read and claim job
```csharp
var files = JobFileHelper.GetPendingJobFiles(sharedPath);
foreach (var file in files)
{
    if (JobFileHelper.TryMoveJob(file, processingPath))
    {
        var job = await JobFileHelper.ReadJobAsync(file);
        // Process job...
    }
}
```

Atomic rename via `File.Move` works on both local and UNC network paths.
