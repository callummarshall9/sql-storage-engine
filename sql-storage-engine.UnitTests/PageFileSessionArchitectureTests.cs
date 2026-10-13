using AwesomeAssertions;
using sql_storage_engine.Brokers.PageFileSessions;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Foundations.PageFileSessions;

namespace sql_storage_engine.UnitTests;
public sealed class PageFileSessionArchitectureTests
{
    [Test]
    public async Task IndependentSessionsSupportCompletePagesReadOnlyOwnershipAndIdempotentClose()
    {
        var root = Path.Combine(Path.GetTempPath(), $"page-sessions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var service = new PageFileSessionService(new PageFileSessionBroker());
        var first = service.CreatePageFileSession(new PageFileSession { Path = Path.Combine(root, "first"), PageSize = 8192, CreateNew = true });
        var second = service.CreatePageFileSession(new PageFileSession { Path = Path.Combine(root, "second"), PageSize = 8192, CreateNew = true });
        try
        {
            var source = Enumerable.Repeat((byte)7, 8192).ToArray();
            await service.WritePageFileSessionAsync(first, 0, source, default);
            await service.FlushPageFileSessionAsync(first,default);
            await service.ClosePageFileSessionAsync(first);
            await service.ClosePageFileSessionAsync(first);
            var readOnly = service.CreatePageFileSession(first with { SessionId = Guid.Empty, CreateNew = false, ReadOnly = true });
            try
            {
                var read = new byte[8192];
                await service.ReadPageFileSessionAsync(readOnly,0,read,default);
                read.Should().Equal(source);
                await ((Func<Task>)(async () => await service.WritePageFileSessionAsync(readOnly, 0, source, default))).Should().ThrowAsync<InvalidOperationException>();
            }
            finally
            {
                await service.ClosePageFileSessionAsync(readOnly);
            }
            await service.WritePageFileSessionAsync(second, 1, source, default);
            service.RestorePageFileSessionLength(second,8192);
            new FileInfo(second.Path).Length.Should().Be(8192);
            await ((Func<Task>)(async () => await service.ReadPageFileSessionAsync(second,0,new byte[1],default))).Should().ThrowAsync<ArgumentException>();
            await ((Func<Task>)(async () => await service.ReadPageFileSessionAsync(second, ulong.MaxValue, source, default))).Should().ThrowAsync<ArgumentException>();
        }
        finally
        {
            await service.ClosePageFileSessionAsync(first);
            await service.ClosePageFileSessionAsync(second);
            Directory.Delete(root, true);
        }
    }
}