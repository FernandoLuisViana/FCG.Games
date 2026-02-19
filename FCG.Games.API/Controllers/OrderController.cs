using FCG.Games.API.Extensions;
using FCG.Games.API.Filters;
using FCG.Games.API.Messaging;
using FCG.Games.Domain.DTOs.Requests;
using FCG.Games.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Games.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderController(IOrderService service, RabbitPublisher publisher) : ControllerBase
{
    [HttpPost]
    [ServiceFilter(typeof(ValidationFilter<CreateOrderRequest>))]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var orderCreated = await service.CreateAsync(request, ct);
        if (orderCreated is not null)
        {
            publisher.PublishOrderCreated(new
            {
                orderId = orderCreated.Data.Id,
                paymentMethod = orderCreated.Data.PaymentMethod
            });
        }
        return orderCreated.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var orders = await service.ListAsync(ct);
        return orders.ToActionResult();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await service.GetByIdAsync(id);
        return order.ToActionResult();
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUserId(Guid userId)
    {
        var orders = await service.GetByUserIdAsync(userId);
        return orders.ToActionResult();
    }
}
