using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace TestApi.Entity;

public record User : IEntity
{
    public User(){}
    
    public User(DateTimeOffset time)
    {
        Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
    
    public string Id { get; set; }
    public string Name { get; set; }
    
    public int Age { get; set; }

    [JsonProperty("Time")]
    [JsonInclude]
    public long Time { get; private set; }
    
    [NotMapped]public List<long>? ThirdPartyIds { get; set; }
}