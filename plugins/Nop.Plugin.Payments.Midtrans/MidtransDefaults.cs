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
}
