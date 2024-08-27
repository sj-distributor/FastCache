using System;
using System.Collections.Generic;
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

namespace IntegrationTests;

[Collection("Sequential")]
public class DistributedLockTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly ILockUserService _lockUserService;
    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _serviceProvider;

    public DistributedLockTests(WebApplicationFactory<Program> factory)
    {
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

        var task1 = Task.Run(() =>
        {
            var requestContent = new
            {
                user = new User { Id = "1", Name = "1" },
                delayMs = 700
            };

            _httpClient.PostAsJsonAsync(baseUrl, requestContent);
        });

        tasks.Add(task1);

        var task2 = Task.Run(() => { _httpClient.PostAsJsonAsync(baseUrl, new User { Id = "2", Name = "2" }); });

        tasks.Add(task2);

        await Task.WhenAll(tasks);

        // 验证数据库内容
        var resp = await _httpClient.GetAsync($"{baseUrl}/users?page=1");
        Assert.True(resp.StatusCode == HttpStatusCode.OK);
        var message = await resp.Content.ReadAsStringAsync();
        var response = JsonConvert.DeserializeObject<List<User>>(message);

        Assert.Single(response);
    }
}