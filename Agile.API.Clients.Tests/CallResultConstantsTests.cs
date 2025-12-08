using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Agile.API.Clients.CallHandling;
using NUnit.Framework;

namespace Agile.API.Clients.Tests;

/// <summary>
/// Additional tests for <see cref="CallResult{T}"/> - verifies constants and null handling.
/// </summary>
[TestFixture]
public class CallResultConstantsTests
{
    [Test]
    public void NoCallMadeUri_HasExpectedValue()
    {
        Assert.That(CallResult<string>.NoCallMadeUri, Is.EqualTo("nocallmade"));
    }

    [Test]
    public void UnknownUri_HasExpectedValue()
    {
        Assert.That(CallResult<string>.UnknownUri, Is.EqualTo("unknown"));
    }

    [Test]
    public void BuildException_WithRawString_SetsNoCallMadeUri()
    {
        var exception = new InvalidOperationException("Test error");
        
        var result = CallResult<string>.BuildException(exception, "raw response");
        
        Assert.That(result.AbsoluteUri, Is.EqualTo(CallResult<string>.NoCallMadeUri));
        Assert.That(result.Exception, Is.SameAs(exception));
        Assert.That(result.RawText, Is.EqualTo("raw response"));
        Assert.That(result.WasSuccessful, Is.False);
    }

    [Test]
    public void BuildException_WithRequest_SetsElapsedTime()
    {
        var exception = new HttpRequestException("Network error");
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");
        
        var result = CallResult<string>.BuildException(exception, request, elapsedMilliseconds: 150);
        
        Assert.That(result.Elapsed, Is.EqualTo(150));
        Assert.That(result.AbsoluteUri, Is.EqualTo("https://api.example.com/test"));
        Assert.That(result.Exception, Is.SameAs(exception));
    }

    [Test]
    public void BuildException_WithNullRequestUri_SetsUnknownUri()
    {
        var exception = new InvalidOperationException("Test");
        var request = new HttpRequestMessage();
        // RequestUri is null by default
        
        var result = CallResult<string>.BuildException(exception, request, elapsedMilliseconds: 100);
        
        Assert.That(result.AbsoluteUri, Is.EqualTo(CallResult<string>.UnknownUri));
    }

    [Test]
    public async Task Wrap_WithSuccessResponse_SetsCorrectUri()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/resource/123");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": 1}", System.Text.Encoding.UTF8, "application/json")
        };
        
        var result = await CallResult<TestDto>.Wrap(request, response, elapsedMilliseconds: 50);
        
        Assert.That(result.AbsoluteUri, Is.EqualTo("https://api.example.com/resource/123"));
        Assert.That(result.WasSuccessful, Is.True);
        Assert.That(result.Elapsed, Is.EqualTo(50));
    }

    [Test]
    public async Task Wrap_WithErrorResponse_CapturesStatusCode()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/notfound");
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Resource not found")
        };
        
        var result = await CallResult<TestDto>.Wrap(request, response, elapsedMilliseconds: 75);
        
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(result.WasSuccessful, Is.False);
        Assert.That(result.Exception, Is.Not.Null);
        Assert.That(result.RawText, Does.Contain("Resource not found"));
    }

    [Test]
    public async Task Wrap_WithNullRequestUri_UsesUnknownUri()
    {
        var request = new HttpRequestMessage(); // No URI set
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        
        var result = await CallResult<TestDto>.Wrap(request, response, elapsedMilliseconds: 25);
        
        Assert.That(result.AbsoluteUri, Is.EqualTo(CallResult<TestDto>.UnknownUri));
    }

    private class TestDto
    {
        public int Id { get; set; }
    }
}
