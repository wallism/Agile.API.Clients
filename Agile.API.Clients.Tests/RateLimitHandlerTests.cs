using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Agile.API.Clients.Infrastructure;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;

namespace Agile.API.Clients.Tests;

/// <summary>
/// Tests for <see cref="RateLimitHandler"/> - verifies async rate limiting behavior.
/// </summary>
[TestFixture]
public class RateLimitHandlerTests
{
    [Test]
    public void Constructor_WithConfiguration_ReadsRateLimitSettings()
    {
        var config = CreateConfigWithRateLimit("TestApi", "5", "2");
        
        using var handler = new RateLimitHandler(config, "TestApi");
        
        Assert.That(handler.IsEnabled, Is.True);
        Assert.That(handler.Occurrences, Is.EqualTo(5));
        Assert.That(handler.TimeUnitSeconds, Is.EqualTo(2));
    }

    [Test]
    public void Constructor_WithMissingConfiguration_UsesDefaults()
    {
        var config = new ConfigurationBuilder().Build();
        
        using var handler = new RateLimitHandler(config, "NonExistentApi");
        
        Assert.That(handler.IsEnabled, Is.True);
        Assert.That(handler.Occurrences, Is.EqualTo(RateLimitHandler.DefaultOccurrences));
        Assert.That(handler.TimeUnitSeconds, Is.EqualTo(RateLimitHandler.DefaultSeconds));
    }

    [Test]
    public void Constructor_WithInvalidOccurrences_UsesDefault()
    {
        var config = CreateConfigWithRateLimit("TestApi", "invalid", "1");
        
        using var handler = new RateLimitHandler(config, "TestApi");
        
        Assert.That(handler.Occurrences, Is.EqualTo(RateLimitHandler.DefaultOccurrences));
    }

    [Test]
    public void Constructor_WithNegativeOccurrences_UsesDefault()
    {
        var config = CreateConfigWithRateLimit("TestApi", "-5", "1");
        
        using var handler = new RateLimitHandler(config, "TestApi");
        
        Assert.That(handler.Occurrences, Is.EqualTo(RateLimitHandler.DefaultOccurrences));
    }

    [Test]
    public void Constructor_WithZeroOccurrences_UsesDefault()
    {
        var config = CreateConfigWithRateLimit("TestApi", "0", "1");
        
        using var handler = new RateLimitHandler(config, "TestApi");
        
        Assert.That(handler.Occurrences, Is.EqualTo(RateLimitHandler.DefaultOccurrences));
    }

    [Test]
    public void Constructor_WithExplicitValues_SetsCorrectly()
    {
        using var handler = new RateLimitHandler(occurrences: 15, timeUnitSeconds: 3);
        
        Assert.That(handler.IsEnabled, Is.True);
        Assert.That(handler.Occurrences, Is.EqualTo(15));
        Assert.That(handler.TimeUnitSeconds, Is.EqualTo(3));
    }

