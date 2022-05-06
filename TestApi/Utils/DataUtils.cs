namespace TestApi.Utils;

public static class DataUtils
{
    public static Entity.User[] GetData()
    {
        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };
        return Enumerable.Range(1, 5).Select(index => new Entity.User
            {
                Id = $"{index}",
                Name = summaries[Random.Shared.Next(1, 10)],
            })
            .ToArray();
    }
}