using System.Collections.Generic;
using System.Threading.Tasks;

namespace EscapeHookah.Shared.Services
{
    public interface IFirebaseAuthService
    {
        bool IsAuthenticated { get; }
        string CurrentUserId { get; }
        string CurrentUserEmail { get; }

        Task<string> GetIdTokenAsync();
        Task<Dictionary<string, object>?> GetUserProfile(string userId);
        Task<bool> RegisterUser(string email, string password, string firstName, string lastName,
                                string username, string phoneNumber, System.DateTime? dateOfBirth, string gender);
        Task<bool> LoginUser(string email, string password);
        void Logout();
        bool IsFirebaseAvailable { get; }

        // Admin helpers
        Task<bool> IsUserAdminAsync(string userId);
        Task<bool> PromoteUserToAdmin(string userId);
        Task<bool> CreateAdminUser(string email, string password, string firstName, string lastName, string username, string phoneNumber);
    }
}