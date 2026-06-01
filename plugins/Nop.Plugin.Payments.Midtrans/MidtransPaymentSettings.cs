using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Midtrans;

/// <summary>
/// Represents settings of the Midtrans payment plugin
/// </summary>
public class MidtransPaymentSettings : ISettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to use the Midtrans sandbox environment
    /// </summary>
    public bool UseSandbox { get; set; }

    /// <summary>
    /// Gets or sets the Midtrans Server Key (kept server-side; used for API auth and notification signature verification)
    /// </summary>
    public string ServerKey { get; set; }

    /// <summary>
    /// Gets or sets the Midtrans Client Key
    /// </summary>
    public string ClientKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the additional fee is a percentage (true) or a fixed value (false)
    /// </summary>
    public bool AdditionalFeePercentage { get; set; }

    /// <summary>
    /// Gets or sets an additional fee
    /// </summary>
    public decimal AdditionalFee { get; set; }
}
