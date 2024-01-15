using System.ComponentModel.DataAnnotations.Schema;

namespace TestApi.Entity;


public record Company : IEntity
{
    public string Id { get; set; }
    public string Name { get; set; }
    [NotMapped]public List<CompanyMenu>? Menus { get; set; }
    
    [NotMapped]public List<long>? ThirdPartyIds { get; set; }
    
    [NotMapped]public List<CompanyMerchant>? Merchants { get; set; }
}

public class CompanyMenu
{
    public DateTimeOffset openTime { get; set; }
    public DateTimeOffset endTime { get; set; }
}

public class CompanyMerchant
{
    public List<string> MerchantIds { get; set; }
}