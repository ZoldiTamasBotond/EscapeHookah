using Microsoft.AspNetCore.Mvc;
using EscapeHookah.Shared.Services;
using System.Threading.Tasks;

namespace EscapeHookah.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserProfileController : ControllerBase
    {
        private readonly IFirebaseAuthService _authService;

        public UserProfileController(IFirebaseAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("ensure")]
        public async Task<IActionResult> EnsureProfile()
        {
            if (!_authService.IsAuthenticated)
                return Unauthorized();

            var uid = _authService.CurrentUserId;
            if (string.IsNullOrWhiteSpace(uid))
                return BadRequest("Missing user id");

            var profile = await _authService.GetUserProfile(uid);
            if (profile == null)
                return StatusCode(500, "Could not create or read user profile");

            return Ok(profile);
        }
    }
}
