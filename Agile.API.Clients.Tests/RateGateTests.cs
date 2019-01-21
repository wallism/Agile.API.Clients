using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agile.API.Client.CallHandling;
using Agile.API.Client.Tests.Mocks;
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
        private MockApi GetMockApi(RateLimit rateLimit)
        {
            return new MockApi("KEY", "SECRET", rateLimit);
        }

        [Test]
        public async Task CallReturns_404_ErrorResult()
        {
            // get api with Rate limit of 3/second
            var api = GetMockApi(RateLimit.Build(3, TimeSpan.FromMilliseconds(1000)));

            var result = await api.GetWidget(1);
            Assert.IsTrue(result is CallErrorResult<Widget>);
            Assert.IsFalse(result.WasSuccessful);
        }

        [Test]
        public async Task RateLimitOf_2PerSecond_FifthCallOccursAfter_2Seconds()
        {
            // get api with Rate limit of 2/second
            var api = GetMockApi(RateLimit.Build(2, TimeSpan.FromMilliseconds(1000)));

            var timer = Stopwatch.StartNew();
            // TODO also test running all on different threads
            Console.WriteLine($"0 {timer.ElapsedMilliseconds} - started");
            var result1 = await api.GetWidget(1);
            Console.WriteLine($"1 {timer.ElapsedMilliseconds}");
            var result2 = await api.GetWidget(2);
            Console.WriteLine($"2 {timer.ElapsedMilliseconds}");
            var result3 = await api.GetWidget(3);
            Console.WriteLine($"3 {timer.ElapsedMilliseconds}");
            var result4 = await api.GetWidget(4);

            Assert.IsTrue(timer.ElapsedMilliseconds < 2000);
            Console.WriteLine($"4 {timer.ElapsedMilliseconds}");
            var result5 = await api.GetWidget(5);
            Assert.IsTrue(timer.ElapsedMilliseconds > 2000);

            Console.WriteLine($"5 {timer.ElapsedMilliseconds}");
            var result6 = await api.GetWidget(6);
            Console.WriteLine($"6 {timer.ElapsedMilliseconds}");
            var result7 = await api.GetWidget(7);
            Console.WriteLine($"7 {timer.ElapsedMilliseconds}");

            var result8 = await api.GetWidget(8);
            Console.WriteLine($"8 {timer.ElapsedMilliseconds}");
        }

        [Test]
        public async Task MultiThreadTest()
        {
            mockApi = GetMockApi(RateLimit.Build(2, TimeSpan.FromMilliseconds(1000)));
            
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


        private MockApi mockApi;

        private async Task<ServiceCallResult<Widget>> CallApiInNewThread(int number, Stopwatch timer)
        {
            // we want to test that the RateGate is indeed thread safe,
            // so must force to run in a new thread, if we don't the call to GetWidget will likely be on the same Thread every time
            return await Task.Run(async () =>
            {
                var result = await mockApi.GetWidget(number);
                Console.WriteLine($"[Thread:{Thread.CurrentThread.ManagedThreadId}] {number} {timer.ElapsedMilliseconds}ms");
                return result;
            });
        }

    }
}