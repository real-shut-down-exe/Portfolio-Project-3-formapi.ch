using System.Threading.RateLimiting;

namespace YS.MailPost.Api.RateLimiting;

public sealed class CompositeRateLimiter : RateLimiter
{
    private readonly RateLimiter[] _limiters;

    public CompositeRateLimiter(IEnumerable<RateLimiter> limiters)
    {
        _limiters = limiters?.ToArray() ?? throw new ArgumentNullException(nameof(limiters));
        if (_limiters.Length == 0)
        {
            throw new ArgumentException("At least one limiter is required.", nameof(limiters));
        }
    }

    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        var leases = new RateLimitLease[_limiters.Length];

        for (var index = 0; index < _limiters.Length; index++)
        {
            var lease = _limiters[index].AttemptAcquire(permitCount);
            leases[index] = lease;

            if (lease.IsAcquired)
            {
                continue;
            }

            for (var releaseIndex = 0; releaseIndex <= index; releaseIndex++)
            {
                leases[releaseIndex].Dispose();
            }

            return lease;
        }

        return new CompositeRateLimitLease(leases);
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        => throw new NotSupportedException("Waiting is not supported for the composite rate limiter.");

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        foreach (var limiter in _limiters)
        {
            limiter.Dispose();
        }
    }

    private sealed class CompositeRateLimitLease : RateLimitLease
    {
        private readonly RateLimitLease[] _leases;

        public CompositeRateLimitLease(RateLimitLease[] leases)
        {
            _leases = leases;
        }

        public override bool IsAcquired => true;

        public override IEnumerable<string> MetadataNames
            => _leases.SelectMany(lease => lease.MetadataNames).Distinct();

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            foreach (var lease in _leases)
            {
                if (lease.TryGetMetadata(metadataName, out metadata))
                {
                    return true;
                }
            }

            metadata = null;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            foreach (var lease in _leases)
            {
                lease.Dispose();
            }
        }
    }
}
