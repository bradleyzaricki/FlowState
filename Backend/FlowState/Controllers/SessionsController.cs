using Microsoft.AspNetCore.Mvc;

namespace FlowState.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionsController : ControllerBase
    {
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "Flow State API is running" });
        }
    }
}
