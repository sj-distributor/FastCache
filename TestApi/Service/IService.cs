namespace TestApi.Service;

public interface IService
{
}

public static class RegisterService
{
    public static void RegisterServices(
        this IServiceCollection services
    )
    {
        typeof(RegisterService).Assembly.GetTypes()
            .Where(x => typeof(IService).IsAssignableFrom(x) && x.IsClass)
            .ToList().ForEach(x => { services.AddScoped(x); });
    }
}