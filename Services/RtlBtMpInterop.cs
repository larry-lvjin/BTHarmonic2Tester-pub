using System.Runtime.InteropServices;

namespace BTHarmonic2Tester.Services;

/// <summary>
/// Low-level P/Invoke bindings for Realtek's <c>RtlBluetoothMP.dll</c> (32-bit).
///
/// The DLL exposes two exported C functions used to build the UART interface and the
/// Bluetooth MP module. The module and interface are plain C structs whose first members
/// are function pointers; we call those pointers back through delegates.
///
/// Layout / values mirror <c>Native/RtlBluetoothMP.h</c> exactly. This binding intentionally
/// implements only the minimum needed to drive BLE (LE) direct-test-mode TX for harmonic
/// measurement: build → open → LE TX start → LE TX end.
/// </summary>
internal static class RtlBtMpInterop
{
    const string Dll = "RtlBluetoothMP.dll";

    // ── Exported functions (see RtlBluetoothMP.h) ────────────────────────────

    // int BTMPAPI_BuildInterfaceRTK(BASE_INTERFACE_MODULE** ppBaseInterface,
    //     BASE_INTERFACE_MODULE* memory, uint InterfaceType,
    //     unsigned char PortNo, unsigned long Baudrate, unsigned char* pData)
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int BTMPAPI_BuildInterfaceRTK(
        out IntPtr ppBaseInterface,
        IntPtr pBaseInterfaceModuleMemory,
        uint interfaceType,
        byte portNo,
        uint baudrate,
        IntPtr pData);

    // int BTMPAPI_BuildBluetoothModule(BASE_BTMPDLL_MODULE* pModule,
    //     BASE_INTERFACE_MODULE* pBaseInterface, void* pExtra,
    //     unsigned char* pTxGainTable, unsigned char* pTxDACTable)
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int BTMPAPI_BuildBluetoothModule(
        IntPtr pBaseBTMPDLLModule,
        IntPtr pBaseInterface,
        IntPtr pExtra,
        IntPtr pTxGainTable,
        IntPtr pTxDACTable);

    // ── Callback delegates for struct-embedded function pointers (cdecl) ──────

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int UpDataParameterFn(IntPtr module, IntPtr param);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int ActionControlExcuteFn(IntPtr module);

    // int (*ActionReport)(BASE_BTMPDLL_MODULE*, int ActiveItem, BT_DEVICE_REPORT*)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int ActionReportFn(IntPtr module, int activeItem, IntPtr report);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int InterfaceFn(IntPtr baseInterface); // Open / Close

    // ── Struct field offsets (x86, 4-byte pointers) ──────────────────────────

    // BASE_BTMPDLL_MODULE: UpDataParameter, ActionControlExcute, ActionReport, ...
    public const int MODULE_OFF_UPDATE_PARAM = 0;
    public const int MODULE_OFF_ACTION_EXEC  = 4;
    public const int MODULE_OFF_ACTION_REPORT = 8;
    // 18 function pointers (72) + pBaseInterface (4) → BuildStatus at 76.
    public const int MODULE_OFF_BUILD_STATUS = 76;

    // BASE_INTERFACE_MODULE: Open, Send, Recv, Close, ... → Open at 0, Close at 12.
    public const int IFACE_OFF_OPEN  = 0;
    public const int IFACE_OFF_CLOSE = 12;

    // Over-allocate the native struct backing stores (real sizes are ~80/~70 bytes).
    public const int MODULE_STRUCT_SIZE = 256;
    public const int IFACE_STRUCT_SIZE  = 512;

    // ── InterfaceType (enum INTERFACE_TYPE) ──────────────────────────────────
    public const uint TYPE_USB  = 0;
    public const uint TYPE_UART = 1;

    // ── ParameterIndex (enum BT_ACTIONCONTROL_TAG) ───────────────────────────
    public const uint HCI_RESET             = 0;
    public const uint TEST_MODE_ENABLE      = 1;
    public const uint LE_TX_DUT_TEST_CMD    = 22;
    public const uint LE_RX_DUT_TEST_CMD    = 23;
    public const uint LE_DUT_TEST_END_CMD   = 24;
    public const uint FW_READ_TX_POWER_INFO = 38;

    // ── Report tag (enum BT_REPORT_TAG) ──────────────────────────────────────
    public const int REPORT_CHIP          = 9;
    public const int REPORT_TX_POWER_INFO = 17;

    // BT_DEVICE_REPORT.ReportData offset (x86, Pack=8). TxPowerLevelInfo is the
    // first 7 bytes of ReportData; index [4] is the default LE TX power index.
    public const int REPORT_OFF_REPORTDATA = 92;
    public const int REPORT_STRUCT_SIZE    = 2048; // >= 92 + 1050

    // ── PHY (enum BT_LE5_TX_PHY) ─────────────────────────────────────────────
    public const byte LE5_TX_1M_PHY = 1;
    public const byte LE5_TX_2M_PHY = 2;

    // ── LE payload type (enum BT_LE_PAYLOAD_TYPE) ────────────────────────────
    public const uint BT_LE_PAYLOAD_TYPE_PRBS9 = 0;

    public const byte STANDARD_MODULATION_INDEX = 0;

    const int MAX_USERAWDATA_SIZE   = 1050;
    const int MAX_TXGAIN_TABLE_SIZE = 7;
    const int MAX_TXDAC_TABLE_SIZE  = 5;

    /// <summary>
    /// Managed mirror of native <c>BT_PARAMETER</c>. Pack=8 matches the VC default so that
    /// the 8-byte-aligned <c>ULONG64 mHitTarget</c> lands at the same offset as native.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct BtParameter
    {
        public uint ParameterIndex;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_USERAWDATA_SIZE)]
        public byte[] mPGRawData;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_USERAWDATA_SIZE)]
        public byte[] mParamData;

        public byte mChannelNumber;
        public uint mPacketType;
        public byte mTxGainIndex;
        public byte mTxGainValue;
        public uint mTxPacketCount;
        public uint mPayloadType;
        public uint mPacketHeader;
        public byte mWhiteningCoeffValue;
        public byte mTxDAC;
        public ulong mHitTarget;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_TXGAIN_TABLE_SIZE)]
        public byte[] TXGainTable;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_TXDAC_TABLE_SIZE)]
        public byte[] TXDACTable;

        public byte bHoppingFixChannel;
        public uint Rtl8761Xtal;
        public byte ExeMode;
        public byte PHY;
        public byte ModulationIndex;
        public byte mTxPower;
        public byte mPowerType;
        public int mMPVersion;

        /// <summary>Create a zero-initialised parameter with the fixed arrays allocated.</summary>
        public static BtParameter Create() => new()
        {
            // 实现细节未公开（算法核心）
            throw new NotImplementedException("该方法的实现不在开源范围内");
        };
    }
}
