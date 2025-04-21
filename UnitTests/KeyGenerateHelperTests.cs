using System;
using System.Collections.Generic;
using System.Linq;
using FastCache.Core.Utils;
using TestApi.Entity;
using Xunit;

namespace UnitTests;

public class KeyGenerateHelperTests
{
    private const string Prefix = "single";
    private readonly string _defaultKey = $"{Prefix}:";

    private readonly Company _companyThirdPartyIds = new()
    {
        Id = "3",
        Name = "anson33",
        ThirdPartyIds = new List<long>() { 123, 456, 789 },
        Menus = new List<CompanyMenu>()
        {
            new()
            {
                Id = "1",
                MenuSettings =
                    [new MenuSetting { Id = "menu_setting_id_1" }, new MenuSetting() { Id = "menu_setting_id_2" }]
            },
            new()
            {
                Id = "2",
                MenuSettings = []
            }
        }
    };

    [Fact]
    public void GetCacheKey()
    {
        // 规则：{company:name}:{company:status}
        // 输出: Prefix:anson33:

        var key1 =
            KeyGenerateHelper.GetKey(Prefix, "{company:name}:{company:status}",
                new Dictionary<string, object>() { { "company", _companyThirdPartyIds } });

        Assert.Equal(key1, $"{Prefix}:{_companyThirdPartyIds.Name}:");

        var arrayKey =
            KeyGenerateHelper.GetKey(Prefix, "{company:thirdPartyIds}",
                new Dictionary<string, object>() { { "company", _companyThirdPartyIds } });

        Assert.Equal(arrayKey, $"{Prefix}:{string.Join(",", _companyThirdPartyIds.ThirdPartyIds!)}");

        var companyMenuOpenTime = DateTimeOffset.UtcNow;
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
            KeyGenerateHelper.GetKey(Prefix, "{company:menus}",
                new Dictionary<string, object>() { { "company", companyMenus } });

        Assert.Equal(companyMenusKey, _defaultKey);

        var companyMenusFirstKey =
            KeyGenerateHelper.GetKey(Prefix, "{company:menus:0:openTime}",
                new Dictionary<string, object>() { { "company", companyMenus } });

        var milliseconds = companyMenuOpenTime.ToString("fffffff").TrimEnd('0');
        Assert.Equal(companyMenusFirstKey, $"{Prefix}:{companyMenuOpenTime:yyyy-MM-ddTHH:mm:ss}.{milliseconds}+00:00");

        var companyMerchants = new Company()
        {
            Id = "c1",
            Name = "company 1",
            Merchants = new List<CompanyMerchant>()
            {
                new() { MerchantIds = ["m11", "m12"] },
                new() { MerchantIds = ["m21", "m22"] }
            }
        };

        var companyMerchantsKey =
            KeyGenerateHelper.GetKey(Prefix, "{company:merchants}",
                new Dictionary<string, object>() { { "company", companyMerchants } });

        Assert.Equal(companyMerchantsKey, _defaultKey);

        var companyMerchantsFirstKey =
            KeyGenerateHelper.GetKey(Prefix, "{company:merchants:0:merchantIds:0}",
                new Dictionary<string, object>() { { "company", companyMerchants } });

        Assert.Equal(companyMerchantsFirstKey, $"{Prefix}:{companyMerchants.Merchants.First().MerchantIds.First()}");

        // 新增规则：company:menus:id:all
        // 输出: Prefix:1,2

        var allKeysRule =
            KeyGenerateHelper.GetKey(Prefix, "{company:menus:id:all}",
                new Dictionary<string, object>() { { "company", _companyThirdPartyIds } });

        Assert.Equal(allKeysRule,
            $"{Prefix}:{string.Join(",", _companyThirdPartyIds.Menus!.Select(x => x.Id).ToList())}");

        // 新增规则：{company:menus:all}
        // 输出: Prefix:

        var allKeysRule2 =
            KeyGenerateHelper.GetKey(Prefix, "{company:menus:all}",
                new Dictionary<string, object>() { { "company", _companyThirdPartyIds } });

        Assert.Equal($"{Prefix}:", allKeysRule2);

        // 新增规则：{company:menus:id:all}:{company:id}
        // 输出: Prefix:1,2:3

        var allKeysRule3 =
            KeyGenerateHelper.GetKey(Prefix, "{company:menus:id:all}:{company:id}",
                new Dictionary<string, object>() { { "company", _companyThirdPartyIds } });

        Assert.Equal(
            $"{Prefix}:{string.Join(",", _companyThirdPartyIds.Menus!.Select(x => x.Id).ToList())}:{_companyThirdPartyIds.Id}",
            allKeysRule3);

        // 新增规则：{company:menus:0:menuSettings:id:all}:{company:id}
        // 输出: Prefix:menu_setting_id_1,menu_setting_id_2:3

        var allKeysRule4 =
            KeyGenerateHelper.GetKey(Prefix, "{company:menus:0:menuSettings:id:all}:{company:id}",
                new Dictionary<string, object> { { "company", _companyThirdPartyIds } });

        Assert.Equal(
            $"{Prefix}:{string.Join(",", _companyThirdPartyIds.Menus!.First().MenuSettings.Select(x => x.Id).ToList())}:{_companyThirdPartyIds.Id}",
            allKeysRule4);

        // 新增规则：{company:all}:{company:id}
        // 输出: Prefix::3

        var allKeysRule5 =
            KeyGenerateHelper.GetKey(Prefix, "{company:all}:{company:id}",
                new Dictionary<string, object> { { "company", _companyThirdPartyIds } });

        Assert.Equal($"{Prefix}::{_companyThirdPartyIds.Id}", allKeysRule5);
    }
}