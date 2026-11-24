using Microsoft.Extensions.Logging.Abstractions;

namespace DocIntel.Worker.Tests;

public class IngestionMessageHandlerTests
{
    private readonly IngestionMessageHandler _handler = new(NullLogger<IngestionMessageHandler>.Instance);

    [Fact]
    public void Reads_a_valid_job()
    {
        var id = Guid.NewGuid();

        var job = _handler.Handle($$"""{"documentId":"{{id}}","version":"0.1"}""");

        Assert.Equal(new IngestionJob(id, "0.1"), job);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"documentId":"00000000-0000-0000-0000-000000000000","version":"0.1"}""")]
    [InlineData("""{"documentId":"6f1c2a9e-1b7d-4c55-9a0e-2a7d3b9c1e11","version":" "}""")]
    public void Rejects_a_bad_message_so_it_is_retried_then_dead_lettered(string body)
    {
        Assert.Throws<InvalidDataException>(() => _handler.Handle(body));
    }
}
