using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
            new()
            {
                Id = "1",
                Name = "anson1"
            },
            new()
            {
                Id = "2",
                Name = "anson2"
            },
            new()
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
        var start = DateTime.UtcNow.Ticks;
        var resp1 = await _httpClient.GetAsync($"{baseUrl}/?id=1");
        Assert.True(resp1.StatusCode == HttpStatusCode.OK);
        await resp1.Content.ReadAsStringAsync();
        var end = DateTime.UtcNow.Ticks;

        Assert.True(end - start > 1000000);

        var start1 = DateTime.UtcNow.Ticks;
        var resp2 = await _httpClient.GetAsync($"{baseUrl}?id=1");
        Assert.True(resp2.StatusCode == HttpStatusCode.OK);
        await resp2.Content.ReadAsStringAsync();
        var end1 = DateTime.UtcNow.Ticks;

        var result = end1 - start1;
        Assert.True(result < 400000);
    }

    [Theory]
    [InlineData("/MultiSource")]
    [InlineData("/MultiSourceInMemory")]
    public async void CacheCanEvict(string baseUrl)
    {
        var resp1 = await _httpClient.GetAsync($"{baseUrl}?id=3");
        Assert.True(resp1.StatusCode == HttpStatusCode.OK);

        var result1 = await resp1.Content.ReadAsStringAsync();

        var resultForPost = await _httpClient.PutAsJsonAsync($"{baseUrl}?id=3", new User()
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
        await _httpClient.PostAsJsonAsync($"{baseUrl}", new User()
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
}