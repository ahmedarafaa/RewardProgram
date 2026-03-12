using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Tests.TestHelpers;

public class FakeTransaction : ITransaction
{
    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Committed = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        RolledBack = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
