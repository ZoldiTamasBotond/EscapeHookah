using System.Collections.Generic;
using System.Threading.Tasks;
using EscapeHookah.Shared.Models;

namespace EscapeHookah.Shared.Services
{
    public interface IMenuService
    {
        Task<List<MenuItem>> GetMenu();
        Task<MenuItem?> GetMenuItem(string id);
    }
}