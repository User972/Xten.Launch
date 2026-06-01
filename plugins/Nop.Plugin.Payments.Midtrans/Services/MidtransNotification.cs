using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.Midtrans.Services;

/// <summary>
/// Payload of a Midtrans HTTP(S) payment notification (webhook).
/// gross_amount is intentionally a string — it must be hashed exactly as received
/// when verifying the signature.
/// </summary>
public class MidtransNotification
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; set; }

    [JsonPropertyName("status_code")]
    public string StatusCode { get; set; }

    [JsonPropertyName("gross_amount")]
    public string GrossAmount { get; set; }

    [JsonPropertyName("signature_key")]
    public string SignatureKey { get; set; }

    [JsonPropertyName("transaction_status")]
    public string TransactionStatus { get; set; }

    [JsonPropertyName("fraud_status")]
    public string FraudStatus { get; set; }

    [JsonPropertyName("payment_type")]
    public string PaymentType { get; set; }

    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; }
}
