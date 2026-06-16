using System.Collections.Generic;
using System.Threading.Tasks;
using EscapeHookah.Shared.Models;

namespace EscapeHookah.Shared.Services
{
    public interface IMenuService
    {
        Task<List<MenuItem>> GetMenu();
        Task<MenuItem?> GetMenuItem(string id);
        // Admin operations
        Task<bool> AddMenuItem(MenuItem item);
        Task<bool> UpdateMenuItem(MenuItem item);
        Task<bool> DeleteMenuItem(string id);
    }
}