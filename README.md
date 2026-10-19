# BTHarmonic2Tester

BLE（蓝牙低功耗）二次谐波杂散测试上位机。一键完成 Realtek DUT 发射控制 + Keysight 频谱分析仪测量 + 判定，自动跑完 PHY × 信道测试矩阵并给出 PASS/FAIL 结果表。

- **平台**：Windows 桌面（WPF，.NET 10）
- **架构**：MVVM
- **被测件（DUT）**：Realtek RTL8773D（经 UART / 串口）
- **测量仪器**：Keysight N9020A（MXA）信号 / 频谱分析仪（经 LAN / SCPI）

---

## 目录

- [功能特性](#功能特性)
- [测试原理](#测试原理)
- [运行环境](#运行环境)
- [硬件连接](#硬件连接)
- [快速开始](#快速开始)
- [使用说明](#使用说明)
- [参数说明](#参数说明)
- [测量方法细节](#测量方法细节)
- [项目结构](#项目结构)
- [编译与打包](#编译与打包)
- [持续集成 / 发布](#持续集成--发布)
- [常见问题](#常见问题)
- [已知限制](#已知限制)

---

## 功能特性

- **自动化测试矩阵**：对 `LE 1M / LE 2M` 两种 PHY，在信道 `0 / 19 / 39`（2402 / 2440 / 2480 MHz）共 6 个测试点逐点测量。
- **DUT 发射控制**：通过 P/Invoke 调用 Realtek `RtlBluetoothMP.dll`，将 DUT 置于 BLE 直接测试模式（Direct Test Mode）连续发射 PRBS9 载荷。
- **谐波测量**：驱动 N9020A 用 Swept SA + MaxHold + 峰值 Marker 分别读取基波与二次谐波功率，对突发式 LE 分组发射稳定捕获。
- **线损补偿与判定**：对读数统一补偿外部链路损耗（衰减器 + 线缆），与限值比较后判定 PASS / FAIL。
- **实时结果表**：以 PHY（行）× 信道（列）的表格展示 `H2 绝对功率 (dBm)` 与 `相对基波 (dBc)`，颜色区分测试中 / 通过 / 失败。
- **深色主题界面 + 运行日志**：内置带时间戳的日志窗口，完整记录 SCPI 收发与 DUT 交互过程，可一键清空。
- **可随时中止**：测试过程中可 Stop，自动停止 DUT 发射并回收资源。

---

## 测试原理

BLE 工作在 2.4 GHz ISM 频段，其**二次谐波**落在约 4.8 GHz，是射频认证与产线必测的杂散指标。

- BLE 测试信道 `N` 的基波频率：`Fund = 2402 + 2 × N` (MHz)
- 二次谐波频率：`H2 = 2 × Fund`

默认测试点：

| PHY | 信道 | 基波 (MHz) | 二次谐波 (MHz) |
|-----|------|-----------|---------------|
| 1M / 2M | 0  | 2402 | 4804 |
| 1M / 2M | 19 | 2440 | 4880 |
| 1M / 2M | 39 | 2480 | 4960 |

单点测试流程：

1. DUT 在指定信道 / PHY 上启动 LE 连续发射（PRBS9）。
2. 频谱仪在基波中心与二次谐波中心分别做 MaxHold 扫描，取峰值。
3. 对两处读数补偿线损，得到 DUT 端真实功率。
4. `H2(补偿后) < H2 限值` → PASS，否则 FAIL。同时记录 `H2 - Fund` 的 dBc 值供参考。
5. 停止 DUT 发射，进入下一个测试点。

---

## 运行环境

**运行（使用打包好的 EXE）**

- Windows 10 / 11（x86 或 x64 均可运行 32 位进程）
- `RtlBluetoothMP.dll`（32 位）需与主程序位于同一目录（打包产物已包含）
- DUT 对应的 USB-转-串口（COM）驱动
- 与 N9020A 同网段的以太网连通

> **重要**：`RtlBluetoothMP.dll` 是 **32 位** 原生库，因此主程序被强制编译为 **x86**（见 `csproj` 的 `PlatformTarget`）。请勿改为 x64，否则无法加载该 DLL。

**开发 / 编译**

- [.NET SDK 10.0](https://dotnet.microsoft.com/)（含 Windows Desktop 组件）
- Visual Studio 2022/2026 或 `dotnet` CLI
- 仅 Windows 可编译（WPF）

---

## 硬件连接

```
┌────────────┐   USB / UART (COM)   ┌──────────────┐
│  上位机 PC  │ ───────────────────▶ │ DUT RTL8773D │
│  (本软件)   │                      └──────┬───────┘
│            │                             │ RF 发射
│            │   LAN / SCPI (TCP 5025)     ▼
│            │ ───────────────────▶ ┌──────────────┐
└────────────┘                      │  N9020A MXA  │
                                    │  频谱分析仪   │
                                    └──────────────┘
     DUT RF 输出 ──[10 dB 衰减器]──[线缆]──▶ N9020A RF IN
```

- DUT 射频输出经外部 **10 dB 衰减器 + 线缆** 接入 N9020A 的 RF 输入（默认线损补偿 **11.5 dB**）。
- 衰减器用于给频谱仪提供余量，故仪器内部衰减设为 0 dB。

---

## 快速开始

### 方式一：直接运行发布版

1. 下载 Release 中的单文件 EXE（自包含，无需预装 .NET 运行时）。
2. 确保 `RtlBluetoothMP.dll` 与 EXE 同目录（打包已内置）。
3. 双击运行。

### 方式二：源码编译运行

```bash
# 还原依赖
dotnet restore

# 运行（Debug）
dotnet run

# 或用 Release 发布单文件（win-x86，自包含）
dotnet publish -c Release
```

> 仓库内 `Properties/PublishProfiles/FolderProfile.pubxml` 定义了 `win-x86` 自包含单文件发布配置，可直接在 Visual Studio 中「发布」使用。

---

## 使用说明

界面自上而下分为 5 个区域：

1. **N9020A Connection** — 填写频谱仪 IP（默认 `192.0.2.10`），点击 **Connect**；连接成功后指示灯变绿并显示 `*IDN?` 返回的仪器标识。
2. **DUT (RTL8773D) Serial** — 选择 COM 口（`↻` 可刷新列表）、波特率（115200 / 921600 / 1500000 / 3000000）与 TX Gain 索引，点击 **Connect**。连接时会自动读取设备默认 LE 功率索引并回填。
3. **Harmonic Test Configuration** — 设置 **Line Loss（线损，dB）** 与 **H2 Limit（限值，dBm）**，点击 **▶ Start** 开始，**⏹ Stop** 中止。中间显示当前测试进度。
4. **Second-Harmonic Result** — 结果表，行为 PHY、列为信道；每格上方为 H2 绝对功率、下方为 dBc，并以黄 / 绿 / 红标识测试中 / PASS / FAIL。
5. **Log** — 带时间戳的运行日志，可 **Clear** 清空。

**典型步骤**：连接频谱仪 → 连接 DUT → 确认线损与限值 → Start → 等待矩阵跑完并查看结果表。

---

## 参数说明

| 参数 | 界面项 | 默认值 | 说明 |
|------|--------|--------|------|
| 频谱仪 IP | IP Address | `192.0.2.10` | 支持纯 IP 或 VISA 形式（自动解析 `TCPIP0::x.x.x.x::...`） |
| 串口 | Port | 自动枚举 | 从注册表 `SERIALCOMM` 枚举 COM 口 |
| 波特率 | Baud | `115200` | DUT UART 波特率 |
| TX 增益索引 | TX Gain Idx | `0x35`（连接后回填设备默认值） | 芯片相关的发射功率表索引 |
| 线损 | Line Loss (dB) | `11.5` | 外部链路补偿 = 10 dB 衰减器 + 1.5 dB 线缆 |
| H2 限值 | H2 Limit (dBm) | `-36.0` | 补偿后二次谐波绝对功率上限 |

> 仅 **线损** 与 **限值**（以及连接类参数）对用户开放；测量方法参数（RBW、扫宽、检波、MaxHold 次数等）按既定方法固化在代码中。

---

## 测量方法细节

频谱仪采用固定的 **Swept SA + MaxHold + 峰值 Marker** 方案（见 `Services/N9020aController.cs`）：

| 项目 | 取值 | 说明 |
|------|------|------|
| 模式 | Swept SA (SA) | `INST:SEL SA` + `CONF:SAN` |
| RBW | 1 MHz | 关闭自动，固定分辨带宽 |
| 检波 | Positive Peak | 峰值检波 |
| 内部衰减 | 0 dB | 外部 10 dB 衰减器提供余量 |
| 参考电平 | 10 dBm | 显示参考 |
| 扫宽 | 20 MHz | 每个中心频率两侧各 10 MHz |
| 扫描点数 | 401 | 兼顾速度与分辨率 |
| MaxHold | 12 次扫描累积 | 稳定捕获突发式 LE 分组发射 |

每个测试点对基波中心与 `2×基波` 中心各执行一次「复位 trace → MaxHold 累积 → 峰值搜索」，读取 Marker 的 `X/Y`。每轮结束后轮询 `SYST:ERR?` 清空仪器错误队列。

判定与补偿：

```
FundDut = FundRaw + LineLoss
H2Dut   = H2Raw   + LineLoss
H2dBc   = H2Dut - FundDut        # 等价于 H2Raw - FundRaw
PASS    = H2Dut < H2Limit
```

---

## 项目结构

```
BTHarmonic2Tester/
├─ App.xaml / App.xaml.cs          # 应用入口与深色主题资源（样式、画刷、控件模板）
├─ MainWindow.xaml / .xaml.cs      # 主界面布局与日志自动滚动
├─ Models/
│  └─ HarmonicModels.cs            # BlePhy 枚举、BleTestPoint（频率计算）、测量结果记录
├─ ViewModels/
│  ├─ MainViewModel.cs             # 核心逻辑：连接、测试流程编排、结果与日志
│  ├─ ViewModelBase.cs             # INotifyPropertyChanged 基类
│  └─ RelayCommand.cs              # ICommand 实现
├─ Services/
│  ├─ N9020aController.cs          # N9020A 原生 TCP SCPI 驱动（端口 5025）
│  ├─ BtDutController.cs           # RTL8773D DUT 高层驱动（连接 / LE TX 起停）
│  └─ RtlBtMpInterop.cs            # RtlBluetoothMP.dll 的 P/Invoke 底层绑定
├─ Native/
│  ├─ RtlBluetoothMP.dll           # Realtek BT MP 原生库（32 位，随程序部署）
│  └─ RtlBluetoothMP.h             # 对应的 C 头文件（结构体 / 枚举参考）
├─ Properties/PublishProfiles/     # 发布配置（win-x86 单文件自包含）
└─ .github/workflows/release.yml   # 打 tag 自动构建并发布 EXE 的 CI
```

### 原生互操作说明

`RtlBluetoothMP.dll` 导出两个 C 函数用于构建接口与蓝牙 MP 模块：

- `BTMPAPI_BuildInterfaceRTK` — 建立 UART 接口对象
- `BTMPAPI_BuildBluetoothModule` — 建立 BT MP 模块

模块与接口本身是「首成员为函数指针」的 C 结构体，代码通过读取结构体偏移拿到函数指针再回调（`Open/Close/UpDataParameter/ActionControlExcute/ActionReport`）。DUT 连接序列严格对齐厂商 App：

```
读芯片信息(建立 UART 同步/MP 命令版本) → HCI Reset → Test Mode Enable → 读 TX 功率表 → LE TX
```

结构体偏移、`Pack=8` 对齐等均按 `Native/RtlBluetoothMP.h` 的 x86 布局设定，修改前请对照头文件。

---

## 编译与打包

```bash
# 调试
dotnet build

# 发布（Release，win-x86，自包含单文件，压缩）
dotnet publish -c Release -r win-x86 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true
```

打包要点：

- `RtlBluetoothMP.dll` **被排除在单文件之外**（`ExcludeFromSingleFile=true`），以便 P/Invoke 从程序目录加载，会随产物一起复制到输出目录。
- 目标框架 `net10.0-windows`，`UseWPF=true`，`PlatformTarget=x86`。
- 卫星资源仅保留英文（`SatelliteResourceLanguages=en`）以缩减体积。
- 构建时会由 `GenerateBuildTime` 目标生成一个带 UTC 时间戳的内部常量类（`BuildInfo`）。

---

## 持续集成 / 发布

`.github/workflows/release.yml` 提供了通用的自动发布流程：

- **触发**：推送 `v*` / `V*` 形式的 tag（也可在 Actions 页面手动触发，仅产出构建物、不建 Release）。
- **流程**：`windows-latest` runner 上安装 .NET 10 → 自动定位唯一 `csproj` → `dotnet publish` 单文件自包含 EXE → 取 publish 目录中体积最大的 EXE 重命名为「仓库名 + 版本」→ 发布到 GitHub Releases。

发布一个版本：

```bash
git tag V1.0
git push origin V1.0
```

> 注：本项目因 32 位原生库需要 **x86**，工作流已相应使用 `win-x86` 发布，以保证与 `RtlBluetoothMP.dll` 匹配（若改回 `win-x64` 会因 `PlatformTarget=x86` 不兼容而报 `NETSDK1032`）。

---

## 常见问题

| 现象 | 可能原因 / 处理 |
|------|-----------------|
| DUT 连接后每条命令返回 code 1 | UART 未完成协议同步；连接序列已内置「先读芯片信息」以建立同步，确认波特率与接线正确 |
| `BuildInterfaceRTK failed` | COM 口占用 / 号码解析失败 / DLL 缺失，确认 `RtlBluetoothMP.dll` 在程序目录且为 32 位 |
| 程序启动即报无法加载 DLL | 主程序未以 x86 运行，或缺少 32 位 `RtlBluetoothMP.dll` |
| 频谱仪连接失败 | 检查 IP / 网段 / 端口 5025 是否可达，仪器是否开启 SCPI raw socket |
| 谐波读数异常偏低 / 偏高 | 核对线损（Line Loss）与外部衰减器、线缆实际损耗是否一致 |
| TX 功率与预期不符 | `TxGainIndex` 为芯片 / 校准相关索引，请对照 DUT efuse / 功率表在真机验证 |

---

## 已知限制

- 仅实现驱动 BLE（LE）直接测试模式发射所需的最小 MP 流程（build → open → LE TX start → LE TX end）。
- DUT 绝对发射功率依赖 `TxGainIndex`，为芯片 / 校准相关值，需在真实硬件上校核。
- 测量方法参数（RBW、扫宽、MaxHold 次数等）为固化方案，仅线损与限值对用户开放。
- 结构体偏移与内存布局针对 x86 硬编码，更换芯片 / DLL 版本时需重新核对 `RtlBluetoothMP.h`。
