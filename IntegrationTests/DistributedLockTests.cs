using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using TestApi.DB;
using TestApi.Entity;
using TestApi.Service;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests;

[Collection("Sequential")]
public class DistributedLockTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ILockUserService _lockUserService;
    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _serviceProvider;

    public DistributedLockTests(WebApplicationFactory<Program> factory, ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _httpClient = factory.CreateClient();
        _serviceProvider = factory.Services;

        // 清空数据库
        using var scope = _serviceProvider.CreateScope();
        var memoryDbContext = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        var list = memoryDbContext.Set<User>().ToList();
        memoryDbContext.RemoveRange(list);
        memoryDbContext.SaveChanges();
    }

    [Theory]
    [InlineData("/UseLock")]
    public async Task CheckCanLock(string baseUrl)
    {
        var tasks = new List<Task>();

        async Task SortedSet(int index, int delayMs)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var responseMessage = await _httpClient.PostAsJsonAsync($"{baseUrl}?delayMs={delayMs}",
                new User { Id = index.ToString(), Name = index.ToString() });
            stopwatch.Stop();

            var message = await responseMessage.Content.ReadAsStringAsync();

            _testOutputHelper.WriteLine($"{message} - {stopwatch.ElapsedMilliseconds}");
        }

        tasks.Add(SortedSet(0, 700));
        tasks.Add(SortedSet(1, 700));

        await Task.WhenAll(tasks);

        // 验证数据库内容
        var resp = await _httpClient.GetAsync($"{baseUrl}/users?page=1");
        Assert.True(resp.StatusCode == HttpStatusCode.OK);
        var message = await resp.Content.ReadAsStringAsync();
        var response = JsonConvert.DeserializeObject<List<User>>(message);

        Assert.Single(response);

        await _httpClient.DeleteAsync($"{baseUrl}?id=0");
        await _httpClient.DeleteAsync($"{baseUrl}?id=1");
    }

    [Theory]
    [InlineData("/UseLock")]
    public async Task CheckCanLockAndCache(string baseUrl)
    {
        var tasks = new List<Task>();

        async Task SortedSet(int index, int delayMs)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/add-with-cache?delayMs={delayMs}",
                new User { Id = index.ToString(), Name = index.ToString() });
            stopwatch.Stop();

            var message = await response.Content.ReadAsStringAsync();

            _testOutputHelper.WriteLine($"{message} - {stopwatch.ElapsedMilliseconds}");
        }

        tasks.Add(SortedSet(3, 800));
        tasks.Add(SortedSet(4, 700));

        await Task.WhenAll(tasks);

        // 验证数据库内容
        var resp = await _httpClient.GetAsync($"{baseUrl}/users?page=1");
        Assert.True(resp.StatusCode == HttpStatusCode.OK);
        var message = await resp.Content.ReadAsStringAsync();
        var response = JsonConvert.DeserializeObject<List<User>>(message);
        Assert.Single(response);

        var stopwatch = Stopwatch.StartNew();
        stopwatch.Start();

        var resp2 = await _httpClient.GetAsync($"{baseUrl}?id={3}");
        stopwatch.Stop();
        var firstQuest = stopwatch.ElapsedMilliseconds;
        if (resp2.StatusCode == HttpStatusCode.OK)
        {
            var message2 = await resp2.Content.ReadAsStringAsync();
            var response2 = JsonConvert.DeserializeObject<User>(message2);
            if (response2 != null)
            {
                Assert.True(firstQuest < 100);
            }
        }

        var resp3 = await _httpClient.GetAsync($"{baseUrl}?id={3}");
        stopwatch.Stop();
        var stopwatchElapsed = stopwatch.ElapsedMilliseconds;
        if (resp3.StatusCode == HttpStatusCode.OK)
        {
            var message3 = await resp3.Content.ReadAsStringAsync();
            var response3 = JsonConvert.DeserializeObject<User>(message3);
            if (response3 != null)
            {
                Assert.True(stopwatchElapsed < 100);
            }
        }

        await _httpClient.DeleteAsync($"{baseUrl}?id=3");
        await _httpClient.DeleteAsync($"{baseUrl}?id=4");
    }
}