using Microsoft.AspNetCore.Mvc;
using EscapeHookah.Shared.Services;
using Firebase.Database;
using System.Threading.Tasks;

namespace EscapeHookah.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceTokenController : ControllerBase
    {
        private readonly IFirebaseAuthService _authService;
        private readonly FirebaseClient _dbClient;

        public DeviceTokenController(IFirebaseAuthService authService)
        {
            _authService = authService;
            _dbClient = new FirebaseClient("https://escapehookah-781e5-default-rtdb.europe-west1.firebasedatabase.app/");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] DeviceTokenRequest request)
        {
            if (!_authService.IsAuthenticated)
                return Unauthorized();

            var uid = _authService.CurrentUserId;
            if (uid != request.UserId)
                return Forbid();

            await _dbClient.Child($"deviceTokens/{uid}/{request.Token}").PutAsync(request.Token);
            return Ok();
        }

        [HttpPost("unregister")]
        public async Task<IActionResult> Unregister([FromBody] DeviceTokenRequest request)
        {
            if (!_authService.IsAuthenticated)
                return Unauthorized();

            var uid = _authService.CurrentUserId;
            if (uid != request.UserId)
                return Forbid();

            await _dbClient.Child($"deviceTokens/{uid}/{request.Token}").DeleteAsync();
            return Ok();
        }
    }

    public class DeviceTokenRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}
