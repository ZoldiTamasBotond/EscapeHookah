using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EscapeHookah.Shared.Services
{
    public class FirebaseAuthService : IFirebaseAuthService
    {
        private readonly HttpClient _httpClient;
        private FirebaseAuthClient? _authClient;
        private FirebaseClient? _databaseClient;
        private User? _currentUser;

        // Token management for REST API calls
        private string _idToken = string.Empty;
        private string _refreshToken = string.Empty;
        private DateTime _tokenExpiryUtc = DateTime.MinValue;
        private string _currentUserId = string.Empty; // Private backing field

        // Your Firebase credentials
        private const string FirebaseApiKey = "AIzaSyDPC6MqXhct7-gVEh_2UgPQUoJXsvaCBYU";
        private const string FirebaseDatabaseUrl = "https://escapehookah-781e5-default-rtdb.europe-west1.firebasedatabase.app/";
        private const string FirebaseAuthDomain = "escapehookah-781e5.firebaseapp.com";

        public FirebaseAuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            Debug.WriteLine("========== FIREBASE INITIALIZATION START ==========");

            try
            {
                Debug.WriteLine($"API Key: {FirebaseApiKey}");
                Debug.WriteLine($"Database URL: {FirebaseDatabaseUrl}");
                Debug.WriteLine($"Auth Domain: {FirebaseAuthDomain}");

                // Initialize Firebase Authentication
                var authConfig = new FirebaseAuthConfig
                {
                    ApiKey = FirebaseApiKey,
                    AuthDomain = FirebaseAuthDomain,
                    Providers = new FirebaseAuthProvider[]
                    {
                        new EmailProvider()
                    }
                };

                _authClient = new FirebaseAuthClient(authConfig);
                Debug.WriteLine("✅ Firebase Auth Client Created with Email/Password provider");

                // Initialize Firebase Realtime Database with auth token factory
                // Defer creating the FirebaseClient until an operation requires it to avoid token timing issues
                _databaseClient = null;
                Debug.WriteLine("✅ Firebase Database Client Created");

                Debug.WriteLine($"✅ Firebase Available: {_authClient != null && _databaseClient != null}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Firebase Error: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            Debug.WriteLine("========== FIREBASE INITIALIZATION END ==========");
        }

        // Lazily create FirebaseClient when needed (so token is available)
        private async Task<bool> EnsureDatabaseClientAsync()
        {
            if (_databaseClient != null) return true;
            try
            {
                _databaseClient = new FirebaseClient(
                    FirebaseDatabaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = async () => await GetIdTokenAsync() });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EnsureDatabaseClientAsync error: {ex.Message}");
                return false;
            }
        }

        public bool IsFirebaseAvailable => _authClient != null && _databaseClient != null;
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_currentUserId) && !string.IsNullOrWhiteSpace(_idToken);
        public string CurrentUserId => _currentUserId; // Property getter only
        public string CurrentUserEmail => _currentUser?.Info?.Email ?? string.Empty;

        // Call this after successful login
        public void SetAuthSession(string userId, string idToken, string refreshToken, int expiresInSeconds)
        {
            Debug.WriteLine($"🔐 Setting auth session for user: {userId}");
            _currentUserId = userId ?? string.Empty; // Set the private backing field
            _idToken = idToken ?? string.Empty;
            _refreshToken = refreshToken ?? string.Empty;
            _tokenExpiryUtc = DateTime.UtcNow.AddSeconds(Math.Max(expiresInSeconds - 60, 0));
        }

        public void ClearAuthSession()
        {
            Debug.WriteLine("🚪 Clearing auth session");
            _currentUserId = string.Empty;
            _idToken = string.Empty;
            _refreshToken = string.Empty;
            _tokenExpiryUtc = DateTime.MinValue;
            _currentUser = null;
        }

        public async Task<string> GetIdTokenAsync()
        {
            if (string.IsNullOrWhiteSpace(_idToken))
            {
                Debug.WriteLine("⚠️ No ID token available");
                return string.Empty;
            }

            if (DateTime.UtcNow < _tokenExpiryUtc)
            {
                Debug.WriteLine("✅ Using cached ID token");
                return _idToken;
            }

            Debug.WriteLine("🔄 ID token expired, refreshing...");

            if (string.IsNullOrWhiteSpace(_refreshToken))
            {
                Debug.WriteLine("❌ No refresh token available");
                return _idToken;
            }

            try
            {
                var refreshUrl = $"https://securetoken.googleapis.com/v1/token?key={FirebaseApiKey}";
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _refreshToken
                });

                var response = await _httpClient.PostAsync(refreshUrl, content);
                response.EnsureSuccessStatusCode();

                var refresh = await response.Content.ReadFromJsonAsync<FirebaseRefreshResponse>();

                if (refresh != null)
                {
                    _idToken = refresh.IdToken ?? _idToken;
                    _refreshToken = refresh.RefreshToken ?? _refreshToken;

                    if (int.TryParse(refresh.ExpiresIn, out var expiresInSeconds))
                    {
                        _tokenExpiryUtc = DateTime.UtcNow.AddSeconds(Math.Max(expiresInSeconds - 60, 0));
                    }

                    Debug.WriteLine("✅ Token refreshed successfully");
                }

                return _idToken;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Token refresh failed: {ex.Message}");
                return _idToken;
            }
        }

        // Admin helpers
        public async Task<bool> IsUserAdminAsync(string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return false;

                if (!await EnsureDatabaseClientAsync())
                    return false;

                var userData = await _databaseClient
                    .Child("users")
                    .Child(userId)
                    .OnceSingleAsync<Dictionary<string, object>>();

                if (userData == null)
                    return false;

                if (userData.TryGetValue("Role", out var roleObj) && roleObj != null)
                {
                    return roleObj.ToString() == "Admin";
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IsUserAdminAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PromoteUserToAdmin(string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return false;

                if (!await EnsureDatabaseClientAsync())
                    return false;

                var userData = await _databaseClient
                    .Child("users")
                    .Child(userId)
                    .OnceSingleAsync<Dictionary<string, object>>();

                if (userData == null)
                    return false;

                userData["Role"] = "Admin";

                await _databaseClient.Child("users").Child(userId).PutAsync(userData);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PromoteUserToAdmin error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateAdminUser(string email, string password, string firstName, string lastName, string username, string phoneNumber)
        {
            Debug.WriteLine($"CreateAdminUser called for {email}");
            try
            {
                // Ensure database client is available before creating profile
                if (!await EnsureDatabaseClientAsync())
                    return false;

                var userCredential = await _authClient!.CreateUserWithEmailAndPasswordAsync(email, password);
                var newUser = userCredential.User;
                if (newUser == null)
                    return false;

                var uid = newUser.Uid;

                var userData = new Dictionary<string, object>
                {
                    { "FirstName", firstName },
                    { "LastName", lastName },
                    { "UserName", username },
                    { "EMail", email },
                    { "PhoneNumber", phoneNumber },
                    { "Role", "Admin" },
                    { "Rate", 0 },
                    { "ReservationsID", new List<string>() },
                    { "CreatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                    { "UserId", uid }
                };

                await _databaseClient!
                    .Child("users")
                    .Child(uid)
                    .PutAsync(userData);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CreateAdminUser error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RegisterUser(string email, string password, string firstName, string lastName,
                                          string username, string phoneNumber, DateTime? dateOfBirth, string gender)
        {
            Debug.WriteLine($"📝 RegisterUser called for email: {email}");

            try
            {
                if (!IsFirebaseAvailable)
                {
                    Debug.WriteLine("❌ Firebase not available");
                    return false;
                }

                // Check if username already exists
                Debug.WriteLine("Checking if username is available...");
                var usernameExists = await CheckUsernameExists(username);

                if (usernameExists)
                {
                    Debug.WriteLine("❌ Username already taken");
                    throw new Exception("Username already taken. Please choose another username.");
                }

                Debug.WriteLine("Creating user in Firebase Authentication...");

                // Create user with email and password
                var userCredential = await _authClient!.CreateUserWithEmailAndPasswordAsync(email, password);
                _currentUser = userCredential.User;
                _currentUserId = _currentUser.Uid;

                Debug.WriteLine($"✅ User created with UID: {_currentUser.Uid}");

                // Get the ID token
                var idToken = await _currentUser.GetIdTokenAsync();
                var refreshToken = TryGetRefreshToken(userCredential);

                // Set auth session
                SetAuthSession(
                    userId: _currentUser.Uid,
                    idToken: idToken,
                    refreshToken: refreshToken,
                    expiresInSeconds: 3600
                );

                // Convert DateTime to Unix timestamp milliseconds
                long dateOfBirthTimestamp = 0;
                if (dateOfBirth.HasValue)
                {
                    var dateOnly = new DateTime(dateOfBirth.Value.Year, dateOfBirth.Value.Month, dateOfBirth.Value.Day, 0, 0, 0, DateTimeKind.Utc);
                    dateOfBirthTimestamp = new DateTimeOffset(dateOnly).ToUnixTimeMilliseconds();
                    Debug.WriteLine($"📅 Date of birth: {dateOnly:yyyy-MM-dd}, Timestamp: {dateOfBirthTimestamp}");
                }

                // CRITICAL: Make sure ReservationsID is included as an empty array
                var userData = new Dictionary<string, object>
        {
            { "FirstName", firstName },
            { "LastName", lastName },
            { "UserName", username },
            { "EMail", email },
            { "PhoneNumber", phoneNumber },
            { "DateOfBirth", dateOfBirthTimestamp },
            { "Gender", gender },
            { "Role", "User" },
            { "Rate", 0 },
            { "ReservationsID", new List<string>() },  
            { "CreatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            { "UserId", _currentUser.Uid }
        };

                Debug.WriteLine("Saving user data to database...");
                Debug.WriteLine($"ReservationsID exists: {userData.ContainsKey("ReservationsID")}");

                // Save user data
                await _databaseClient!
                    .Child("users")
                    .Child(_currentUser.Uid)
                    .PutAsync(userData);

                Debug.WriteLine("✅ User data saved successfully");

                // Save username index
                var usernameData = new Dictionary<string, object>
        {
            { "userId", _currentUser.Uid },
            { "username", username },
            { "createdAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
        };

                await _databaseClient!
                    .Child("usernames")
                    .Child(username.ToLower())
                    .PutAsync(usernameData);

                Debug.WriteLine("✅ Username index saved successfully");
                return true;
            }
            catch (FirebaseAuthException ex)
            {
                Debug.WriteLine($"❌ Firebase auth error: {ex.Message}");
                Debug.WriteLine($"Reason: {ex.Reason}");

                if (ex.Message.Contains("EMAIL_EXISTS"))
                {
                    throw new Exception("This email is already registered. Please use another email or login.");
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Registration error: {ex.Message}");
                throw;
            }
        }
        private async Task<bool> CheckUsernameExists(string username)
        {
            try
            {
                if (_databaseClient == null)
                    return false;

                var usernameData = await _databaseClient
                    .Child("usernames")
                    .Child(username.ToLower())
                    .OnceSingleAsync<Dictionary<string, object>>();

                return usernameData != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking username: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> LoginUser(string email, string password)
        {
            Debug.WriteLine($"🔐 LoginUser called for email: {email}");

            try
            {
                if (_authClient == null)
                {
                    Debug.WriteLine("❌ AuthClient is null");
                    return false;
                }

                var userCredential = await _authClient.SignInWithEmailAndPasswordAsync(email, password);
                _currentUser = userCredential.User;
                _currentUserId = _currentUser.Uid; // Set the private backing field

                // Get the ID token from the user
                var idToken = await _currentUser.GetIdTokenAsync();
                var refreshToken = TryGetRefreshToken(userCredential);

                SetAuthSession(
                    userId: _currentUser.Uid,
                    idToken: idToken,
                    refreshToken: refreshToken,
                    expiresInSeconds: 3600 // Default to 1 hour
                );

                Debug.WriteLine($"✅ User logged in with UID: {_currentUser.Uid}");
                return true;
            }
            catch (FirebaseAuthException ex)
            {
                Debug.WriteLine($"❌ Firebase auth error: {ex.Message}");
                Debug.WriteLine($"Reason: {ex.Reason}");

                if (ex.Message.Contains("INVALID_LOGIN_CREDENTIALS"))
                {
                    throw new Exception("Invalid email or password.");
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Login error: {ex.Message}");
                return false;
            }
        }

        public void Logout()
        {
            Debug.WriteLine("🚪 Logout called");
            _authClient?.SignOut();
            ClearAuthSession();
        }

        public async Task<Dictionary<string, object>?> GetUserProfile(string userId)
        {
            try
            {
                if (_databaseClient == null)
                {
                    // try to (re)initialize database client
                    try
                    {
                        _databaseClient = new FirebaseClient(
                            FirebaseDatabaseUrl,
                            new FirebaseOptions
                            {
                                AuthTokenAsyncFactory = async () => await GetIdTokenAsync()
                            });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ GetUserProfile: Could not init DB client: {ex.Message}");
                        return null;
                    }
                }

                if (string.IsNullOrEmpty(userId))
                {
                    Debug.WriteLine("❌ GetUserProfile: UserId is null or empty");
                    return null;
                }

                Debug.WriteLine($"📋 GetUserProfile: Attempting to get profile for user {userId}");
                Debug.WriteLine($"🔐 Current authenticated user: {_currentUserId}");

                var userData = await _databaseClient
                    .Child("users")
                    .Child(userId)
                    .OnceSingleAsync<Dictionary<string, object>>();

                if (userData == null)
                {
                    Debug.WriteLine($"⚠️ GetUserProfile: No data found for user {userId}");

                    // Try to create basic profile if it doesn't exist and it's the current user
                    if (userId == _currentUserId && !string.IsNullOrEmpty(CurrentUserEmail))
                    {
                        Debug.WriteLine($"🔄 Attempting to create missing user profile for {userId}");

                        var basicUserData = new Dictionary<string, object>
                        {
                            { "UserId", userId },
                            { "EMail", CurrentUserEmail },
                            { "UserName", CurrentUserEmail.Split('@')[0] },
                            { "FirstName", "" },
                            { "LastName", "" },
                            { "PhoneNumber", "" },
                            { "Role", "User" },
                            { "Rate", 0 },
                            { "ReservationsID", new List<string>() },
                            { "CreatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
                        };

                        await _databaseClient
                            .Child("users")
                            .Child(userId)
                            .PutAsync(basicUserData);

                        Debug.WriteLine($"✅ Created missing user profile for {userId}");
                        return basicUserData;
                    }
                }
                else
                {
                    Debug.WriteLine($"✅ GetUserProfile: Successfully retrieved profile for {userId}");
                }

                return userData;
            }
            catch (FirebaseException ex)
            {
                Debug.WriteLine($"❌ GetUserProfile Firebase error: {ex.Message}");

                if (ex.Message.Contains("Permission denied"))
                {
                    Debug.WriteLine("🔑 This is a permissions error. Check Firebase rules.");
                    Debug.WriteLine($"   Current user: {_currentUserId}, Requested user: {userId}");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ GetUserProfile general error: {ex.Message}");
                return null;
            }
        }

        private class FirebaseRefreshResponse
        {
            [JsonPropertyName("id_token")]
            public string? IdToken { get; set; }

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("expires_in")]
            public string? ExpiresIn { get; set; }

            [JsonPropertyName("user_id")]
            public string? UserId { get; set; }

            [JsonPropertyName("project_id")]
            public string? ProjectId { get; set; }
        }

        private static string TryGetRefreshToken(object? userCredential)
        {
            if (userCredential == null)
                return string.Empty;

            try
            {
                var credential = userCredential.GetType().GetProperty("Credential")?.GetValue(userCredential);
                var refreshToken = credential?.GetType().GetProperty("RefreshToken")?.GetValue(credential) as string;
                return refreshToken ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}