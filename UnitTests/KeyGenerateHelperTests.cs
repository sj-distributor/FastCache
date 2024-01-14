using System;
using System.Collections.Generic;
using System.Linq;
using FastCache.Core.Utils;
using TestApi.Entity;
using Xunit;

namespace UnitTests;

public class KeyGenerateHelperTests
{
    [Fact]
    public void GetCacheKey()
    {
        const string prefix = "single";
        const string defaultKey = $"{prefix}:";
        var companyThirdPartyIds = new Company()
        {
            Id = "3",
            Name = "anson33",
            ThirdPartyIds = new List<long>() { 123, 456, 789 }
        };
        var arrayKey =
            KeyGenerateHelper.GetKey(prefix, "{company:thirdPartyIds}",
                new Dictionary<string, object>() { { "company", companyThirdPartyIds } });
        
        Assert.Equal(arrayKey, $"{prefix}:{string.Join(",", companyThirdPartyIds.ThirdPartyIds)}");

        var companyMenuOpenTime = DateTimeOffset.Now.ToUniversalTime();
        var companyMenus = new Company()
        {
            Id = "c1",
            Name = "company 1",
            Menus = new List<CompanyMenu>()
            {
                new CompanyMenu() { openTime = companyMenuOpenTime, endTime = DateTimeOffset.Now.AddHours(1) }
            }
        };
        
        var companyMenusKey =
            KeyGenerateHelper.GetKey(prefix, "{company:menus}",
                new Dictionary<string, object>() { { "company", companyMenus } });
        
        Assert.Equal(companyMenusKey, defaultKey);
        
        var companyMenusFirstKey =
            KeyGenerateHelper.GetKey(prefix, "{company:menus:0:openTime}",
                new Dictionary<string, object>() { { "company", companyMenus } });
        Assert.Equal(companyMenusFirstKey, $"{prefix}:{companyMenuOpenTime:yyyy-MM-ddTHH:mm:ss.ffffffzzz}");

        var companyMerchants = new Company()
        {
            Id = "c1",
            Name = "company 1",
            Merchants = new List<CompanyMerchant>()
            {
                new CompanyMerchant() { MerchantIds = new List<string>() { "m11", "m12" } },
                new CompanyMerchant() { MerchantIds = new List<string>(){ "m21", "m22" }}
            }
        };

        var companyMerchantsKey =
            KeyGenerateHelper.GetKey(prefix, "{company:merchants}",
                new Dictionary<string, object>() { { "company", companyMerchants } });

        Assert.Equal(companyMerchantsKey, defaultKey);
        
        var companyMerchantsFirstKey =
            KeyGenerateHelper.GetKey(prefix, "{company:merchants:0:merchantIds:0}",
                new Dictionary<string, object>() { { "company", companyMerchants } });

        Assert.Equal(companyMerchantsFirstKey, $"{prefix}:{companyMerchants.Merchants.First().MerchantIds.First()}");
    }
}