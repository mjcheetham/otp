using System.Collections;

namespace Mjcheetham.Otp;

public interface IOtpStore
{
    IAsyncEnumerable<IOneTimePassword> ListAsync(CancellationToken ct = default);
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

    public IEnumerator<IOneTimePassword> GetEnumerator() => _otps.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_otps).GetEnumerator();
}
