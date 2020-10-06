using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agile.API.Clients;
using Agile.API.Clients.CallHandling;
using Agile.API.Clients.Tests.Mocks;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;
using PennedObjects.RateLimiting;

namespace Agile.API.Client.Tests
{
    /// <summary>
    /// Note for these tests the service isn't actually running so an error
    /// result is expected each time.
    /// </summary>
    [TestFixture]
    public class RateGateTests
    {
        [OneTimeSetUp]
        public void FixtureSetup()
        {
        }

        [Test]
        public async Task CallReturns_404_ErrorResult()
        {
            // get api with Rate limit of 3/second
            var config = Substitute.For<IConfiguration>();
            config["APIS:MOCK:RateLimit:Occurrences"].Returns(info => "3");
            config["APIS:MOCK:RateLimit:Seconds"].Returns(info => "1");
            var api = new WidgetApi(config);
            
            var result = await api.GetWidget(1);
            Assert.IsTrue(result is CallResult<Widget>);
            Assert.IsFalse(result.WasSuccessful);
        }

        [Test]
        public async Task RateLimitOf_1Per_Seconds_ThirdCallOccursAfter_15_Seconds()
        {
            // get api with Rate limit of 1/second
            var config = Substitute.For<IConfiguration>();
            config["APIS:MOCK:RateLimit:Occurrences"].Returns(info => "1");
            config["APIS:MOCK:RateLimit:Seconds"].Returns(info => "1");
            var api = new WidgetApi(config);

            var timer = Stopwatch.StartNew();
            // TODO also test running all on different threads
            Console.WriteLine($"0 {timer.ElapsedMilliseconds} - started");
            await api.GetWidget(1);
            Console.WriteLine($"1 {timer.ElapsedMilliseconds}");
            await api.GetWidget(2);
            Console.WriteLine($"2 {timer.ElapsedMilliseconds}");


            await api.GetWidget(3);
            Console.WriteLine($"3 {timer.ElapsedMilliseconds}");
            Assert.Greater(timer.ElapsedMilliseconds, 2000);
            await api.GetWidget(4);
            Console.WriteLine($"4 {timer.ElapsedMilliseconds}");
            Assert.Greater(timer.ElapsedMilliseconds, 3000);


            await api.GetWidget(5);
            Console.WriteLine($"5 {timer.ElapsedMilliseconds}");
            await api.GetWidget(6);
            Console.WriteLine($"6 {timer.ElapsedMilliseconds}");
            await api.GetWidget(7);
            Console.WriteLine($"7 {timer.ElapsedMilliseconds}");
        }

        [Test]
        public async Task MultiThreadTest()
        {
            var config = Substitute.For<IConfiguration>();
            config["APIS:MOCK:RateLimit:Occurrences"].Returns(info => "2");
            config["APIS:MOCK:RateLimit:Seconds"].Returns(info => "1");
            _widgetApi = new WidgetApi(config);

            var timer = Stopwatch.StartNew();
            Console.WriteLine($"[Thread:{Thread.CurrentThread.ManagedThreadId}] 0 {timer.ElapsedMilliseconds} - started");

            var numbers = from number in new int[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11} select CallApiInNewThread(number, timer);
            var results = await Task.WhenAll(numbers);
            timer.Stop();

            Console.WriteLine($"[Thread:{Thread.CurrentThread.ManagedThreadId}] {timer.ElapsedMilliseconds}ms  {results.Length}");
            // 2/second should take at least 5s (the 11th is held by the rate gate until the 5th second)
            Assert.IsTrue(timer.ElapsedMilliseconds > 5000);
            Console.WriteLine("done");
        }


        private WidgetApi _widgetApi;

        private async Task<CallResult<Widget>> CallApiInNewThread(int number, Stopwatch timer)
        {
            // we want to test that the RateGate is indeed thread safe,
            // so must force to run in a new thread, if we don't the call to GetWidget will likely be on the same Thread every time
            return await Task.Run(async () =>
            {
                var result = await _widgetApi.GetWidget(number);
                Console.WriteLine($"[Thread:{Thread.CurrentThread.ManagedThreadId}] {number} {timer.ElapsedMilliseconds}ms");
                return result;
            });
        }

    }
}