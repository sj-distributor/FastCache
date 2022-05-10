using AspectCore.Extensions.DependencyInjection;
using EasyCache.InMemory.Setup;
using EasyCache.Redis.Setup;
using TestApi.DB;
using TestApi.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new DynamicProxyServiceProviderFactory());
// builder.Services.AddMultiBucketsInMemoryCache();
builder.Services.AddInMemoryCache();
// builder.Services.AddRedisCache("server=localhost:6379;timeout=5000;MaxMessageSize=1024000;Expire=3600");

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
public partial class Program { }