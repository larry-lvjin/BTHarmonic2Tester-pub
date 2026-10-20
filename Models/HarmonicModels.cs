namespace BTHarmonic2Tester.Models;

/// <summary>BLE PHY under test.</summary>
public enum BlePhy
{
    Le1M = 1,   // matches Realtek LE5_TX_1M_PHY
    Le2M = 2    // matches Realtek LE5_TX_2M_PHY
}

/// <summary>
/// One BLE second-harmonic test point: a PHY on a specific channel.
/// BLE test channel N has fundamental frequency 2402 + 2*N MHz;
/// the 2nd harmonic sits at twice that frequency.
/// </summary>
public sealed class BleTestPoint(BlePhy phy, int channel)
{
    public BlePhy Phy { get; } = phy;
    public int Channel { get; } = channel;

    public string PhyName => Phy == BlePhy.Le1M ? "1M" : "2M";

    /// <summary>Fundamental frequency in Hz (2402 + 2*N MHz).</summary>
    public long FundamentalHz => (2402L + 2L * Channel) * 1_000_000L;

    /// <summary>Second-harmonic frequency in Hz.</summary>
    public long SecondHarmonicHz => FundamentalHz * 2;

    public string ChannelDisplay => $"Ch {Channel}";
}

/// <summary>
/// Raw + compensated result of a single second-harmonic measurement.
/// All power values in dBm unless noted.
/// </summary>
public sealed record HarmonicMeasurement(
    double FundRawDbm,       // fundamental measured at analyzer input
    double H2DbcRaw,         // H2 relative to fundamental, as read from analyzer (dBc)
    double FundFreqHz,
    double H2FreqHz,
    double ThdPercent);
