using NetArchTest.Rules;

namespace Architecture.Tests;

public class LayerTests
{
    [Fact]
    public void Data_layer_does_not_depend_on_the_api()
    {
        var result = Types.InAssembly(typeof(Ingest.Data.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Ingest.Api", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Access_data_layer_does_not_depend_on_aspnetcore()
    {
        var result = Types.InAssembly(typeof(Access.Data.AccessService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Access.Api", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Payments_application_knows_nothing_about_sql_http_or_messaging()
    {
        var result = Types.InAssembly(typeof(Payments.Application.Mediator.Mediator).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Payments.Data", "Payments.Api", "Microsoft.Data.SqlClient", "Dapper", "Microsoft.AspNetCore", "RabbitMQ")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
