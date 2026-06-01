using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Midtrans.Services;
using Nop.Services.Orders;

namespace Nop.Plugin.Payments.Midtrans.Controllers;

/// <summary>
/// Public, unauthenticated endpoints for Midtrans:
///  - Notify: server-to-server payment notification (signature-verified)
///  - Return: customer redirect target after paying (callbacks.finish)
/// </summary>
public class PaymentMidtransWebhookController : Controller
{
    #region Fields

    protected readonly ILogger<PaymentMidtransWebhookController> _logger;
    protected readonly IOrderProcessingService _orderProcessingService;
    protected readonly IOrderService _orderService;
    protected readonly IWebHelper _webHelper;
    protected readonly MidtransPaymentSettings _settings;
    protected readonly MidtransService _midtransService;

    #endregion

    #region Ctor

    public PaymentMidtransWebhookController(ILogger<PaymentMidtransWebhookController> logger,
        IOrderProcessingService orderProcessingService,
        IOrderService orderService,
        IWebHelper webHelper,
        MidtransPaymentSettings settings,
        MidtransService midtransService)
    {
        _logger = logger;
        _orderProcessingService = orderProcessingService;
        _orderService = orderService;
        _webHelper = webHelper;
        _settings = settings;
        _midtransService = midtransService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Server-to-server payment notification. Verifies the SHA512 signature, then marks the
    /// order as paid on a settled/captured payment (idempotent).
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Notify([FromBody] MidtransNotification notification)
    {
        if (notification == null || string.IsNullOrEmpty(notification.OrderId))
            return BadRequest();

        // Reject anything we cannot cryptographically attribute to our Server Key. This logs to the
        // framework logger (container stdout, bounded/rotated) rather than the nopCommerce DB log,
        // so a flood of unsigned POSTs to this public endpoint can't grow the Log table unbounded.
        if (string.IsNullOrEmpty(_settings.ServerKey) || !_midtransService.IsValidSignature(notification))
        {
            _logger.LogWarning("Midtrans: rejected notification with invalid signature for order_id '{OrderId}'.", notification.OrderId);
            return BadRequest();
        }

        if (!Guid.TryParse(notification.OrderId, out var orderGuid))
            return BadRequest();

        var order = await _orderService.GetOrderByGuidAsync(orderGuid);
        if (order == null)
            return NotFound();

        // Defense in depth (the signature already authenticates these fields): only act on orders
        // that actually used Midtrans, and only when the notified amount matches what we charged.
        if (!string.Equals(order.PaymentMethodSystemName, MidtransDefaults.SystemName, StringComparison.OrdinalIgnoreCase))
        {
            await InsertNoteAsync(order, $"Midtrans: ignored notification — order uses payment method '{order.PaymentMethodSystemName}', not '{MidtransDefaults.SystemName}'.");
            return Ok();
        }

        var expectedAmount = (long)Math.Round(order.OrderTotal, MidpointRounding.AwayFromZero);
        if (!decimal.TryParse(notification.GrossAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var notifiedAmount)
            || (long)Math.Round(notifiedAmount, MidpointRounding.AwayFromZero) != expectedAmount)
        {
            _logger.LogWarning("Midtrans: amount mismatch for order {OrderId} — notified gross_amount '{Notified}' != order total {Expected}.",
                notification.OrderId, notification.GrossAmount, expectedAmount);
            await InsertNoteAsync(order, $"Midtrans: amount mismatch — notified gross_amount '{notification.GrossAmount}' != order total {expectedAmount}. NOT marked paid; review manually.");
            return Ok();
        }

        var status = notification.TransactionStatus?.ToLowerInvariant();
        var fraud = notification.FraudStatus?.ToLowerInvariant();

        // "settlement" = funds captured (QRIS/VA/e-wallet). "capture" + accept = card captured.
        var isPaid = status == "settlement" || (status == "capture" && (string.IsNullOrEmpty(fraud) || fraud == "accept"));

        if (isPaid)
        {
            // CanMarkOrderAsPaid is false if already paid/refunded -> idempotent no-op on retries.
            if (_orderProcessingService.CanMarkOrderAsPaid(order))
            {
                await _orderProcessingService.MarkOrderAsPaidAsync(order);
                await InsertNoteAsync(order, $"Midtrans: '{status}' confirmed (transaction_id: {notification.TransactionId}). Order marked as paid; downloadable products activated.");
            }
        }
        else if (status is "deny" or "cancel" or "expire" or "failure")
        {
            await InsertNoteAsync(order, $"Midtrans: payment '{status}' (transaction_id: {notification.TransactionId}).");
        }
        else
        {
            await InsertNoteAsync(order, $"Midtrans: status '{status}' received (transaction_id: {notification.TransactionId}).");
        }

        return Ok();
    }

    /// <summary>
    /// Customer is redirected here after the Snap flow. Sends them to the order-completed page.
    /// (The order may still be Pending here; the Notify webhook is the source of truth for payment.)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Return(Guid orderId)
    {
        var order = await _orderService.GetOrderByGuidAsync(orderId);
        if (order == null)
            return Redirect(_webHelper.GetStoreLocation());

        return Redirect($"{_webHelper.GetStoreLocation()}checkout/completed/{order.Id}");
    }

    #endregion

    #region Utilities

    protected async Task InsertNoteAsync(Order order, string note)
    {
        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = note,
            DisplayToCustomer = false,
            CreatedOnUtc = DateTime.UtcNow
        });
    }

    #endregion
}
