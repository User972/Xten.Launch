using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Midtrans.Components;
using Nop.Plugin.Payments.Midtrans.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.Midtrans;

/// <summary>
/// Midtrans (Snap) payment processor.
/// Flow: customer is redirected to Midtrans Snap to pay (QRIS / VA / e-wallet / card).
/// The order stays Pending until Midtrans sends a signature-verified server-to-server
/// notification with status "settlement" (or "capture"+accept), at which point the order
/// is marked Paid — which auto-activates downloadable products configured with
/// "Download activation type = When order is paid".
/// </summary>
public class MidtransPaymentProcessor : BasePlugin, IPaymentMethod
{
    #region Fields

    protected readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;
    protected readonly MidtransPaymentSettings _midtransPaymentSettings;
    protected readonly MidtransService _midtransService;

    #endregion

    #region Ctor

    public MidtransPaymentProcessor(IHttpContextAccessor httpContextAccessor,
        ILocalizationService localizationService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        IWebHelper webHelper,
        MidtransPaymentSettings midtransPaymentSettings,
        MidtransService midtransService)
    {
        _httpContextAccessor = httpContextAccessor;
        _localizationService = localizationService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _midtransPaymentSettings = midtransPaymentSettings;
        _midtransService = midtransService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Process a payment. Nothing happens here for a redirection method — the order
    /// remains Pending until the gateway confirms settlement via the notification webhook.
    /// </summary>
    public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        return Task.FromResult(new ProcessPaymentResult());
    }

    /// <summary>
    /// Post process payment — create the Snap transaction and redirect the customer to Midtrans.
    /// </summary>
    public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        var order = postProcessPaymentRequest.Order;

        var storeLocation = _webHelper.GetStoreLocation();
        var finishUrl = $"{storeLocation}Plugins/PaymentMidtrans/Return?orderId={order.OrderGuid}";

        var redirectUrl = await _midtransService.CreateSnapTransactionAsync(order, finishUrl);

        _httpContextAccessor.HttpContext?.Response.Redirect(redirectUrl);
    }

    /// <summary>
    /// Hide the method during checkout if it is not configured yet (no Server Key).
    /// </summary>
    public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        return Task.FromResult(string.IsNullOrEmpty(_midtransPaymentSettings.ServerKey));
    }

    /// <summary>
    /// Gets additional handling fee
    /// </summary>
    public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
    {
        return await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(cart,
            _midtransPaymentSettings.AdditionalFee, _midtransPaymentSettings.AdditionalFeePercentage);
    }

    /// <summary>
    /// Capture is handled by Midtrans; not supported from nopCommerce.
    /// </summary>
    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
    {
        return Task.FromResult(new CapturePaymentResult { Errors = new[] { "Capture method not supported" } });
    }

    /// <summary>
    /// Online refunds are not implemented in v1 — process refunds in the Midtrans dashboard,
    /// then use nopCommerce "Refund (offline)" on the order to keep statuses in sync.
    /// </summary>
    public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
    {
        return Task.FromResult(new RefundPaymentResult { Errors = new[] { "Online refund not supported — refund in the Midtrans dashboard, then use Refund (offline)." } });
    }

    /// <summary>
    /// Void is not supported.
    /// </summary>
    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
    {
        return Task.FromResult(new VoidPaymentResult { Errors = new[] { "Void method not supported" } });
    }

    /// <summary>
    /// Recurring payments are not supported.
    /// </summary>
    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        var result = new ProcessPaymentResult();
        result.AddError("Recurring payment not supported");
        return Task.FromResult(result);
    }

    /// <summary>
    /// Cancels a recurring payment.
    /// </summary>
    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
    {
        return Task.FromResult(new CancelRecurringPaymentResult());
    }

    /// <summary>
    /// This is a redirection payment method, so customers may complete payment after the
    /// order is placed (e.g. if they closed the Snap window before paying).
    /// </summary>
    public Task<bool> CanRePostProcessPaymentAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return Task.FromResult(true);
    }

    /// <summary>
    /// No on-site payment form, so nothing to validate.
    /// </summary>
    public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
    {
        return Task.FromResult<IList<string>>(new List<string>());
    }

    /// <summary>
    /// No on-site payment fields are collected.
    /// </summary>
    public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
    {
        return Task.FromResult(new ProcessPaymentRequest());
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/PaymentMidtrans/Configure";
    }

    /// <summary>
    /// Gets the view component used in the public store. With SkipPaymentInfo = true this is
    /// not rendered during checkout, but the interface requires a valid type.
    /// </summary>
    public Type GetPublicViewComponent()
    {
        return typeof(MidtransViewComponent);
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new MidtransPaymentSettings
        {
            UseSandbox = true
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.Midtrans.Fields.UseSandbox"] = "Use sandbox",
            ["Plugins.Payments.Midtrans.Fields.UseSandbox.Hint"] = "Determines whether to use the Midtrans sandbox environment for testing.",
            ["Plugins.Payments.Midtrans.Fields.ServerKey"] = "Server key",
            ["Plugins.Payments.Midtrans.Fields.ServerKey.Hint"] = "Your Midtrans Server Key. Kept server-side; used for Snap API authentication and notification signature verification.",
            ["Plugins.Payments.Midtrans.Fields.ClientKey"] = "Client key",
            ["Plugins.Payments.Midtrans.Fields.ClientKey.Hint"] = "Your Midtrans Client Key.",
            ["Plugins.Payments.Midtrans.Fields.AdditionalFee"] = "Additional fee",
            ["Plugins.Payments.Midtrans.Fields.AdditionalFee.Hint"] = "Enter an additional fee to charge your customers.",
            ["Plugins.Payments.Midtrans.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
            ["Plugins.Payments.Midtrans.Fields.AdditionalFeePercentage.Hint"] = "Apply a percentage additional fee to the order total. If disabled, a fixed value is used.",
            ["Plugins.Payments.Midtrans.NotifyUrlLabel"] = "Payment Notification URL (set this in your Midtrans dashboard → Settings → Configuration):",
            ["Plugins.Payments.Midtrans.PaymentMethodDescription"] = "You will be redirected to Midtrans to complete payment (QRIS, bank transfer / Virtual Account, or e-wallet)."
        });

        await base.InstallAsync();
    }

    /// <summary>
    /// Uninstall the plugin
    /// </summary>
    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<MidtransPaymentSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.Midtrans");

        await base.UninstallAsync();
    }

    /// <summary>
    /// Gets the payment method description shown on the checkout "payment method" step.
    /// </summary>
    public async Task<string> GetPaymentMethodDescriptionAsync()
    {
        return await _localizationService.GetResourceAsync("Plugins.Payments.Midtrans.PaymentMethodDescription");
    }

    #endregion

    #region Properties

    public bool SupportCapture => false;

    public bool SupportPartiallyRefund => false;

    public bool SupportRefund => false;

    public bool SupportVoid => false;

    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

    public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

    public bool SkipPaymentInfo => true;

    #endregion
}
