using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TestApi.DB;
using TestApi.Entity;
using Xunit;

namespace IntegrationTests;

[Collection("Sequential")]
public class ApiRequestCacheTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _httpClient;

    public ApiRequestCacheTests(WebApplicationFactory<Program> factory)
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
            _httpClient.PostAsJsonAsync("/", user);
        }
    }

    [Fact]
    public async void RequestCanCache()
    {
        var start = DateTime.Now.Ticks;
        var resp1 = await _httpClient.GetAsync("/?id=1");
        await resp1.Content.ReadAsStringAsync();
        var end = DateTime.Now.Ticks;

        Assert.True(end - start > 1000000);

        var start1 = DateTime.Now.Ticks;
        var resp2 = await _httpClient.GetAsync("/?id=1");
        await resp2.Content.ReadAsStringAsync();
        var end1 = DateTime.Now.Ticks;

        var result = end1 - start1;
        Assert.True(result < 400000);
    }

    [Fact]
    public async void CacheCanEvict()
    {
        var resp1 = await _httpClient.GetAsync("/?id=3");
        var result1 = await resp1.Content.ReadAsStringAsync();

        var resultForPost = await _httpClient.PutAsJsonAsync("/?id=3", new User()
        {
            Id = "3",
            Name = "anson33"
        });

        var stringAsync = await resultForPost.Content.ReadAsStringAsync();
        Assert.NotEqual(stringAsync, result1);

        var resp2 = await _httpClient.GetAsync("/?id=3");
        var result2 = await resp2.Content.ReadAsStringAsync();

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public async void CacheAndEvictOther()
    {
        
        await _httpClient.PostAsJsonAsync("/", new User()
        {
            Id = "5",
            Name = "anson5"
        });
        
        var resp1 = await _httpClient.GetAsync("/users?page=1");
        var result1 = await resp1.Content.ReadAsStringAsync();

        var resp2 = await _httpClient.DeleteAsync("/?id=1");
        await resp2.Content.ReadAsStringAsync();

        var resp3 = await _httpClient.GetAsync("/users?page=1");
        var result3 = await resp3.Content.ReadAsStringAsync();

        var start = DateTime.Now.Ticks;
        var resp4 = await _httpClient.GetAsync("/users?page=1");
        var result4 = await resp4.Content.ReadAsStringAsync();
        var end = DateTime.Now.Ticks;

        Assert.NotEqual(result1, result3);
        Assert.Equal(result3, result4);
        var timeResult = end - start;
        Assert.True(timeResult < 500000);
    }
}