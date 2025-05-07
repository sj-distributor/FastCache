using AspectCore.Extensions.DependencyInjection;
using FastCache.Core.Entity;
using FastCache.InMemory.Setup;
using FastCache.MultiSource.Setup;
using StackExchange.Redis;
using TestApi.DB;
using TestApi.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new DynamicProxyServiceProviderFactory());

// builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
// builder.Host.ConfigureContainer<ContainerBuilder>(build =>
// {
//     build.RegisterDynamicProxy();
// });

builder.Services.AddMvc().AddControllersAsServices();

builder.Services.RegisterMultiSourceCache(
    new ConfigurationOptions()
    {
        EndPoints = { "localhost:6379" },
        ReconnectRetryPolicy = new ExponentialRetry(
            deltaBackOffMilliseconds: 1000, // 初始延迟 1s
            maxDeltaBackOffMilliseconds: 30000 // 最大延迟 30s
        ),
        AbortOnConnectFail = false,
        SyncTimeout = 5000,
        ConnectTimeout = 5000,
        ResponseTimeout = 5000
    },
    new RedisCacheOptions()
    {
        ConnectionRestoredHandler = (o, eventArgs) => { Console.WriteLine("[断开链接]"); },
        ConnectionFailureHandler = (o, eventArgs) => { Console.WriteLine("[重新链接]"); }
    }
);


// builder.Services.AddMultiBucketsInMemoryCache();
builder.Services.AddInMemoryCache();
// builder.Services.AddRedisCache("server=localhost:6379;timeout=5000;MaxMessageSize=1024000;Expire=3600"); // "Expire=3600" redis global timeout 

builder.Services.RegisterServices();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddRazorPages();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<MemoryDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}