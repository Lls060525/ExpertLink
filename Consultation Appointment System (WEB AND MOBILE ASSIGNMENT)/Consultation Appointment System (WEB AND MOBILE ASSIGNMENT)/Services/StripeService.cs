using ExpertLink.Models;
using Stripe;
using Stripe.Checkout;

namespace ExpertLink.Services;

// ---------------------------------------------------------------------------
// 3rd-party payment API: Stripe Checkout (TEST MODE)
// Put your test secret key (sk_test_...) in appsettings.Local.json:
//   "Stripe": { "SecretKey": "sk_test_..." }
// If no key is configured, the Stripe option is hidden and the simulated
// gateway (Card / FPX / E-Wallet) is used instead.
// ---------------------------------------------------------------------------
public class StripeService(IConfiguration cf, ILogger<StripeService> log)
{
    public const decimal MinAmount = 2.00m;   // Stripe minimum charge for MYR

    private string? Key => cf["Stripe:SecretKey"];

    public bool Enabled => !string.IsNullOrWhiteSpace(Key);

    private StripeClient Client => new(Key);

    // Creates a hosted Checkout Session and returns the Stripe payment page URL
    public async Task<string> CreateCheckoutAsync(Appointment a, string successUrl, string cancelUrl)
    {
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            PaymentMethodTypes = ["card"],
            CustomerEmail = a.Client.Email,
            ClientReferenceId = a.Id.ToString(),
            Metadata = new Dictionary<string, string> { ["appointmentId"] = a.Id.ToString(), ["reference"] = a.Reference },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "myr",
                        UnitAmount = (long)Math.Round(a.Fee * 100),   // amount in sen
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"{a.Service.Name} with {a.Consultant.Name}",
                            Description = $"{a.Reference} · {a.Date:ddd, d MMM yyyy} · {a.StartTime.Time12()} - {a.EndTime.Time12()} · {a.Mode.Text()}",
                        },
                    },
                },
            ],
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
        };
        var session = await new SessionService(Client).CreateAsync(options);
        return session.Url;
    }

    // Reads the session back from Stripe (never trust the redirect alone)
    public Task<Session> GetSessionAsync(string sessionId) =>
        new SessionService(Client).GetAsync(sessionId, new SessionGetOptions { Expand = ["payment_intent.latest_charge"] });

    // Full refund of a Stripe payment. Returns false if Stripe rejected it.
    public async Task<bool> RefundAsync(string? paymentIntentId)
    {
        if (string.IsNullOrEmpty(paymentIntentId) || !Enabled) return false;
        try
        {
            await new RefundService(Client).CreateAsync(new RefundCreateOptions { PaymentIntent = paymentIntentId });
            return true;
        }
        catch (StripeException ex)
        {
            log.LogError(ex, "Stripe refund failed for {PaymentIntent}", paymentIntentId);
            return false;
        }
    }
}
