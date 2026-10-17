using Abp.MultiTenancy;
using System.ComponentModel.DataAnnotations;

namespace _2380601390.Authorization.Accounts.Dto;

public class IsTenantAvailableInput
{
    [Required]
    [StringLength(AbpTenantBase.MaxTenancyNameLength)]
    public string TenancyName { get; set; }
}
