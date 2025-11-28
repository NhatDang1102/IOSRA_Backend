using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Payment;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class PaymentHistoryController : AppControllerBase
    {
        private readonly IPaymentHistoryService _paymentHistoryService;

        public PaymentHistoryController(IPaymentHistoryService paymentHistoryService)
        {
            _paymentHistoryService = paymentHistoryService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<PaymentHistoryItemResponse>>> Get([FromQuery] PaymentHistoryQuery query, CancellationToken ct)
        {
            var result = await _paymentHistoryService.GetAsync(AccountId, query ?? new PaymentHistoryQuery(), ct);
            return Ok(result);
        }
    }
}
