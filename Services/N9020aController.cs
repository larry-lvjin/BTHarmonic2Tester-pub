using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace BTHarmonic2Tester.Services;

/// <summary>
/// Controls a Keysight N9020A (MXA) signal analyzer over raw TCP SCPI (port 5025).
/// Measures the fundamental and 2nd harmonic with Swept SA + MaxHold + peak marker
/// (robust against the bursty LE PKT TX packet gaps).
/// </summary>
public sealed class N9020aController : IDisposable
{
    TcpClient? _client;
    NetworkStream? _stream;
    StreamReader? _reader;
    StreamWriter? _writer;
    readonly object _lock = new();
    bool _ioLog = true;   // when false, suppress per-command SCPI logging (e.g. the sweep loop)

    public bool IsConnected => _client?.Connected == true;

    public event Action<string>? Log;

    public void Connect(string visaAddress)
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    static string ParseIpFromVisa(string visa)
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    void Send(string cmd)
    {
        lock (_lock) { _writer!.WriteLine(cmd); }
        if (_ioLog) Log?.Invoke($">> {cmd}");
    }

    string Query(string cmd)
    {
        string resp;
        lock (_lock)
        {
            _writer!.WriteLine(cmd);
            resp = _reader!.ReadLine() ?? string.Empty;
        }
        if (_ioLog) { Log?.Invoke($">> {cmd}"); Log?.Invoke($"<< {resp.Trim()}"); }
        return resp;
    }

    public string GetIdentity() => Query("*IDN?").Trim();

    /// <summary>Poll SYSTem:ERRor? until the queue is empty, logging any errors.</summary>
    void DrainErrors()
    {
        for (int i = 0; i < 20; i++)
        {
            var err = Query("SYST:ERR?");
            if (err.Contains("+0,") || err.Contains("No error")) return;
            Log?.Invoke($"[N9020A] Error: {err.Trim()}");
        }
    }

    // ── Fixed measurement settings (Swept SA + MaxHold + peak marker) ────────
    // These are hardcoded per the agreed method (only line-loss & limit are user-editable).
    const double RbwHz      = 1_000_000;  // 1 MHz reference bandwidth (>1 GHz spurious)
    const double AttenDb    = 0;          // analyzer internal attenuation (external 10 dB pad handles headroom)
    const double RefLevelDbm = 10;        // display reference level
    const double SpanHz     = 20_000_000; // 20 MHz span around each center
    const int    SweepPoints = 401;       // fewer points → faster sweeps
    const int    MaxHoldSweeps = 12;      // sweeps accumulated per point (catches bursty LE PKT TX)

    bool _sweptConfigured;

    /// <summary>One-time Swept-SA setup: SA mode, fixed RBW, 0 dB atten, peak detector.</summary>
    public void ConfigureSweptSa()
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    /// <summary>
    /// Measure fundamental and 2nd harmonic (raw dBm at the analyzer input) using
    /// Swept SA + MaxHold + peak marker at the fundamental and at 2×fundamental.
    /// MaxHold reliably captures the bursty LE PKT TX between packet gaps.
    /// </summary>
    public (double fundRawDbm, double h2RawDbm, double fundFreqHz, double h2FreqHz)
        MeasureHarmonicSwept(long fundamentalHz, CancellationToken ct)
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    /// <summary>Center on a frequency, accumulate MaxHold over N sweeps, return peak (dBm, Hz).</summary>
    (double dbm, double freqHz) MaxHoldPeak(long centerHz, CancellationToken ct)
    {
        Log?.Invoke($"[N9020A] MaxHold {MaxHoldSweeps}x @ {centerHz / 1e6:F1} MHz ...");
        bool prev = _ioLog; _ioLog = false;   // suppress per-sweep spam
        try
        {
            Send($"SENS:FREQ:CENT {centerHz}");
            Send("TRAC1:TYPE WRIT");                 // reset trace
            Send("INIT:IMM"); Query("*OPC?");
            Send("TRAC1:TYPE MAXH");                 // accumulate max-hold
            for (int i = 0; i < MaxHoldSweeps; i++)
            {
                ct.ThrowIfCancellationRequested();
                Send("INIT:IMM"); Query("*OPC?");
            }
            Send("CALC:MARK1:MODE POS");
            Send("CALC:MARK1:MAX");                   // peak search
            double y = ParseDouble(Query("CALC:MARK1:Y?"));
            double x = ParseDouble(Query("CALC:MARK1:X?"));
            return (y, x);
        }
        finally { _ioLog = prev; }
    }

    static double ParseDouble(string s)
        => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : double.NaN;

    public void Disconnect()
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    public void Dispose() => Disconnect();
}
