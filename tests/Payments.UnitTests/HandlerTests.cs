using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Domain;
using Payments.Application.Invoices;
using Payments.Application.Mediator;

namespace Payments.UnitTests;

public class HandlerTests
{
    private readonly Fakes _fakes = new();
    private readonly Guid _tenant = Guid.NewGuid();

    private async Task<T> Send<T>(IRequest<T> request)
    {
        await using var provider = _fakes.BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    private Task<Guid> NewInvoice(decimal amount = 120m) => Send(new CreateInvoice(_tenant, amount, "USD", "PO-1"));

    [Fact]
    public async Task Create_invoice_stores_an_open_invoice()
    {
        var id = await NewInvoice(99.99m);

        var view = await Send(new GetInvoice(_tenant, id));
        view.ShouldNotBeNull();
        view.Status.ShouldBe("open");
        view.Amount.ShouldBe(99.99m);
    }

    [Fact]
    public async Task Capture_charges_once_marks_paid_and_records_an_event()
    {
        var id = await NewInvoice();

        var receipt = await Send(new CapturePayment(_tenant, id, "key-1"));

        receipt.Replayed.ShouldBeFalse();
        receipt.Amount.ShouldBe(120m);
        _fakes.Charges.ShouldBe(["key-1"]);
        (await Send(new GetInvoice(_tenant, id)))!.Status.ShouldBe("paid");
        _fakes.Events.Single().Type.ShouldBe("payment.captured");
    }

    [Fact]
    public async Task Capture_with_the_same_key_replays_without_charging_again()
    {
        var id = await NewInvoice();
        var first = await Send(new CapturePayment(_tenant, id, "key-1"));

        var second = await Send(new CapturePayment(_tenant, id, "key-1"));

        second.Replayed.ShouldBeTrue();
        second.PaymentId.ShouldBe(first.PaymentId);
        _fakes.Charges.Count.ShouldBe(1);
        _fakes.Events.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Capturing_a_paid_invoice_with_a_new_key_is_a_conflict()
    {
        var id = await NewInvoice();
        await Send(new CapturePayment(_tenant, id, "key-1"));

        await Should.ThrowAsync<ConflictException>(() => Send(new CapturePayment(_tenant, id, "key-2")));
        _fakes.Charges.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Reusing_a_key_for_another_invoice_is_a_conflict()
    {
        var first = await NewInvoice();
        var second = await NewInvoice();
        await Send(new CapturePayment(_tenant, first, "key-1"));

        await Should.ThrowAsync<ConflictException>(() => Send(new CapturePayment(_tenant, second, "key-1")));
    }

    [Fact]
    public async Task Gateway_failure_rolls_back_and_leaves_the_invoice_open()
    {
        var id = await NewInvoice();
        _fakes.FailNextGatewayCall = true;

        await Should.ThrowAsync<HttpRequestException>(() => Send(new CapturePayment(_tenant, id, "key-1")));

        _fakes.Payments.ShouldBeEmpty();
        _fakes.Log[^1].ShouldBe("rollback");
    }

    [Fact]
    public async Task Other_tenants_cannot_capture_or_read_the_invoice()
    {
        var id = await NewInvoice();

        await Should.ThrowAsync<NotFoundException>(() => Send(new CapturePayment(Guid.NewGuid(), id, "key-x")));
        (await Send(new GetInvoice(Guid.NewGuid(), id))).ShouldBeNull();
    }

    [Fact]
    public async Task Refund_marks_the_invoice_refunded_and_is_idempotent()
    {
        var id = await NewInvoice();
        var capture = await Send(new CapturePayment(_tenant, id, "cap-1"));

        var refund = await Send(new RefundPayment(_tenant, capture.PaymentId, "ref-1"));
        var again = await Send(new RefundPayment(_tenant, capture.PaymentId, "ref-1"));

        refund.Kind.ShouldBe("refund");
        again.Replayed.ShouldBeTrue();
        (await Send(new GetInvoice(_tenant, id)))!.Status.ShouldBe("refunded");
        _fakes.Events.Select(e => e.Type).ShouldBe(["payment.captured", "payment.refunded"]);
    }

    [Fact]
    public async Task Refunding_twice_with_different_keys_is_a_conflict()
    {
        var id = await NewInvoice();
        var capture = await Send(new CapturePayment(_tenant, id, "cap-1"));
        await Send(new RefundPayment(_tenant, capture.PaymentId, "ref-1"));

        await Should.ThrowAsync<ConflictException>(() => Send(new RefundPayment(_tenant, capture.PaymentId, "ref-2")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Capture_requires_an_idempotency_key(string? key)
    {
        await Should.ThrowAsync<RequestValidationException>(() => Send(new CapturePayment(_tenant, Guid.NewGuid(), key)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(10_000, 200)]
    public async Task Payment_history_page_size_is_clamped(int requested, int expected)
    {
        await Send(new ListTenantPayments(_tenant, requested));

        _fakes.LastTake.ShouldBe(expected);
    }
}
