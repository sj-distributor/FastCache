using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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
        
        users.ForEach(x =>
        {
            _httpClient.PostAsJsonAsync("/", x);
        });
    }

    [Fact]
    public async void RequestCanCache()
    {
        var start = DateTime.Now.Ticks;
        var resp1 = await _httpClient.GetAsync("/?id=1");
        await resp1.Content.ReadAsStringAsync();
        var end = DateTime.Now.Ticks;

        Assert.True( end - start > 1000000);


        var start1 = DateTime.Now.Ticks;
        var resp2 = await _httpClient.GetAsync("/?id=1");
        await resp2.Content.ReadAsStringAsync();
        var end1 = DateTime.Now.Ticks;
        
        Assert.True( end1 - start1 < 300000);
    }

    [Fact]
    public async void CacheCanEvict()
    {
        var resp1 = await _httpClient.GetAsync("/?id=3");
        var result1 = await resp1.Content.ReadAsStringAsync();

        await _httpClient.PutAsJsonAsync("/?id=3", new User()
        {
            Id ="3",
            Name = "anson33"
        });

        var resp2 = await _httpClient.GetAsync("/?id=3");
        var result2 = await resp2.Content.ReadAsStringAsync();

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public async void CacheAndEvictOther()
    {
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
        Assert.True(end - start < 300000 );
    }
}