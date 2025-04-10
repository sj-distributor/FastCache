using TestApi.Entity;

namespace TestApi.Service;

public interface IMultiSourceService
{
    User Add(User user);

    Task<User> Single(string id);

    Task<User?> SingleOrDefault(string id, string name, bool canChange);

    Task<User?> SingleOrDefault(string id);

    Task<User?> SingleOrDefaultByName(string name);

    Task<User> Update(User user);

    bool Delete(string id);

    IEnumerable<User> List(string page);
}