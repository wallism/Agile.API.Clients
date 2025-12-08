using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Agile.API.Clients.CallHandling;
using NUnit.Framework;

namespace Agile.API.Clients.Tests;

/// <summary>
/// Tests for the CallResult class
/// </summary>
[TestFixture]
public class CallResultTests
{
    [Test]
    public async Task Wrap_WithSuccessResponse_ReturnsSuccessfulResult()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/test");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": 1, \"name\": \"Test\"}", System.Text.Encoding.UTF8, "application/json")
        };
            
        var result = await CallResult<TestObject>.Wrap(request, response, 100);
            
        Assert.That(result.WasSuccessful, Is.True);
        Assert.That(result.IsSuccessStatusCode, Is.True);
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(1));
        Assert.That(result.Value.Name, Is.EqualTo("Test"));
        Assert.That(result.Elapsed, Is.EqualTo(100));
        Assert.That(result.Exception, Is.Null);
    }

    [Test]
    public async Task Wrap_WithErrorStatusCode_ReturnsFailedResult()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/test");
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Resource not found", System.Text.Encoding.UTF8, "text/plain")
        };
            
        var result = await CallResult<TestObject>.Wrap(request, response, 50);
            
        Assert.That(result.WasSuccessful, Is.False);
        Assert.That(result.IsSuccessStatusCode, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(result.Exception, Is.Not.Null);
        Assert.That(result.RawText, Does.Contain("Resource not found"));
    }

    [Test]
    public async Task Wrap_WithInvalidJson_ReturnsFailedResultWithException()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/test");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("this is not valid json", System.Text.Encoding.UTF8, "application/json")
        };
            
        var result = await CallResult<TestObject>.Wrap(request, response, 75);
            
        Assert.That(result.WasSuccessful, Is.False);
        Assert.That(result.IsSuccessStatusCode, Is.True); // HTTP was successful
        Assert.That(result.Exception, Is.Not.Null);
        Assert.That(result.RawText, Is.EqualTo("this is not valid json"));
    }

    [Test]
    public async Task Wrap_WithTextContent_ReturnsStringResult()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/text");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Plain text response", System.Text.Encoding.UTF8, "text/plain")
        };
            
        var result = await CallResult<string>.Wrap(request, response, 25);
            
        Assert.That(result.WasSuccessful, Is.True);
        Assert.That(result.StringValue, Is.EqualTo("Plain text response"));
    }

    [Test]
    public async Task Wrap_WithStringTypeAndJsonContent_ReturnsStringWithoutDeserialization()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/json");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"key\": \"value\"}", System.Text.Encoding.UTF8, "application/json")
        };
            
        var result = await CallResult<string>.Wrap(request, response, 30);
            
        Assert.That(result.WasSuccessful, Is.True);
        Assert.That(result.StringValue, Is.EqualTo("{\"key\": \"value\"}"));
    }

    [Test]
    public async Task Wrap_WithNullContentType_ReturnsSuccessWithNullValue()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/nocontent");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("")
        };
        response.Content.Headers.ContentType = null;
            
        var result = await CallResult<TestObject>.Wrap(request, response, 10);
            
        Assert.That(result.WasSuccessful, Is.True);
        Assert.That(result.Value, Is.Null);
    }

    [Test]
    public async Task Wrap_WithUnsupportedContentType_ReturnsFailedResult()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/binary");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("binary data", System.Text.Encoding.UTF8, "application/octet-stream")
        };
            
        var result = await CallResult<TestObject>.Wrap(request, response, 40);
            
        Assert.That(result.WasSuccessful, Is.False);
        Assert.That(result.Exception, Is.Not.Null);
        Assert.That(result.Exception!.Message, Does.Contain("unsupported ContentType"));
    }

    [Test]
    public void BuildException_WithExceptionAndRequest_CreatesFailedResult()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/error");
        var exception = new HttpRequestException("Connection refused");
            
        var result = CallResult<TestObject>.BuildException(exception, request, 5000);
            
        Assert.That(result.WasSuccessful, Is.False);
        Assert.That(result.Exception, Is.SameAs(exception));
        Assert.That(result.RawText, Is.EqualTo("no response"));
        Assert.That(result.AbsoluteUri, Does.Contain("example.com"));
        Assert.That(result.Elapsed, Is.EqualTo(5000));
    }

    [Test]
    public void BuildException_WithExceptionAndRaw_CreatesFailedResult()
    {
        var exception = new TimeoutException("Request timed out");
            
        var result = CallResult<TestObject>.BuildException(exception, "timeout error");
            
        Assert.That(result.WasSuccessful, Is.False);
        Assert.That(result.Exception, Is.SameAs(exception));
        Assert.That(result.RawText, Is.EqualTo("timeout error"));
        Assert.That(result.AbsoluteUri, Is.EqualTo("nocallmade"));
    }

    [Test]
    public async Task Wrap_PreservesElapsedTime()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/test");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
            
        var result = await CallResult<TestObject>.Wrap(request, response, 12345);
            
        Assert.That(result.Elapsed, Is.EqualTo(12345));
    }

    [Test]
    public async Task Wrap_With500Error_ReturnsServerError()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/api/create");
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal server error occurred", System.Text.Encoding.UTF8, "text/plain")
        };
            
        var result = await CallResult<TestObject>.Wrap(request, response, 200);
            
        Assert.That(result.WasSuccessful, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(result.Exception!.Message, Does.Contain("InternalServerError"));
    }

    [Test]
    public async Task Wrap_WithEmptyJsonArray_ReturnsEmptyList()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/items");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
        };
            
        var result = await CallResult<List<TestObject>>.Wrap(request, response, 15);
            
        Assert.That(result.WasSuccessful, Is.True);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task Wrap_WithJsonArray_ReturnsPopulatedList()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/items");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"id\":1,\"name\":\"Item1\"},{\"id\":2,\"name\":\"Item2\"}]", System.Text.Encoding.UTF8, "application/json")
        };
            
        var result = await CallResult<List<TestObject>>.Wrap(request, response, 20);
            
        Assert.That(result.WasSuccessful, Is.True);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count, Is.EqualTo(2));
        Assert.That(result.Value[0].Name, Is.EqualTo("Item1"));
        Assert.That(result.Value[1].Name, Is.EqualTo("Item2"));
    }

    private class TestObject
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}