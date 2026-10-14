using System.Net;
using System.Net.Http.Json;
using Payments.Application.Invoices;

namespace Payments.IntegrationTests;

[Collection(PaymentsTests.Name)]
public sealed class CaptureTests(PaymentsFixture api)
{
    private readonly Guid _tenant = Guid.NewGuid();


    private HttpClient Client() => api.ClientFor(_tenant);

    private static async Task<Guid> NewInvoice(HttpClient http, decimal amount = 120m)
    {
        var response = await http.PostAsJsonAsync("/v1/invoices", new { amount, currency = "USD", reference = "PO-1" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<InvoiceView>())!.Id;
    }

    private static async Task<HttpResponseMessage> Capture(HttpClient http, Guid invoiceId, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/invoices/{invoiceId}/capture");
        request.Headers.Add("Idempotency-Key", key);
        return await http.SendAsync(request);
    }

    private Task<int> ChargesFor(Guid invoiceId) =>
        api.CountAsync("SELECT COUNT(*) FROM dbo.Payments WHERE InvoiceId = @invoiceId AND Kind = 'capture'", new { invoiceId });

    [Fact]
    public async Task Capture_twice_with_the_same_key_charges_once()
    {
        using var http = Client();
        var invoice = await NewInvoice(http);
        var chargesBefore = api.Gateway.ChargeCount;

        var first = await Capture(http, invoice, $"key-{invoice}");
        var second = await Capture(http, invoice, $"key-{invoice}");

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await second.Content.ReadFromJsonAsync<PaymentReceipt>())!.PaymentId
            .ShouldBe((await first.Content.ReadFromJsonAsync<PaymentReceipt>())!.PaymentId);
        (await ChargesFor(invoice)).ShouldBe(1);
        (api.Gateway.ChargeCount - chargesBefore).ShouldBe(1);
    }

    [Fact]
    public async Task Concurrent_captures_with_the_same_key_produce_one_charge()
    {
        using var http = Client();
        var invoice = await NewInvoice(http);
        var chargesBefore = api.Gateway.ChargeCount;

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Capture(http, invoice, $"same-{invoice}")));

        responses.Select(r => r.StatusCode).ShouldAllBe(s => s == HttpStatusCode.Created || s == HttpStatusCode.OK);
        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        var ids = await Task.WhenAll(responses.Select(async r => (await r.Content.ReadFromJsonAsync<PaymentReceipt>())!.PaymentId));
        ids.Distinct().Count().ShouldBe(1);
        (await ChargesFor(invoice)).ShouldBe(1);
        (api.Gateway.ChargeCount - chargesBefore).ShouldBe(1);
    }

    [Fact]
    public async Task Concurrent_captures_with_different_keys_let_exactly_one_win()
    {
        using var http = Client();
        var invoice = await NewInvoice(http);
        var chargesBefore = api.Gateway.ChargeCount;

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => Capture(http, invoice, $"k{i}-{invoice}")));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(4);
        (await ChargesFor(invoice)).ShouldBe(1);
        (api.Gateway.ChargeCount - chargesBefore).ShouldBe(1);
    }

    [Fact]
    public async Task Transient_gateway_failures_are_retried()
    {
        using var http = Client();
        var invoice = await NewInvoice(http);
        api.Gateway.FailNext(2);

        var response = await Capture(http, invoice, $"retry-{invoice}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        api.Gateway.FailNext(0);
    }

    [Fact]
    public async Task Gateway_down_returns_502_and_leaves_the_invoice_open()
    {
        using var http = Client();
        var invoice = await NewInvoice(http);
        api.Gateway.FailNext(10);

        var response = await Capture(http, invoice, $"down-{invoice}");
        api.Gateway.FailNext(0);

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        (await http.GetFromJsonAsync<InvoiceView>($"/v1/invoices/{invoice}"))!.Status.ShouldBe("open");
        (await ChargesFor(invoice)).ShouldBe(0);
    }

    [Fact]
    public async Task Capture_writes_the_event_to_the_outbox_in_the_same_transaction()
    {
        using var http = Client();
        var invoice = await NewInvoice(http);

        await Capture(http, invoice, $"evt-{invoice}");

        (await api.CountAsync(
            "SELECT COUNT(*) FROM dbo.Outbox WHERE Type = 'payment.captured' AND JSON_VALUE(Payload, '$.invoiceId') = @id",
            new { id = invoice.ToString() })).ShouldBe(1);
    }

    [Fact]
    public async Task Refund_marks_the_invoice_refunded()
    {
        using var http = Client();
        var invoice = await NewInvoice(http);
        var capture = await (await Capture(http, invoice, $"cap-{invoice}")).Content.ReadFromJsonAsync<PaymentReceipt>();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/payments/{capture!.PaymentId}/refund");
        request.Headers.Add("Idempotency-Key", $"ref-{invoice}");
        var refund = await http.SendAsync(request);

        refund.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await http.GetFromJsonAsync<InvoiceView>($"/v1/invoices/{invoice}"))!.Status.ShouldBe("refunded");
    }

    [Fact]
    public async Task Missing_idempotency_key_is_a_400()
    {
        using var http = Client();
        var invoice = await NewInvoice(http);

        var response = await http.PostAsync(new Uri($"/v1/invoices/{invoice}/capture", UriKind.Relative), null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("items:read", HttpStatusCode.Forbidden)]
    [InlineData("payments:read", HttpStatusCode.Forbidden)]
    [InlineData("payments:write", HttpStatusCode.Created)]
    public async Task Creating_invoices_needs_payments_write(string permission, HttpStatusCode expected)
    {
        using var http = api.ClientFor(_tenant, permission);

        var response = await http.PostAsJsonAsync("/v1/invoices", new { amount = 5m, currency = "USD", reference = "X" });

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task Invoices_are_tenant_scoped()
    {
        using var http = Client();
        var invoice = await NewInvoice(http);
        using var other = api.ClientFor(Guid.NewGuid());

        (await other.GetAsync(new Uri($"/v1/invoices/{invoice}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Capture(other, invoice, "x")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
