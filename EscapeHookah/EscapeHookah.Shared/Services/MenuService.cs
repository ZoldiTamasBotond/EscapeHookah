using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EscapeHookah.Shared.Models;
using Firebase.Database;
using Firebase.Database.Query;
using Microsoft.Extensions.Logging;

namespace EscapeHookah.Shared.Services
{
    public class MenuService : IMenuService
    {
        private readonly List<MenuItem> _menuFallback = new()
        {
            new MenuItem { Id = "m1", Name = "Mint Tea", Description = "Fresh mint tea", Price = 2.50m, Category = "Drinks" },
            new MenuItem { Id = "m2", Name = "Lemonade", Description = "Homemade lemonade", Price = 3.00m, Category = "Drinks" },
            new MenuItem { Id = "m3", Name = "Shisha Classic", Description = "Classic tobacco flavor", Price = 10.00m, Category = "Shisha" },
            new MenuItem { Id = "m4", Name = "Fruit Platter", Description = "Assorted fruits", Price = 7.50m, Category = "Food" }
        };

        private FirebaseClient? _databaseClient;
        private readonly IFirebaseAuthService _authService;
        private readonly ILogger<MenuService> _logger;

        public MenuService(IFirebaseAuthService authService, ILogger<MenuService> logger)
        {
            _authService = authService;
            _databaseClient = null;
            _logger = logger;
        }

        public async Task<List<MenuItem>> GetMenu()
        {
            try
            {
                if (_databaseClient == null)
                {
                    try
                    {
                        _databaseClient = new FirebaseClient(
                            "https://escapehookah-781e5-default-rtdb.europe-west1.firebasedatabase.app/",
                            new FirebaseOptions { AuthTokenAsyncFactory = async () => await _authService.GetIdTokenAsync() });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "MenuService: failed to initialize Firebase client in GetMenu");
                        return await Task.FromResult(_menuFallback.ToList());
                    }
                }

                var items = await _databaseClient.Child("menu").OnceAsync<MenuItem>();
                var list = items.Where(x => x.Object != null).Select(x =>
                {
                    var m = x.Object!;
                    m.Id = x.Key ?? m.Id;
                    return m;
                }).ToList();

                return list;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MenuService: GetMenu failed");
                return _menuFallback.ToList();
            }
        }

        public async Task<MenuItem?> GetMenuItem(string id)
        {
            try
            {
                if (_databaseClient == null)
                {
                    try
                    {
                        _databaseClient = new FirebaseClient(
                            "https://escapehookah-781e5-default-rtdb.europe-west1.firebasedatabase.app/",
                            new FirebaseOptions { AuthTokenAsyncFactory = async () => await _authService.GetIdTokenAsync() });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "MenuService: failed to initialize Firebase client in GetMenuItem");
                        return _menuFallback.FirstOrDefault(x => x.Id == id);
                    }
                }

                var itm = await _databaseClient.Child("menu").Child(id).OnceSingleAsync<MenuItem>();
                if (itm == null) return null;
                itm.Id = id;
                return itm;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MenuService: GetMenuItem failed for id {MenuId}", id);
                return _menuFallback.FirstOrDefault(x => x.Id == id);
            }
        }

        public async Task<bool> AddMenuItem(MenuItem item)
        {
            if (item == null)
                return false;

            try
            {
                if (_databaseClient == null)
                {
                    try
                    {
                        _databaseClient = new FirebaseClient(
                            "https://escapehookah-781e5-default-rtdb.europe-west1.firebasedatabase.app/",
                            new FirebaseOptions { AuthTokenAsyncFactory = async () => await _authService.GetIdTokenAsync() });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "MenuService: failed to initialize Firebase client in AddMenuItem");
                        // fallback: assign auto id if missing then add to fallback list
                        if (string.IsNullOrWhiteSpace(item.Id))
                        {
                            var next = _menuFallback.Count + 1;
                            item.Id = $"m{next}";
                        }
                        if (_menuFallback.Any(x => x.Id == item.Id)) return false;
                        _menuFallback.Add(item);
                        return true;
                    }
                }

                // If Id not provided, generate one based on existing keys (m1, m2, ...)
                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    var existing = await _databaseClient.Child("menu").OnceAsync<MenuItem>();
                    var max = 0;
                    foreach (var e in existing)
                    {
                        if (int.TryParse(e.Key?.TrimStart('m') , out var n))
                        {
                            if (n > max) max = n;
                        }
                    }
                    var next = max + 1;
                    item.Id = $"m{next}";
                }

                await _databaseClient.Child("menu").Child(item.Id).PutAsync(item);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MenuService: AddMenuItem failed for id {MenuId}", item.Id);
                return false;
            }
        }

        public async Task<bool> UpdateMenuItem(MenuItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
                return false;

            try
            {
                if (_databaseClient == null)
                {
                    try
                    {
                        _databaseClient = new FirebaseClient(
                            "https://escapehookah-781e5-default-rtdb.europe-west1.firebasedatabase.app/",
                            new FirebaseOptions { AuthTokenAsyncFactory = async () => await _authService.GetIdTokenAsync() });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "MenuService: failed to initialize Firebase client in UpdateMenuItem");
                        var existing = _menuFallback.FirstOrDefault(x => x.Id == item.Id);
                        if (existing == null) return false;
                        existing.Name = item.Name;
                        existing.Description = item.Description;
                        existing.Price = item.Price;
                        existing.Category = item.Category;
                        return true;
                    }
                }

                await _databaseClient.Child("menu").Child(item.Id).PutAsync(item);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MenuService: UpdateMenuItem failed for id {MenuId}", item.Id);
                return false;
            }
        }

        public async Task<bool> DeleteMenuItem(string id)
        {
            try
            {
                if (_databaseClient == null)
                {
                    try
                    {
                        _databaseClient = new FirebaseClient(
                            "https://escapehookah-781e5-default-rtdb.europe-west1.firebasedatabase.app/",
                            new FirebaseOptions { AuthTokenAsyncFactory = async () => await _authService.GetIdTokenAsync() });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "MenuService: failed to initialize Firebase client in DeleteMenuItem");
                        var existing = _menuFallback.FirstOrDefault(x => x.Id == id);
                        if (existing == null) return false;
                        _menuFallback.Remove(existing);
                        return true;
                    }
                }

                await _databaseClient.Child($"menu/{id}").DeleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MenuService: DeleteMenuItem failed for id {MenuId}", id);
                return false;
            }
        }
    }
}
