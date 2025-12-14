using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agile.API.Clients.RateLimiting;
using NUnit.Framework;

namespace Agile.API.Clients.Tests;

/// <summary>
/// Tests specifically for the new ApiRateLimiter class
/// </summary>
[TestFixture]
public class ApiRateLimiterTests
{
    [Test]
    public void Constructor_WithValidRateLimit_CreatesInstance()
    {
        var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(1));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        Assert.That(limiter.Occurrences, Is.EqualTo(10));
        Assert.That(limiter.TimeUnit, Is.EqualTo(TimeSpan.FromSeconds(1)));
        Assert.That(limiter.TimeUnitMilliseconds, Is.EqualTo(1000));
    }

    [Test]
    public void Constructor_WithNullRateLimit_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ApiRateLimiter(null!));
    }

    [Test]
    public void Constructor_WithZeroOccurrences_ThrowsArgumentOutOfRangeException()
    {
        var rateLimit = RateLimit.Build(0, TimeSpan.FromSeconds(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApiRateLimiter(rateLimit));
    }

    [Test]
    public void Constructor_WithNegativeTimeUnit_ThrowsArgumentOutOfRangeException()
    {
        var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApiRateLimiter(rateLimit));
    }

    [Test]
    public void WaitToProceed_WithinLimit_ReturnsImmediately()
    {
        var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(1));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < 5; i++)
        {
            limiter.WaitToProceed();
        }
        timer.Stop();
            
        // Should complete very quickly since we're within the limit
        Assert.That(timer.ElapsedMilliseconds, Is.LessThan(500));
    }

    [Test]
    public async Task WaitToProceedAsync_WithinLimit_ReturnsImmediately()
    {
        var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(1));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < 5; i++)
        {
            await limiter.WaitToProceedAsync();
        }
        timer.Stop();
            
        // Should complete very quickly since we're within the limit
        Assert.That(timer.ElapsedMilliseconds, Is.LessThan(500));
    }

    [Test]
    public void WaitToProceed_ExceedsLimit_Waits()
    {
        var rateLimit = RateLimit.Build(2, TimeSpan.FromSeconds(1));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        var timer = Stopwatch.StartNew();
            
        // First two should be immediate
        limiter.WaitToProceed();
        limiter.WaitToProceed();
            
        // Third should wait
        limiter.WaitToProceed();
            
        timer.Stop();
            
        // Should have waited approximately 1 second for window to slide
        Assert.That(timer.ElapsedMilliseconds, Is.GreaterThan(500));
    }

    [Test]
    public void WaitToProceed_WithTimeout_ReturnsOnTimeout()
    {
        var rateLimit = RateLimit.Build(1, TimeSpan.FromSeconds(10));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        // Exhaust the permit
        limiter.WaitToProceed();
            
        var timer = Stopwatch.StartNew();
        var result = limiter.WaitToProceed(100);
        timer.Stop();
            
        // Should timeout quickly
        Assert.That(result, Is.False);
        Assert.That(timer.ElapsedMilliseconds, Is.LessThan(500));
    }

    [Test]
    public void GetAvailablePermits_ReturnsCorrectCount()
    {
        var rateLimit = RateLimit.Build(5, TimeSpan.FromSeconds(10));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        var initial = limiter.GetAvailablePermits();
        Assert.That(initial, Is.EqualTo(5));
            
        limiter.WaitToProceed();
        var afterOne = limiter.GetAvailablePermits();
        Assert.That(afterOne, Is.EqualTo(4));
    }

    [Test]
    public void Dispose_DisposesLimiter()
    {
        var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(1));
        var limiter = new ApiRateLimiter(rateLimit);
            
        limiter.Dispose();
            
        Assert.Throws<ObjectDisposedException>(() => limiter.WaitToProceed());
    }

    [Test]
    public async Task DisposeAsync_DisposesLimiter()
    {
        var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(1));
        var limiter = new ApiRateLimiter(rateLimit);
            
        await limiter.DisposeAsync();
            
        Assert.Throws<ObjectDisposedException>(() => limiter.WaitToProceed());
    }

    [Test]
    public void NotifyPriorityCallMade_DoesNotBlock()
    {
        var rateLimit = RateLimit.Build(1, TimeSpan.FromSeconds(10));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        // Exhaust the permit
        limiter.WaitToProceed();
            
        var timer = Stopwatch.StartNew();
            
        // Priority call should not block even when permits exhausted
        limiter.NotifyPriorityCallMade();
            
        timer.Stop();
            
        // Should complete immediately
        Assert.That(timer.ElapsedMilliseconds, Is.LessThan(100));
    }

    [Test]
    public void RateLimit_CreateRateLimiter_ReturnsValidLimiter()
    {
        var rateLimit = RateLimit.Build(5, TimeSpan.FromSeconds(2));
        using var limiter = rateLimit.CreateRateLimiter();
            
        Assert.That(limiter, Is.Not.Null);
        Assert.That(limiter.Occurrences, Is.EqualTo(5));
        Assert.That(limiter.TimeUnit, Is.EqualTo(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void EnumerableExtensions_LimitRate_RateLimitsEnumeration()
    {
        var rateLimit = RateLimit.Build(2, TimeSpan.FromSeconds(1));
        var items = Enumerable.Range(1, 4);
            
        var timer = Stopwatch.StartNew();
        var results = items.LimitRate(rateLimit).ToList();
        timer.Stop();
            
        Assert.That(results.Count, Is.EqualTo(4));
        // Should have taken at least 1 second to process 4 items at 2/second
        Assert.That(timer.ElapsedMilliseconds, Is.GreaterThan(500));
    }

    [Test]
    public async Task WaitToProceedAsync_WithCancellation_ThrowsOperationCancelledException()
    {
        var rateLimit = RateLimit.Build(1, TimeSpan.FromSeconds(10));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        // Exhaust the permit
        await limiter.WaitToProceedAsync();
            
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately
            
        // TaskCanceledException inherits from OperationCanceledException
        Assert.ThrowsAsync<TaskCanceledException>(async () => 
            await limiter.WaitToProceedAsync(cts.Token));
    }

    [Test]
    public async Task WaitToProceedAsync_WithTimeout_ReturnsFalseOnTimeout()
    {
        var rateLimit = RateLimit.Build(1, TimeSpan.FromSeconds(10));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        // Exhaust the permit
        await limiter.WaitToProceedAsync();
            
        var timer = Stopwatch.StartNew();
        var result = await limiter.WaitToProceedAsync(TimeSpan.FromMilliseconds(100));
        timer.Stop();
            
        Assert.That(result, Is.False);
        Assert.That(timer.ElapsedMilliseconds, Is.LessThan(500));
    }

    [Test]
    public async Task MultipleThreads_RateLimitIsRespected()
    {
        var rateLimit = RateLimit.Build(3, TimeSpan.FromSeconds(1));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        var timer = Stopwatch.StartNew();
        var completedCount = 0;
            
        // Launch 6 concurrent tasks that each need to acquire a permit
        var tasks = Enumerable.Range(1, 6).Select(async i =>
        {
            await limiter.WaitToProceedAsync();
            Interlocked.Increment(ref completedCount);
            return i;
        }).ToArray();
            
        await Task.WhenAll(tasks);
        timer.Stop();
            
        Assert.That(completedCount, Is.EqualTo(6));
        // 6 operations at 3/second should take at least 1 second
        Assert.That(timer.ElapsedMilliseconds, Is.GreaterThan(500));
    }

    [Test]
    public void WaitToProceed_WithNegativeTimeout_ThrowsArgumentOutOfRangeException()
    {
        var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(1));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        Assert.Throws<ArgumentOutOfRangeException>(() => limiter.WaitToProceed(-2));
    }

    [Test]
    public void GetAvailablePermits_AfterDisposal_ThrowsObjectDisposedException()
    {
        var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(1));
        var limiter = new ApiRateLimiter(rateLimit);
        limiter.Dispose();
            
        Assert.Throws<ObjectDisposedException>(() => limiter.GetAvailablePermits());
    }

    [Test]
    public void NotifyPriorityCallMade_AfterDisposal_ThrowsObjectDisposedException()
    {
        var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(1));
        var limiter = new ApiRateLimiter(rateLimit);
        limiter.Dispose();
            
        Assert.Throws<ObjectDisposedException>(() => limiter.NotifyPriorityCallMade());
    }

    [Test]
    public void Constructor_WithVerySmallTimeUnit_CreatesInstance()
    {
        var rateLimit = RateLimit.Build(100, TimeSpan.FromMilliseconds(100));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        Assert.That(limiter.Occurrences, Is.EqualTo(100));
        Assert.That(limiter.TimeUnitMilliseconds, Is.EqualTo(100));
    }

    [Test]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(1));
        var limiter = new ApiRateLimiter(rateLimit);
            
        // Should not throw when called multiple times
        Assert.DoesNotThrow(() =>
        {
            limiter.Dispose();
            limiter.Dispose();
            limiter.Dispose();
        });
    }

    [Test]
    public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
    {
        var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(1));
        var limiter = new ApiRateLimiter(rateLimit);
            
        // Should not throw when called multiple times
        await limiter.DisposeAsync();
        await limiter.DisposeAsync();
        await limiter.DisposeAsync();
    }

    [Test]
    public void EnumerableExtensions_LimitRate_WithSharedLimiter_RateLimitsCorrectly()
    {
        var rateLimit = RateLimit.Build(3, TimeSpan.FromSeconds(1));
        using var limiter = new ApiRateLimiter(rateLimit);
            
        var items = Enumerable.Range(1, 3);
            
        var timer = Stopwatch.StartNew();
        var results = items.LimitRate(limiter).ToList();
        timer.Stop();
            
        Assert.That(results.Count, Is.EqualTo(3));
        // First 3 should be immediate since within limit
        Assert.That(timer.ElapsedMilliseconds, Is.LessThan(500));
    }
}