using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.Midtrans.Models;

public record ConfigurationModel : BaseNopModel
{
    public int ActiveStoreScopeConfiguration { get; set; }

    /// <summary>
    /// Read-only helper shown in the UI so the admin can copy it into the Midtrans dashboard.
    /// </summary>
    public string NotifyUrl { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Midtrans.Fields.UseSandbox")]
    public bool UseSandbox { get; set; }
    public bool UseSandbox_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Midtrans.Fields.ServerKey")]
    public string ServerKey { get; set; }
    public bool ServerKey_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Midtrans.Fields.ClientKey")]
    public string ClientKey { get; set; }
    public bool ClientKey_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Midtrans.Fields.AdditionalFee")]
    public decimal AdditionalFee { get; set; }
    public bool AdditionalFee_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Midtrans.Fields.AdditionalFeePercentage")]
    public bool AdditionalFeePercentage { get; set; }
    public bool AdditionalFeePercentage_OverrideForStore { get; set; }
}
