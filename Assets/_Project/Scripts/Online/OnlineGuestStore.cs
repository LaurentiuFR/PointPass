using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PointPass.Online
{
    /// <summary>Windows pilot credentials are protected for the current OS user with DPAPI.</summary>
    public static class OnlineGuestStore
    {
        [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }
        [DllImport("crypt32.dll", SetLastError = true)] private static extern bool CryptProtectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("crypt32.dll", SetLastError = true)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr data);

        public static string LoadOrCreate(string profile)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer)
                throw new PlatformNotSupportedException("Online guest storage is currently validated on Windows only.");
            if (string.IsNullOrEmpty(profile) || profile.Length > 24) throw new ArgumentException("Invalid test profile.");
            foreach (char letter in profile) if (!char.IsLetterOrDigit(letter) && letter != '-') throw new ArgumentException("Invalid test profile.");
            string directory = Path.Combine(Application.persistentDataPath, "OnlinePilot");
            string path = Path.Combine(directory, profile + ".protected");
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > 4096) throw new IOException("Guest profile is damaged.");
                string credential = Encoding.UTF8.GetString(Transform(File.ReadAllBytes(path), false));
                if (credential.Length != 43) throw new IOException("Guest profile is damaged.");
                return credential;
            }
            byte[] random = new byte[32]; using (RandomNumberGenerator generator = RandomNumberGenerator.Create()) generator.GetBytes(random);
            string created = Convert.ToBase64String(random).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Directory.CreateDirectory(directory);
            byte[] encrypted = Transform(Encoding.UTF8.GetBytes(created), true);
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) file.Write(encrypted, 0, encrypted.Length);
            return created;
        }

        private static byte[] Transform(byte[] data, bool protect)
        {
            var input = new Blob { Length = data.Length, Data = Marshal.AllocHGlobal(data.Length) };
            Blob output = default;
            try
            {
                Marshal.Copy(data, 0, input.Data, data.Length);
                bool success = protect ? CryptProtectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
                if (!success) throw new IOException("Windows could not protect or unlock this guest profile.");
                var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
            }
            finally { Marshal.FreeHGlobal(input.Data); if (output.Data != IntPtr.Zero) LocalFree(output.Data); }
        }
    }
}
