using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace Neonix.Services
{
    public class Account
    {
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Salt { get; set; } = "";
        public string Hash { get; set; } = "";
        public int Iterations { get; set; }
        public DateTime Created { get; set; }
    }

    /// <summary>
    /// Local accounts. They live on this PC only (%AppData%\Neonix\accounts.json) and keep people's
    /// profiles separate. Passwords are never stored: only a salted PBKDF2-SHA256 hash.
    /// There is no cloud sync and no password recovery.
    /// </summary>
    public static class AccountService
    {
        const int Iterations = 210_000;
        const int SaltBytes = 16;
        const int HashBytes = 32;

        static readonly string FilePath = Path.Combine(SettingsStore.DataDirectory, "accounts.json");
        static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
        static List<Account> _accounts = new();
        static int _failedAttempts;

        public static event Action? Changed;

        public static Account? Current =>
            _accounts.FirstOrDefault(a => a.Email == SettingsStore.Current.SignedInEmail);

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    _accounts = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(FilePath), Options) ?? new();
            }
            catch { _accounts = new(); }

            // Signed-in account was deleted behind our back.
            if (SettingsStore.Current.SignedInEmail != null && Current == null)
            {
                SettingsStore.Current.SignedInEmail = null;
                SettingsStore.Save();
            }
        }

        static void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsStore.DataDirectory);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(_accounts, Options));
            }
            catch { /* best effort */ }
        }

        // ───────── validation ─────────

        public static string? ValidateEmail(string email)
        {
            email = email.Trim();
            if (email.Length == 0) return "Enter your email address.";
            if (email.Length > 254) return "That email address is too long.";
            try
            {
                var addr = new MailAddress(email);
                if (addr.Address != email || !addr.Host.Contains('.')) return "That doesn't look like a valid email address.";
            }
            catch { return "That doesn't look like a valid email address."; }
            return null;
        }

        public static string? ValidatePassword(string password, string email = "")
        {
            if (password.Length < 8) return "Use at least 8 characters.";
            if (password.Length > 128) return "Use 128 characters or fewer.";
            if (password.Distinct().Count() < 4) return "That password is too repetitive.";
            if (email.Length > 0 && password.Equals(email, StringComparison.OrdinalIgnoreCase))
                return "Your password can't be your email address.";
            return null;
        }

        /// <summary>Rough 0-100 strength score for the on-screen meter.</summary>
        public static int Strength(string p)
        {
            if (p.Length == 0) return 0;
            int score = Math.Min(p.Length, 16) * 4;                 // up to 64 for length
            int classes = 0;
            if (p.Any(char.IsLower)) classes++;
            if (p.Any(char.IsUpper)) classes++;
            if (p.Any(char.IsDigit)) classes++;
            if (p.Any(c => !char.IsLetterOrDigit(c))) classes++;
            score += classes * 9;                                     // up to 36
            if (p.Distinct().Count() < 5) score = Math.Min(score, 25);
            return Math.Clamp(score, 0, 100);
        }

        // ───────── hashing ─────────

        static byte[] Derive(string password, byte[] salt, int iterations) =>
            Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);

        static (string Salt, string Hash) NewHash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            return (Convert.ToBase64String(salt), Convert.ToBase64String(Derive(password, salt, Iterations)));
        }

        static bool Matches(Account a, string password)
        {
            var expected = Convert.FromBase64String(a.Hash);
            var actual = Derive(password, Convert.FromBase64String(a.Salt), a.Iterations);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        // ───────── actions ─────────

        public static (bool Ok, string? Error) SignUp(string email, string displayName, string password)
        {
            email = email.Trim().ToLowerInvariant();
            displayName = displayName.Trim();

            var err = ValidateEmail(email) ?? ValidatePassword(password, email);
            if (err != null) return (false, err);
            if (displayName.Length == 0) displayName = email.Split('@')[0];
            if (displayName.Length > 40) return (false, "Keep your name to 40 characters or fewer.");
            if (_accounts.Any(a => a.Email == email)) return (false, "An account with that email already exists on this PC.");

            var (salt, hash) = NewHash(password);
            _accounts.Add(new Account
            {
                Email = email, DisplayName = displayName, Salt = salt, Hash = hash,
                Iterations = Iterations, Created = DateTime.UtcNow,
            });
            Save();
            SetSignedIn(email);
            return (true, null);
        }

        public static async Task<(bool Ok, string? Error)> SignInAsync(string email, string password)
        {
            email = email.Trim().ToLowerInvariant();
            if (email.Length == 0 || password.Length == 0) return (false, "Enter your email and password.");

            var account = _accounts.FirstOrDefault(a => a.Email == email);
            bool ok;
            if (account != null) ok = await Task.Run(() => Matches(account, password));
            else
            {
                // Do the same amount of work so timing doesn't reveal which emails exist.
                await Task.Run(() => Derive(password, new byte[SaltBytes], Iterations));
                ok = false;
            }

            if (ok)
            {
                _failedAttempts = 0;
                SetSignedIn(email);
                return (true, null);
            }

            _failedAttempts++;
            await Task.Delay(Math.Min(_failedAttempts, 6) * 600); // slows down guessing
            return (false, "Incorrect email or password.");
        }

        public static void SignOut() => SetSignedIn(null);

        public static async Task<(bool Ok, string? Error)> ChangePasswordAsync(string oldPassword, string newPassword)
        {
            var a = Current;
            if (a == null) return (false, "You're not signed in.");
            if (!await Task.Run(() => Matches(a, oldPassword))) return (false, "Your current password is wrong.");
            var err = ValidatePassword(newPassword, a.Email);
            if (err != null) return (false, err);

            var (salt, hash) = await Task.Run(() => NewHash(newPassword));
            a.Salt = salt; a.Hash = hash; a.Iterations = Iterations;
            Save();
            return (true, null);
        }

        public static void DeleteCurrent()
        {
            var a = Current;
            if (a == null) return;
            // Their private profiles go with the account.
            SettingsStore.Current.Profiles.RemoveAll(p => p.Owner == a.Email);
            _accounts.Remove(a);
            Save();
            SetSignedIn(null);
        }

        static void SetSignedIn(string? email)
        {
            SettingsStore.Current.SignedInEmail = email;
            SettingsStore.Save();
            SettingsStore.EnsureVisibleActive();
            Changed?.Invoke();
        }
    }
}
