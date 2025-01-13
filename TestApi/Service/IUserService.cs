using TestApi.Entity;

namespace TestApi.Service;

public interface IUserService: IIndirectService<User>
{
    User Add(User user);

    Task<User> Single(string id);
    
    Task<User> Single(string id, string name);

    Task<User> Update(User user);

    bool Delete(string id);

    IEnumerable<User> List(string page);
}