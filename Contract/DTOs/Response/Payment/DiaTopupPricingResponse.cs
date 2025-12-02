using System;

namespace Contract.DTOs.Response.Payment;

public class DiaTopupPricingResponse
{
    public Guid PricingId { get; set; }
    public ulong AmountVnd { get; set; }
    public ulong DiamondGranted { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}
