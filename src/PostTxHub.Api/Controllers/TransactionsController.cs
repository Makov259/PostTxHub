using Microsoft.AspNetCore.Mvc;
using PostTxHub.Api.Contracts;

namespace PostTxHub.Api.Controllers;


[ApiController]
[Route("transactions")]
public sealed class TransactionsController : ControllerBase
{

    [HttpPost]
    public IActionResult Create([FromBody] CreateTransactionsRequest request)
    {
        var transactionId = Guid.NewGuid();

        //Next Step: publish to RabbitMQ here.
        
        return Accepted(new { transactionId });
    }
}