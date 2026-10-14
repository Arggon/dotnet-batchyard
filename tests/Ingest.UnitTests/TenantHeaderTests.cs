using BuildingBlocks;
using Microsoft.AspNetCore.Http;

namespace Ingest.UnitTests;

public class TenantHeaderTests
{
    [Fact]
    public void Valid_guid_header_is_read()
    {
        var id = Guid.NewGuid();
        var request = RequestWith(id.ToString());

        TenantHeader.TryGet(request, out var tenantId).ShouldBeTrue();
        tenantId.ShouldBe(id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Missing_or_invalid_header_is_refused(string? value)
    {
        TenantHeader.TryGet(RequestWith(value), out _).ShouldBeFalse();
    }

    private static HttpRequest RequestWith(string? tenant)
    {
        var context = new DefaultHttpContext();
        if (tenant is not null)
        {
            context.Request.Headers[TenantHeader.Name] = tenant;
        }

        return context.Request;
    }
}
