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
using TestApi.DB;
using TestApi.Entity;
using Xunit;

namespace IntegrationTests;

[Collection("Sequential")]
public class MultiSourceApiRequestCacheTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _httpClient;

    public MultiSourceApiRequestCacheTests(WebApplicationFactory<Program> factory)
    {
        _httpClient = factory.CreateClient();
        var memoryDbContext = factory.Services.GetService<MemoryDbContext>();
        var list = memoryDbContext.Set<User>().ToList();
        memoryDbContext.RemoveRange(list);
        memoryDbContext.SaveChanges();
        var users = new List<User>()
        {
            new(DateTimeOffset.UtcNow)
            {
                Id = "1",
                Name = "anson1",
                Age = 10
            },
            new(DateTimeOffset.UtcNow)
            {
                Id = "2",
                Name = "anson2"
            },
            new(DateTimeOffset.UtcNow)
            {
                Id = "3",
                Name = "anson3"
            },
        };

        foreach (var user in users)
        {
            _httpClient.PostAsJsonAsync("/MultiSource", user);
        }
    }

    [Theory]
    [InlineData("/MultiSource")]
    [InlineData("/MultiSourceInMemory")]
    public async void RequestCanCache(string baseUrl)
    {
        var stopwatch = new Stopwatch();

        // 第一次请求
        stopwatch.Start();
        var resp1 = await _httpClient.GetAsync($"{baseUrl}/?id=1");
        stopwatch.Stop();

        Assert.True(resp1.StatusCode == HttpStatusCode.OK);
        await resp1.Content.ReadAsStringAsync();

        // 验证第一次请求花费的时间是否大于 1 秒（1000 毫秒）
        var firstRequestDuration = stopwatch.ElapsedMilliseconds;
        Assert.True(firstRequestDuration > 1000);

        // 第二次请求
        stopwatch.Restart();
        var resp2 = await _httpClient.GetAsync($"{baseUrl}?id=1");
        stopwatch.Stop();

        Assert.True(resp2.StatusCode == HttpStatusCode.OK);
        await resp2.Content.ReadAsStringAsync();

        // 验证第二次请求花费的时间是否小于 400 毫秒
        var secondRequestDuration = stopwatch.ElapsedMilliseconds;
        Assert.True(secondRequestDuration < 400);
    }

    [Theory]
    [InlineData("/MultiSource")]
    [InlineData("/MultiSourceInMemory")]
    public async void CacheCanEvict(string baseUrl)
    {
        var resp1 = await _httpClient.GetAsync($"{baseUrl}?id=3");
        Assert.True(resp1.StatusCode == HttpStatusCode.OK);

        var result1 = await resp1.Content.ReadAsStringAsync();

        var resultForPost = await _httpClient.PutAsJsonAsync($"{baseUrl}?id=3", new User(DateTimeOffset.UtcNow)
        {
            Id = "3",
            Name = "anson33"
        });

        var stringAsync = await resultForPost.Content.ReadAsStringAsync();
        Assert.NotEqual(stringAsync, result1);

        var resp2 = await _httpClient.GetAsync($"{baseUrl}?id=3");
        Assert.True(resp2.StatusCode == HttpStatusCode.OK);

        var result2 = await resp2.Content.ReadAsStringAsync();

        Assert.NotEqual(result1, result2);
    }

    [Theory]
    [InlineData("/MultiSource")]
    [InlineData("/MultiSourceInMemory")]
    public async void CacheAndEvictOther(string baseUrl)
    {
        await _httpClient.PostAsJsonAsync($"{baseUrl}", new User(DateTimeOffset.UtcNow)
        {
            Id = "5",
            Name = "anson5"
        });

        var resp1 = await _httpClient.GetAsync($"{baseUrl}/users?page=1");
        Assert.True(resp1.StatusCode == HttpStatusCode.OK);

        var result1 = await resp1.Content.ReadAsStringAsync();

        var resp2 = await _httpClient.DeleteAsync($"{baseUrl}?id=1");
        Assert.True(resp2.StatusCode == HttpStatusCode.OK);

        await resp2.Content.ReadAsStringAsync();

        var resp3 = await _httpClient.GetAsync($"{baseUrl}/users?page=1");
        Assert.True(resp3.StatusCode == HttpStatusCode.OK);

        var result3 = await resp3.Content.ReadAsStringAsync();

        var start = DateTime.UtcNow.Ticks;
        var resp4 = await _httpClient.GetAsync($"{baseUrl}/users?page=1");
        Assert.True(resp4.StatusCode == HttpStatusCode.OK);

        var result4 = await resp4.Content.ReadAsStringAsync();
        var end = DateTime.UtcNow.Ticks;

        Assert.NotEqual(result1, result3);
        Assert.Equal(result3, result4);
        var timeResult = end - start;
        Assert.True(timeResult < 500000);
    }

    [Theory]
    [InlineData("/MultiSource")]
    public async Task TestUpdated(string baseUrl)
    {
        var responseMessage = await _httpClient.GetAsync($"{baseUrl}?id=1");
        Assert.Equal(responseMessage.StatusCode, HttpStatusCode.OK);

        var user = await responseMessage.Content.ReadFromJsonAsync<User>();

        Assert.NotNull(user);

        var responseMessage2 = await _httpClient.GetAsync($"{baseUrl}?id=1");
        Assert.Equal(responseMessage2.StatusCode, HttpStatusCode.OK);

        var userByCache = await responseMessage2.Content.ReadFromJsonAsync<User>();
        Assert.True(userByCache.Time > 0);

        user.Name = "joe";

        var updateAfter = await _httpClient.PutAsJsonAsync(baseUrl, user);
        Assert.Equal(updateAfter.StatusCode, HttpStatusCode.OK);


        var responseMessageUpdated = await _httpClient.GetAsync($"{baseUrl}?id=1");
        Assert.Equal(responseMessageUpdated.StatusCode, HttpStatusCode.OK);

        var updatedUser = await responseMessageUpdated.Content.ReadFromJsonAsync<User>();

        Assert.NotNull(updatedUser);
        Assert.Equal(updatedUser.Name, "joe");
    }


    [Theory]
    [InlineData("/MultiSource")]
    public async void CacheResultTaskNull(string baseUrl)
    {
        var resp1 = await _httpClient.GetAsync($"{baseUrl}/get?id=null");
        Assert.Equal(HttpStatusCode.NoContent, resp1.StatusCode);

        var result1 = await resp1.Content.ReadAsStringAsync();
        Assert.Equal("", result1);
    }

    [Theory]
    [InlineData("/MultiSource")]
    public async void TestMultiSourceEvictableAllowsEvictionByMultipleRules(string baseUrl)
    {
        var responseMessage = await _httpClient.GetAsync($"{baseUrl}?id=1");
        Assert.Equal(responseMessage.StatusCode, HttpStatusCode.OK);

        var user = await responseMessage.Content.ReadFromJsonAsync<User>();
        Assert.Equal(user.Name, "anson1");
        Assert.Equal(user.Age, 10);

        var responseMessageBySearchName = await _httpClient.GetAsync($"{baseUrl}/get/name?name=anson1");
        Assert.Equal(responseMessageBySearchName.StatusCode, HttpStatusCode.OK);

        var userBySearchName = await responseMessageBySearchName.Content.ReadFromJsonAsync<User>();

        Assert.Equal(userBySearchName.Name, "anson1");
        Assert.Equal(userBySearchName.Age, 10);

        user.Age = 1;

        var updateAfter = await _httpClient.PutAsJsonAsync(baseUrl, user);
        Assert.Equal(updateAfter.StatusCode, HttpStatusCode.OK);

        var responseMessageBySearchNameUpdated = await _httpClient.GetAsync($"{baseUrl}/get/name?name=anson1");
        Assert.Equal(responseMessageBySearchNameUpdated.StatusCode, HttpStatusCode.OK);

        var userUpdated = await responseMessageBySearchNameUpdated.Content.ReadFromJsonAsync<User>();
        Assert.Equal("anson1", userUpdated.Name);
        Assert.Equal(1, userUpdated.Age);

        var deleted = await _httpClient.DeleteAsync($"{baseUrl}?id=1");
        Assert.True(deleted.StatusCode == HttpStatusCode.OK);

        var responseMessageBySearchNameDeleted = await _httpClient.GetAsync($"{baseUrl}/get/name?name=anson1");
        Assert.Equal(responseMessageBySearchNameDeleted.StatusCode, HttpStatusCode.OK);

        var userDeleted = await responseMessageBySearchNameDeleted.Content.ReadFromJsonAsync<User>();
        Assert.Equal("anson1", userDeleted.Name);
        Assert.Equal(1, userDeleted.Age);
    }

    [Theory]
    [InlineData("/MultiSource")]
    public async void TestMultiSourceCacheableMultipleParam(string baseUrl)
    {
        var responseMessage = await _httpClient.GetAsync($"{baseUrl}/get/two?id=123&name=anson1");
        Assert.Equal(responseMessage.StatusCode, HttpStatusCode.NoContent);

        var responseMessage2 = await _httpClient.GetAsync($"{baseUrl}/get/two?id=123&name=anson1");
        Assert.Equal(responseMessage2.StatusCode, HttpStatusCode.NoContent);
    }
}