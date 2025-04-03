using TestApi.Entity;

namespace TestApi.Service;

public interface IUserService
{
    User Add(User user);

    Task<User> Single(string id);

    Task<User?> SingleOrDefault(string id);

    Task<User> Update(User user);

    bool Delete(string id);

    IEnumerable<User> List(string page);
}