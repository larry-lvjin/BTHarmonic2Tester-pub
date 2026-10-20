using System.Runtime.InteropServices;
using Microsoft.Win32;
using BTHarmonic2Tester.Models;
using static BTHarmonic2Tester.Services.RtlBtMpInterop;

namespace BTHarmonic2Tester.Services;

/// <summary>
/// High-level driver for the RTL8773D DUT over UART, wrapping <see cref="RtlBtMpInterop"/>.
///
/// Lifecycle: <see cref="Connect"/> builds the UART interface + BT MP module and opens the
/// device; <see cref="StartLeTx"/>/<see cref="StopLeTx"/> put the DUT into BLE direct-test-mode
/// TX on a channel/PHY so the analyzer can read the emission and its 2nd harmonic.
///
/// NOTE: absolute TX power depends on <see cref="TxGainIndex"/>, which is chip/calibration
/// specific — validate against the DUT's efuse/gain table on real hardware.
/// </summary>
public sealed class BtDutController : IDisposable
{
    readonly object _lock = new();

    IntPtr _ifaceMem = IntPtr.Zero;   // BASE_INTERFACE_MODULE backing store
    IntPtr _moduleMem = IntPtr.Zero;  // BASE_BTMPDLL_MODULE backing store
    IntPtr _pData = IntPtr.Zero;      // scratch buffer for BuildInterfaceRTK
    IntPtr _pIface = IntPtr.Zero;     // interface pointer returned by the DLL

    // Cached delegates (kept alive to prevent GC of the thunks)
    UpDataParameterFn? _upDataParameter;
    ActionControlExcuteFn? _actionExec;
    ActionReportFn? _actionReport;
    InterfaceFn? _open;
    InterfaceFn? _close;

    public bool IsConnected { get; private set; }

    /// <summary>LE TX gain index passed to the DUT (chip-specific power table index).
    /// Default 0x35 matches the RTL8773D reported LE 1M/2M default power index.</summary>
    public byte TxGainIndex { get; set; } = 0x35;

    /// <summary>Default LE TX power index read from the device (null if not read).</summary>
    public byte? DeviceDefaultLeGainIndex { get; private set; }

    public event Action<string>? Log;

    public static string[] GetAvailablePorts()
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    /// <summary>Build the UART interface + BT module and open the DUT.</summary>
    public void Connect(string portName, int baudRate = 115200)
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    /// <summary>
    /// Read chip info (ActionReport REPORT_CHIP). This is the first transaction RTLBTAPP performs and
    /// it establishes the UART link / MP command version before any control command. Best-effort.
    /// </summary>
    void TryGetChipInfo()
    {
        if (_actionReport == null) return;
        IntPtr rep = Marshal.AllocHGlobal(REPORT_STRUCT_SIZE);
        try
        {
            Zero(rep, REPORT_STRUCT_SIZE);
            int r = _actionReport(_moduleMem, REPORT_CHIP, rep);
            if (r != 0) { Log?.Invoke($"[DUT] GetChipInfo returned {r}"); return; }

            // pBTInfo pointer at offset 60 → BT_CHIPINFO; ChipType is the 5th uint (+16).
            IntPtr pInfo = Marshal.ReadIntPtr(rep, 60);
            uint chipType = pInfo != IntPtr.Zero ? (uint)Marshal.ReadInt32(pInfo, 16) : 0u;
            Log?.Invoke($"[DUT] Chip info OK (ChipType={chipType})");
        }
        catch (Exception ex) { Log?.Invoke($"[DUT] GetChipInfo skipped: {ex.Message}"); }
        finally { Marshal.FreeHGlobal(rep); }
    }

    /// <summary>
    /// Read the device's default TX power indices (FW_READ_TX_POWER_INFO → REPORT_TX_POWER_INFO)
    /// and adopt the default LE index. Best-effort: logs and continues on failure.
    /// </summary>
    void TryReadTxPowerInfo()
    {
        if (_actionReport == null) return;
        try
        {
            Exec("Read TX power info", FW_READ_TX_POWER_INFO, null);

            IntPtr report = Marshal.AllocHGlobal(REPORT_STRUCT_SIZE);
            try
            {
                Zero(report, REPORT_STRUCT_SIZE);
                int r = _actionReport(_moduleMem, REPORT_TX_POWER_INFO, report);
                if (r != 0)
                {
                    Log?.Invoke($"[DUT] ActionReport(TX power) returned {r}; keeping gain index 0x{TxGainIndex:X2}");
                    return;
                }

                byte maxIdx = Marshal.ReadByte(report, REPORT_OFF_REPORTDATA + 0);
                byte leIdx  = Marshal.ReadByte(report, REPORT_OFF_REPORTDATA + 4); // DEFAULT_LE_TX_POWER_INDEX
                DeviceDefaultLeGainIndex = leIdx;
                TxGainIndex = leIdx;
                Log?.Invoke($"[DUT] TX power info: maxIdx={maxIdx}, LE default idx={leIdx} (0x{leIdx:X2}) — using it");
            }
            finally { Marshal.FreeHGlobal(report); }
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[DUT] Read TX power info skipped: {ex.Message}");
        }
    }

    /// <summary>Start BLE direct-test-mode TX (PRBS9) on the given channel and PHY.</summary>
    public void StartLeTx(int channel, BlePhy phy)
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    /// <summary>Stop the LE direct-test-mode TX.</summary>
    public void StopLeTx()
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    // ── internals ────────────────────────────────────────────────────────────

    void Exec(string what, uint paramIndex, Func<BtParameter, BtParameter>? setup)
    {
        lock (_lock)
        {
            var p = BtParameter.Create();
            p.ParameterIndex = paramIndex;
            if (setup != null) p = setup(p);
            p.ParameterIndex = paramIndex; // ensure preserved after setup

            IntPtr pp = Marshal.AllocHGlobal(Marshal.SizeOf<BtParameter>());
            try
            {
                Marshal.StructureToPtr(p, pp, false);
                int u = _upDataParameter!(_moduleMem, pp);
                if (u != 0)
                    throw new InvalidOperationException($"{what}: UpDataParameter failed (code {u})");
                int e = _actionExec!(_moduleMem);
                if (e != 0)
                    throw new InvalidOperationException($"{what}: ActionControlExcute failed (code {e})");
                Log?.Invoke($"[DUT] {what} OK");
            }
            finally
            {
                Marshal.DestroyStructure<BtParameter>(pp);
                Marshal.FreeHGlobal(pp);
            }
        }
    }

    /// <summary>Best-effort variant that logs failures instead of throwing.</summary>
    void TryExec(string what, uint paramIndex, Func<BtParameter, BtParameter>? setup)
    {
        try { Exec(what, paramIndex, setup); }
        catch (Exception ex) { Log?.Invoke($"[DUT] {what} warning: {ex.Message}"); }
    }

    static T GetFn<T>(IntPtr structBase, int offset) where T : Delegate
    {
        IntPtr fn = Marshal.ReadIntPtr(structBase, offset);
        if (fn == IntPtr.Zero)
            throw new InvalidOperationException($"Null function pointer at offset {offset}");
        return Marshal.GetDelegateForFunctionPointer<T>(fn);
    }

    static void Zero(IntPtr ptr, int size)
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    static byte ParsePortNumber(string portName)
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    void EnsureConnected()
    {
        if (!IsConnected) throw new InvalidOperationException("DUT not connected");
    }

    public void Disconnect()
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    public void Dispose() => Disconnect();
}
