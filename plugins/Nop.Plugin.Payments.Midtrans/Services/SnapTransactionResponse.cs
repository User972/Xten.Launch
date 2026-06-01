using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.Midtrans.Services;

/// <summary>
/// Response from the Midtrans Snap "create transaction" endpoint
/// </summary>
public class SnapTransactionResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; }

    [JsonPropertyName("redirect_url")]
    public string RedirectUrl { get; set; }
}
