using System;
using System.IO;

namespace MediaVault.Services
{
    public static class EnvService
    {
        private static bool _isLoaded;

        public static void Load()
        {
            if (_isLoaded) return;

            try
            {
                string envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");

                if (File.Exists(envPath))
                {
                    DotNetEnv.Env.Load(envPath);
                }
                else
                {
                    string projectRoot = Path.GetFullPath(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\"));

                    string devEnvPath = Path.Combine(projectRoot, ".env");

                    if (File.Exists(devEnvPath))
                    {
                        DotNetEnv.Env.Load(devEnvPath);
                    }
                }

                _isLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка завантаження .env: {ex.Message}");
            }
        }

        public static string Get(string key, string defaultValue = null)
        {
            if (!_isLoaded)
                Load();

            string value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }
    }
}