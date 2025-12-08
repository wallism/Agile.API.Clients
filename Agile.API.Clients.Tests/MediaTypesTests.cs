using Agile.API.Clients.Helpers;
using NUnit.Framework;

namespace Agile.API.Clients.Tests;

/// <summary>
/// Tests for the MediaTypes class
/// </summary>
[TestFixture]
public class MediaTypesTests
{
    [Test]
    public void JSON_HasCorrectMediaType()
    {
        Assert.That(MediaTypes.JSON.MediaType, Is.EqualTo("application/json"));
    }

    [Test]
    public void TEXT_HasCorrectMediaType()
    {
        Assert.That(MediaTypes.TEXT.MediaType, Is.EqualTo("text/plain"));
    }

    [Test]
    public void XML_HasCorrectMediaType()
    {
        Assert.That(MediaTypes.XML.MediaType, Is.EqualTo("application/xml"));
    }

    [Test]
    public void HTML_HasCorrectMediaType()
    {
        Assert.That(MediaTypes.HTML.MediaType, Is.EqualTo("text/html"));
    }

    [Test]
    public void JPEG_HasCorrectMediaType()
    {
        Assert.That(MediaTypes.JPEG.MediaType, Is.EqualTo("image/jpeg"));
    }

    [Test]
    public void FormUrlEncoded_HasCorrectMediaType()
    {
        Assert.That(MediaTypes.FormUrlEncoded.MediaType, Is.EqualTo("application/x-www-form-urlencoded"));
    }
}