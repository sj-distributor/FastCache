using System.ComponentModel.DataAnnotations.Schema;

namespace TestApi.Entity;

public record User : IEntity
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    [NotMapped]public List<long>? ThirdPartyIds { get; set; }
}