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
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMultiSourceService, MultiSourceService>();
        services.AddScoped<ILockUserService, LockUserService>();
    }
}