using Microsoft.AspNetCore.Mvc;
using Nutzen;
using Nutzen.WebAPI.Features.RandomNumber;

namespace Nutzen.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RandomNumberController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromServices] IRequestHandler<RandomNumber.Request, RandomNumber.Response> handler)
    {
        var result = await handler.Handle(new RandomNumber.Request());

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return BadRequest(result.ErrorMessage);
    }
}
