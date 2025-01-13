using TestApi.Entity;

namespace TestApi.Service;


public interface IService
{
}


public interface IIndirectService<T> where T : IEntity
{
    Task<T> Add(T entity, CancellationToken cancellationToken);
    Task<T> Single(string id, string name, CancellationToken cancellationToken);
}

public static class RegisterService
{
    public static void RegisterServices(
        this IServiceCollection services
    )
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMultiSourceService, MultiSourceService>();
    }
}