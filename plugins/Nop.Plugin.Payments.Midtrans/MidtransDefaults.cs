namespace Nop.Plugin.Payments.Midtrans;

/// <summary>
/// Represents constants for the Midtrans payment plugin
/// </summary>
public static class MidtransDefaults
{
    /// <summary>
    /// Plugin system name
    /// </summary>
    public static string SystemName => "Payments.Midtrans";

    /// <summary>
    /// Route names
    /// </summary>
    public static string ConfigurationRouteName => "Plugin.Payments.Midtrans.Configure";
    public static string NotifyRouteName => "Plugin.Payments.Midtrans.Notify";
    public static string ReturnRouteName => "Plugin.Payments.Midtrans.Return";

    /// <summary>
    /// Midtrans Snap "create transaction" endpoints
    /// </summary>
    public static string SandboxSnapUrl => "https://app.sandbox.midtrans.com/snap/v1/transactions";
    public static string ProductionSnapUrl => "https://app.midtrans.com/snap/v1/transactions";

    /// <summary>
    /// Timeout for Midtrans Snap API calls. Kept well under HttpClient's default 100s because the
    /// call runs inline during checkout (the customer is waiting), so a hung connection must not
    /// tie up the request. The order is already placed and can be re-paid, so a timeout is recoverable.
    /// </summary>
    public static TimeSpan ApiTimeout => TimeSpan.FromSeconds(30);
}
