using NAudio.CoreAudioApi;
using Serilog;

namespace OnAirCut.Recorder.Helpers;

public class AudioLevelHelper : IDisposable
{
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private bool _disposed;

    public double LeftLevel { get; private set; }
    public double RightLevel { get; private set; }

    public void Initialize(string? deviceId = null)
    {
        try
        {
            _enumerator = new MMDeviceEnumerator();

            if (string.IsNullOrEmpty(deviceId))
            {
                _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            else
            {
                _device = _enumerator.GetDevice(deviceId);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize audio level helper");
        }
    }

    public void UpdateLevels()
    {
        try
        {
            if (_device?.AudioMeterInformation is null)
            {
                LeftLevel = 0;
                RightLevel = 0;
                return;
            }

            var channelCount = _device.AudioMeterInformation.PeakValues.Count;
            if (channelCount >= 2)
            {
                LeftLevel = _device.AudioMeterInformation.PeakValues[0];
                RightLevel = _device.AudioMeterInformation.PeakValues[1];
            }
            else if (channelCount == 1)
            {
                LeftLevel = _device.AudioMeterInformation.MasterPeakValue;
                RightLevel = LeftLevel;
            }
            else
            {
                LeftLevel = _device.AudioMeterInformation.MasterPeakValue;
                RightLevel = LeftLevel;
            }
        }
        catch
        {
            LeftLevel = 0;
            RightLevel = 0;
        }
    }

    public static List<string> GetAudioDevices()
    {
        var devices = new List<string>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                devices.Add(device.FriendlyName);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enumerate audio devices");
        }
        return devices;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _device?.Dispose();
        _enumerator?.Dispose();
    }
}
