using System;
using Agile.API.Clients.RateLimiting;
using NUnit.Framework;

namespace Agile.API.Clients.Tests;

/// <summary>
/// Tests for the RateLimit class
/// </summary>
[TestFixture]
public class RateLimitTests
{
    [Test]
    public void Build_WithValidParameters_CreatesRateLimit()
    {
        var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(5));
            
        Assert.That(rateLimit.Occurrences, Is.EqualTo(10));
        Assert.That(rateLimit.TimeUnit, Is.EqualTo(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public void HasLimit_WithPositiveOccurrences_ReturnsTrue()
    {
        var rateLimit = RateLimit.Build(1, TimeSpan.FromSeconds(1));
            
        Assert.That(rateLimit.HasLimit, Is.True);
    }

    [Test]
    public void HasLimit_WithZeroOccurrences_ReturnsFalse()
    {
        var rateLimit = RateLimit.Build(0, TimeSpan.FromSeconds(1));
            
        Assert.That(rateLimit.HasLimit, Is.False);
    }

    [Test]
    public void CreateRateLimiter_ReturnsConfiguredLimiter()
    {
        var rateLimit = RateLimit.Build(15, TimeSpan.FromMinutes(1));
        using var limiter = rateLimit.CreateRateLimiter();
            
        Assert.That(limiter.Occurrences, Is.EqualTo(15));
        Assert.That(limiter.TimeUnit, Is.EqualTo(TimeSpan.FromMinutes(1)));
    }
}