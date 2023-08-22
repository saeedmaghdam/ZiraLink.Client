using System.Reflection;
using Microsoft.Extensions.Configuration.Json;

namespace ZiraLink.Client
{
    public class CustomConfigurationProvider : ConfigurationProvider, IConfigurationProvider
    {
        private const string ENCRYPTION_KEY = "";

        public override void Load()
        {
            var pathToExe = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);

            var allConfiguration = new ConfigurationBuilder()
                .SetBasePath(pathToExe)
                .AddJsonFile("appsettings.json", false, true)
                .AddEnvironmentVariables()
                .Build();
            var environment = allConfiguration["ASPNETCORE_ENVIRONMENT"];
            if (string.IsNullOrEmpty(environment))
                environment = "Production";

            var configuration = new ConfigurationBuilder()
                .SetBasePath(pathToExe)
                .AddJsonFile($"appsettings.{environment}.json", false, true)
                .Build();

            var isProduction = environment == "Production";
            if (isProduction && string.IsNullOrEmpty(ENCRYPTION_KEY))
                throw new ApplicationException("Encryption key is empty");

            // Encrypt the configuration values.
            var root = configuration as ConfigurationRoot;
            if (root != null)
            {
                var appSettingsProvider = root.Providers.FirstOrDefault() as JsonConfigurationProvider;

                if (appSettingsProvider == null)
                    throw new ApplicationException("Settings are not correct");

                var dataElement = appSettingsProvider.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.NonPublic);
                var dataValue = (Dictionary<string, string>)dataElement!.GetValue(appSettingsProvider)!;
                foreach (var item in dataValue)
                {
                    if (isProduction && item.Key.StartsWith("ZIRALINK"))
                        Data.Add(item.Key, EncryptionHelper.DecryptWithPrivateKey(ENCRYPTION_KEY, item.Value));
                    else
                        Data.Add(item.Key, item.Value);
                }

            }
        }
    }
}
