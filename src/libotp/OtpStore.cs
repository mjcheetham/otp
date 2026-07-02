using System.Collections;

namespace Mjcheetham.Otp;

public interface IOtpStore
{
    IAsyncEnumerable<IOneTimePassword> ListAsync(CancellationToken ct = default);

    ValueTask<IOneTimePassword?> GetAsync(string name, CancellationToken ct = default);
}

public class InMemoryOtpStore : IOtpStore, IEnumerable<IOneTimePassword>
{
    private readonly List<IOneTimePassword> _otps = new();

    public void Add(IOneTimePassword otp)
    {
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

    public IEnumerator<IOneTimePassword> GetEnumerator() => _otps.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_otps).GetEnumerator();
}
