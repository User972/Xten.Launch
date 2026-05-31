using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Midtrans.Components;

/// <summary>
/// Public "payment info" view component. Not rendered during checkout because
/// SkipPaymentInfo = true, but the IPaymentMethod contract requires a valid type.
/// </summary>
public class MidtransViewComponent : NopViewComponent
{
    public IViewComponentResult Invoke()
    {
        return View("~/Plugins/Payments.Midtrans/Views/PaymentInfo.cshtml");
    }
}