    [Test]
    public void Constructor_WithZeroExplicitOccurrences_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimitHandler(0, 1));
    }

    [Test]
    public void Constructor_WithNegativeExplicitOccurrences_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimitHandler(-1, 1));
    }

    [Test]
    public void Constructor_WithZeroTimeUnit_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimitHandler(10, 0));
    }

    [Test]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RateLimitHandler(null!, "TestApi"));
    }

    [Test]
    public void Constructor_WithNullApiId_ThrowsArgumentNullException()
    {
        var config = new ConfigurationBuilder().Build();
        Assert.Throws<ArgumentNullException>(() => new RateLimitHandler(config, null!));
    }

    [Test]
    public void Constructor_WithEmptyApiId_ThrowsArgumentException()
    {
        var config = new ConfigurationBuilder().Build();
        Assert.Throws<ArgumentException>(() => new RateLimitHandler(config, ""));
    }

    [Test]
    public void CreateDisabled_ReturnsDisabledHandler()
    {
        using var handler = RateLimitHandler.CreateDisabled();
        
        Assert.That(handler.IsEnabled, Is.False);
        Assert.That(handler.Occurrences, Is.EqualTo(0));
        Assert.That(handler.TimeUnitSeconds, Is.EqualTo(0));
    }

    [Test]
    public async Task WaitToProceedAsync_WhenDisabled_ReturnsImmediately()
    {
        using var handler = RateLimitHandler.CreateDisabled();
        var timer = Stopwatch.StartNew();
        
        for (int i = 0; i < 100; i++)
        {
            await handler.WaitToProceedAsync();
        }
        
        timer.Stop();
        Assert.That(timer.ElapsedMilliseconds, Is.LessThan(100));
    }

    [Test]
    public async Task WaitToProceedAsync_WithinLimit_ReturnsQuickly()
    {
        using var handler = new RateLimitHandler(occurrences: 10, timeUnitSeconds: 1);
        var timer = Stopwatch.StartNew();
        
        for (int i = 0; i < 5; i++)
        {
            await handler.WaitToProceedAsync();
        }
        
        timer.Stop();
        Assert.That(timer.ElapsedMilliseconds, Is.LessThan(500));
    }

    [Test]
    public async Task WaitToProceedAsync_WithCancellation_ThrowsTaskCanceledException()
    {
        using var handler = new RateLimitHandler(occurrences: 1, timeUnitSeconds: 10);
        
        // Exhaust the limit
        await handler.WaitToProceedAsync();
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        
        // TaskCanceledException is a subclass of OperationCanceledException
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await handler.WaitToProceedAsync(cts.Token);
        });
    }

    [Test]
    public void NotifyPriorityCall_WhenDisabled_DoesNotThrow()
    {
        using var handler = RateLimitHandler.CreateDisabled();
        
        Assert.DoesNotThrow(() => handler.NotifyPriorityCall());
    }

    [Test]
    public void NotifyPriorityCall_WhenEnabled_DoesNotBlock()
    {
        using var handler = new RateLimitHandler(occurrences: 1, timeUnitSeconds: 10);
        var timer = Stopwatch.StartNew();
        
        // Even if we call many times, priority calls should not block
        for (int i = 0; i < 100; i++)
        {
            handler.NotifyPriorityCall();
        }
        
        timer.Stop();
        Assert.That(timer.ElapsedMilliseconds, Is.LessThan(500));
    }

    [Test]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var handler = new RateLimitHandler(occurrences: 10, timeUnitSeconds: 1);
        
        Assert.DoesNotThrow(() =>
        {
            handler.Dispose();
            handler.Dispose();
        });
    }

    [Test]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        var handler = new RateLimitHandler(occurrences: 10, timeUnitSeconds: 1);
        
        await handler.DisposeAsync();
        await handler.DisposeAsync();
        
        // Should not throw
        Assert.Pass();
    }

    [Test]
    public void WaitToProceedAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var handler = new RateLimitHandler(occurrences: 10, timeUnitSeconds: 1);
        handler.Dispose();
        
        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await handler.WaitToProceedAsync();
        });
    }

    [Test]
    public void NotifyPriorityCall_AfterDispose_ThrowsObjectDisposedException()
    {
        var handler = new RateLimitHandler(occurrences: 10, timeUnitSeconds: 1);
        handler.Dispose();
        
        Assert.Throws<ObjectDisposedException>(() => handler.NotifyPriorityCall());
    }

    [Test]
    public async Task WaitToProceedAsync_DisabledHandler_AfterDispose_DoesNotThrow()
    {
        var handler = RateLimitHandler.CreateDisabled();
        handler.Dispose();
        
        // Disabled handlers have no underlying limiter, so this should just return
        await handler.WaitToProceedAsync();
        Assert.Pass();
    }

    private static IConfiguration CreateConfigWithRateLimit(string apiId, string occurrences, string seconds)
    {
        var configData = new Dictionary<string, string?>
        {
            { $"APIS:{apiId}:RateLimit:Occurrences", occurrences },
            { $"APIS:{apiId}:RateLimit:Seconds", seconds }
        };
        
        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }
}
