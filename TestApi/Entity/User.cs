namespace TestApi.Entity;

public record User : IEntity
{
    public string Id { get; set; }
    public string Name { get; set; }
}