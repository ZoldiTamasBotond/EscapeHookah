using Microsoft.AspNetCore.Mvc;
using EscapeHookah.Shared.Services;
using System.Threading.Tasks;

namespace EscapeHookah.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IFirebaseAuthService _authService;

        public AdminController(IFirebaseAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("promote/{userId}")]
        public async Task<IActionResult> Promote(string userId)
        {
            if (!_authService.IsAuthenticated)
                return Unauthorized();

            // Only existing admins can promote
            var current = _authService.CurrentUserId;
            if (!await _authService.IsUserAdminAsync(current))
                return Forbid();

            var ok = await _authService.PromoteUserToAdmin(userId);
            if (!ok) return BadRequest();
            return Ok();
        }

        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminRequest req)
        {
            // Only allow when current caller is admin
            if (!_authService.IsAuthenticated)
                return Unauthorized();

            if (!await _authService.IsUserAdminAsync(_authService.CurrentUserId))
                return Forbid();

            var ok = await _authService.CreateAdminUser(req.Email, req.Password, req.FirstName, req.LastName, req.UserName, req.PhoneNumber);
            if (!ok) return BadRequest();
            return Ok();
        }
    }

    public class CreateAdminRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
