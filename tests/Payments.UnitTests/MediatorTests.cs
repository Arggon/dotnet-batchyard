using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Domain;
using Payments.Application.Invoices;
using Payments.Application.Mediator;

namespace Payments.UnitTests;

public class MediatorTests
{
    private readonly Fakes _fakes = new();

    private async Task<T> Send<T>(IRequest<T> request)
    {
        await using var provider = _fakes.BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    [Fact]
    public async Task Command_runs_inside_one_committed_transaction()
    {
        await Send(new CreateInvoice(Guid.NewGuid(), 10m, "USD", "PO-1"));

        _fakes.Log.ShouldBe(["begin", "commit"]);
    }

    [Fact]
    public async Task Failing_command_is_rolled_back()
    {
        await Should.ThrowAsync<NotFoundException>(() => Send(new CapturePayment(Guid.NewGuid(), Guid.NewGuid(), "k1")));

        _fakes.Log.ShouldBe(["begin", "rollback"]);
    }

    [Fact]
    public async Task Invalid_command_never_reaches_the_handler_or_the_database()
    {
        var error = await Should.ThrowAsync<RequestValidationException>(() => Send(new CreateInvoice(Guid.NewGuid(), -5m, "usd", "")));

        error.Errors.ShouldBe(
        [
            "amount must be greater than zero",
            "currency must be a 3-letter upper-case ISO 4217 code",
            "reference must be 1-64 characters",
        ]);
        _fakes.Log.ShouldBeEmpty();
        _fakes.Invoices.ShouldBeEmpty();
    }

    [Fact]
    public async Task Queries_do_not_open_a_transaction()
    {
        (await Send(new GetInvoice(Guid.NewGuid(), Guid.NewGuid()))).ShouldBeNull();

        _fakes.Log.ShouldBeEmpty();
    }
}
