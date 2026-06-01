using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Services.Common;
using Nop.Services.Directory;

namespace Nop.Plugin.Payments.Midtrans.Services;

/// <summary>
/// Talks to the Midtrans Snap API and verifies notification signatures.
/// </summary>
public class MidtransService
{
    #region Fields

    protected readonly HttpClient _httpClient;
    protected readonly IAddressService _addressService;
    protected readonly ICurrencyService _currencyService;
    protected readonly CurrencySettings _currencySettings;
    protected readonly MidtransPaymentSettings _settings;

    #endregion

    #region Ctor

    public MidtransService(HttpClient httpClient,
        IAddressService addressService,
        ICurrencyService currencyService,
        CurrencySettings currencySettings,
        MidtransPaymentSettings settings)
    {
        _httpClient = httpClient;
        _addressService = addressService;
        _currencyService = currencyService;
        _currencySettings = currencySettings;
        _settings = settings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Create a Snap transaction for the given order and return the hosted payment redirect URL.
    /// </summary>
    /// <param name="order">The order to be paid</param>
    /// <param name="finishUrl">URL Midtrans redirects the customer back to after payment</param>
    /// <returns>The Snap redirect_url</returns>
    public async Task<string> CreateSnapTransactionAsync(Order order, string finishUrl)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (string.IsNullOrEmpty(_settings.ServerKey))
            throw new NopException("Midtrans Server Key is not configured.");

        // Midtrans settles only in IDR, and order monetary fields are stored in the PRIMARY store
        // currency — so that currency must be IDR or the gross_amount below would be wrong.
        var primaryCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
        if (!string.Equals(primaryCurrency?.CurrencyCode, "IDR", StringComparison.OrdinalIgnoreCase))
            throw new NopException(
                $"Midtrans only accepts IDR, but the primary store currency is '{primaryCurrency?.CurrencyCode ?? "unset"}'. " +
                "Set IDR as the primary store currency (Configuration → Currencies) before accepting Midtrans payments.");

        // IDR has no minor units -> gross_amount must be an integer
        var grossAmount = (long)Math.Round(order.OrderTotal, MidpointRounding.AwayFromZero);

        var billing = order.BillingAddressId > 0
            ? await _addressService.GetAddressByIdAsync(order.BillingAddressId)
            : null;

        // NOTE: item_details is intentionally omitted so we never trip Midtrans'
        // "gross_amount must equal sum(item_details)" validation on rounded IDR totals.
        var payload = new
        {
            transaction_details = new
            {
                order_id = order.OrderGuid.ToString(),
                gross_amount = grossAmount
            },
            customer_details = new
            {
                first_name = billing?.FirstName ?? string.Empty,
                last_name = billing?.LastName ?? string.Empty,
                email = billing?.Email ?? string.Empty,
                phone = billing?.PhoneNumber ?? string.Empty
            },
            callbacks = new { finish = finishUrl }
        };

        var url = _settings.UseSandbox
            ? MidtransDefaults.SandboxSnapUrl
            : MidtransDefaults.ProductionSnapUrl;

        using var request = new HttpRequestMessage(HttpMethod.Post, url);

        // HTTP Basic auth: base64("<ServerKey>:")
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ServerKey}:"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new NopException($"Midtrans Snap error ({(int)response.StatusCode}): {body}");

        var snap = JsonSerializer.Deserialize<SnapTransactionResponse>(body);
        if (string.IsNullOrEmpty(snap?.RedirectUrl))
            throw new NopException($"Midtrans Snap did not return a redirect_url. Response: {body}");

        return snap.RedirectUrl;
    }

    /// <summary>
    /// Verify a Midtrans notification signature.
    /// signature_key = SHA512(order_id + status_code + gross_amount + ServerKey)
    /// </summary>
    public bool IsValidSignature(MidtransNotification notification)
    {
        if (notification == null || string.IsNullOrEmpty(notification.SignatureKey))
            return false;

        var raw = $"{notification.OrderId}{notification.StatusCode}{notification.GrossAmount}{_settings.ServerKey}";
        var expected = SHA512.HashData(Encoding.UTF8.GetBytes(raw));

        // Compare in constant time: this check gates marking orders Paid, so the public webhook
        // must not be probe-able for a valid signature byte-by-byte via a timing side-channel.
        if (!TryParseHex(notification.SignatureKey, out var provided) || provided.Length != expected.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    /// <summary>
    /// Parses a hex string to bytes without throwing on attacker-supplied malformed input.
    /// </summary>
    private static bool TryParseHex(string hex, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
            return false;
        try
        {
            bytes = Convert.FromHexString(hex);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    #endregion
}
