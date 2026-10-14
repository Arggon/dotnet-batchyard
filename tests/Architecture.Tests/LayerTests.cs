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
}
