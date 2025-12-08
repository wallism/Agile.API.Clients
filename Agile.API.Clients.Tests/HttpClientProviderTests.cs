using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Agile.API.Clients.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Agile.API.Clients.Tests;

/// <summary>
/// Tests for <see cref="HttpClientProvider"/> - verifies thread-safe HttpClient management.
/// </summary>
[TestFixture]
public class HttpClientProviderTests
{
    private IHttpClientFactory _mockFactory = null!;
    private HttpClientProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _mockFactory = Substitute.For<IHttpClientFactory>();
        _mockFactory.CreateClient(Arg.Any<string>()).Returns(callInfo => new HttpClient());
        _provider = new HttpClientProvider(_mockFactory);
    }

    [Test]
    public void Constructor_WithNullFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpClientProvider(null!));
    }

    [Test]
    public void GetClient_WithValidName_ReturnsHttpClient()
    {
        var client = _provider.GetClient("TestClient");

        Assert.That(client, Is.Not.Null);
        Assert.That(client, Is.TypeOf<HttpClient>());
    }

    [Test]
    public void GetClient_CalledMultipleTimes_ReturnsSameInstance()
    {
        var client1 = _provider.GetClient("TestClient");
        var client2 = _provider.GetClient("TestClient");

        Assert.That(client1, Is.SameAs(client2));
        _mockFactory.Received(1).CreateClient("TestClient");
    }

    [Test]
    public void GetClient_DifferentNames_ReturnsDifferentInstances()
    {
        var client1 = _provider.GetClient("Client1");
        var client2 = _provider.GetClient("Client2");

        Assert.That(client1, Is.Not.SameAs(client2));
        _mockFactory.Received(1).CreateClient("Client1");
        _mockFactory.Received(1).CreateClient("Client2");
    }

    [Test]
    public void GetClient_WithNullName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _provider.GetClient(null!));
    }

    [Test]
    public void GetClient_WithEmptyName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _provider.GetClient(""));
    }

    [Test]
    public void GetClient_WithWhitespaceName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _provider.GetClient("   "));
    }

    [Test]
    public void GetClient_ConcurrentAccess_CreatesOnlyOneInstance()
    {
        const int threadCount = 100;
        var clients = new ConcurrentBag<HttpClient>();
        var barrier = new System.Threading.Barrier(threadCount);

        Parallel.For(0, threadCount, _ =>
        {
            barrier.SignalAndWait();
            var client = _provider.GetClient("ConcurrentClient");
            clients.Add(client);
        });

        // All threads should get the same instance
        var firstClient = clients.First();
        Assert.That(clients.All(c => ReferenceEquals(c, firstClient)), Is.True);
        
        // Factory should only be called once
        _mockFactory.Received(1).CreateClient("ConcurrentClient");
    }

    [Test]
    public void GetClient_MultipleNamesConcurrently_CreatesOneInstancePerName()
    {
        const int threadCount = 50;
        var clientNames = new[] { "ClientA", "ClientB", "ClientC" };
        var results = new ConcurrentDictionary<string, ConcurrentBag<HttpClient>>();
        
        foreach (var name in clientNames)
        {
            results[name] = new ConcurrentBag<HttpClient>();
        }

        Parallel.For(0, threadCount * clientNames.Length, i =>
        {
            var name = clientNames[i % clientNames.Length];
            var client = _provider.GetClient(name);
            results[name].Add(client);
        });

        // Each name should have one unique instance shared across all threads
        foreach (var name in clientNames)
        {
            var clientsForName = results[name];
            var firstClient = clientsForName.First();
            Assert.That(clientsForName.All(c => ReferenceEquals(c, firstClient)), Is.True,
                $"All clients for '{name}' should be the same instance");
        }

        // Factory should be called exactly once per unique name
        foreach (var name in clientNames)
        {
            _mockFactory.Received(1).CreateClient(name);
        }
    }
}
