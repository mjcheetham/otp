using System.Collections;

namespace Mjcheetham.Otp.Storage;

public interface IOtpStore
{
    IAsyncEnumerable<IOneTimePassword> ListAsync(CancellationToken ct = default);

    ValueTask<IOneTimePassword?> GetAsync(string name, CancellationToken ct = default);

    Task AddAsync(IOneTimePassword otp, CancellationToken ct = default);

    Task<bool> RemoveAsync(string name, CancellationToken ct = default);
}

/// <summary>
/// A simple in-memory store for one-time passwords, backed by a simple list.
/// </summary>
public class InMemoryOtpStore : IOtpStore, IEnumerable<IOneTimePassword>
{
    private readonly List<IOneTimePassword> _otps = new();

    public void Add(IOneTimePassword otp)
    {
        if (_otps.Any(o => string.Equals(o.Name, otp.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A one-time password named '{otp.Name}' already exists.");
        }

        _otps.Add(otp);
    }

    public IAsyncEnumerable<IOneTimePassword> ListAsync(CancellationToken ct = default)
    {
        return _otps.ToAsyncEnumerable();
    }

    public ValueTask<IOneTimePassword?> GetAsync(string name, CancellationToken ct = default)
    {
        IOneTimePassword? match = _otps.Find(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
        return ValueTask.FromResult(match);
    }

    public Task AddAsync(IOneTimePassword otp, CancellationToken ct = default)
    {
        Add(otp);
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string name, CancellationToken ct = default)
    {
        int removed = _otps.RemoveAll(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(removed > 0);
    }

    public IEnumerator<IOneTimePassword> GetEnumerator() => _otps.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_otps).GetEnumerator();
}
