using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EscapeHookah.Shared.Models;

namespace EscapeHookah.Shared.Services
{
    public class MenuService : IMenuService
    {
        private readonly List<MenuItem> _menu = new()
        {
            new MenuItem { Id = "m1", Name = "Mint Tea", Description = "Fresh mint tea", Price = 2.50m, Category = "Drinks" },
            new MenuItem { Id = "m2", Name = "Lemonade", Description = "Homemade lemonade", Price = 3.00m, Category = "Drinks" },
            new MenuItem { Id = "m3", Name = "Shisha Classic", Description = "Classic tobacco flavor", Price = 10.00m, Category = "Shisha" },
            new MenuItem { Id = "m4", Name = "Fruit Platter", Description = "Assorted fruits", Price = 7.50m, Category = "Food" }
        };

        public Task<List<MenuItem>> GetMenu()
        {
            return Task.FromResult(_menu.ToList());
        }

        public Task<MenuItem?> GetMenuItem(string id)
        {
            var item = _menu.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(item);
        }
    }
}