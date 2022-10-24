using System.Net;
using System.Security.Cryptography.X509Certificates;
using NLog.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace SourceLinkGitLabProxy;

public class Program {
	private static readonly string AppSettingsFilenameBase = "appsettings";

	public static void Main(string[] args) {
		CreateHostBuilder(args)
			.Build()
			.Run();
	}

	public static IHostBuilder CreateHostBuilder(string[] args) =>
		Host
			.CreateDefaultBuilder(args)
			.ConfigureAppConfiguration((context, builder) => {
				builder.AddYamlFile(Path.Join(context.HostingEnvironment.ContentRootPath, $"{AppSettingsFilenameBase}.yml"));
				builder.AddYamlFile(Path.Join(context.HostingEnvironment.ContentRootPath, $"{AppSettingsFilenameBase}.{context.HostingEnvironment.EnvironmentName}.yml"), optional: true);
			})
			.ConfigureLogging(loggingBuilder => loggingBuilder.AddNLog())
			.ConfigureWebHostDefaults(webBuilder => {
				webBuilder
						.UseKestrel(options => options.ConfigureEndpoints())
						.UseStartup<Startup>();
			});
}

// Most of the following code was copied from:
// https://devblogs.microsoft.com/dotnet/configuring-https-in-asp-net-core-across-different-platforms/

public static class KestrelServerOptionsExtensions {
	public static void ConfigureEndpoints(this KestrelServerOptions options) {
		var configuration = options.ApplicationServices.GetRequiredService<IConfiguration>();
		var environment = options.ApplicationServices.GetRequiredService<IHostEnvironment>();

		var endpoints = configuration.GetSection("HttpServer:Endpoints")
			.GetChildren()
			.ToDictionary(section => section.Key, section => {
				var endpoint = new EndpointConfiguration();
				section.Bind(endpoint);
				return endpoint;
			});

		foreach (var endpoint in endpoints.Values) {
			var config = endpoint;
			var port = config.Port ?? (config.Scheme == "https" ? 443 : 80);
			static IReadOnlyCollection<IPAddress> getIPAddresses(string host) {
				if (host == "localhost")
					return new[] { IPAddress.IPv6Loopback, IPAddress.Loopback };
				else if (IPAddress.TryParse(host, out var address))
					return new[] { address };
				return new[] { IPAddress.IPv6Any };
			}

			foreach (var address in getIPAddresses(config.Host))
				options.Listen(address, port,
						listenOptions => {
							if (config.Scheme == "https") {
								var certificate = LoadCertificate(config, environment);
								listenOptions.UseHttps(certificate);
							}
						});
		}
	}

	private static X509Certificate2 LoadCertificate(EndpointConfiguration config, IHostEnvironment environment) {
		if (!string.IsNullOrEmpty(config.StoreName) && !string.IsNullOrEmpty(config.StoreLocation)) {
			using var store = new X509Store(config.StoreName, Enum.Parse<StoreLocation>(config.StoreLocation));
			store.Open(OpenFlags.ReadOnly);
			var certificate = store.Certificates.Find(
					X509FindType.FindBySubjectName,
					config.Host,
					validOnly: !environment.IsDevelopment());
			var returnCertificate = certificate.FirstOrDefault();
			return returnCertificate ?? throw new InvalidOperationException($"Certificate not found for {config.Host}.");
		}
		if (!string.IsNullOrEmpty(config.FilePath) && !string.IsNullOrEmpty(config.Password))
			return new X509Certificate2(config.FilePath, config.Password);
		throw new InvalidOperationException("No valid certificate configuration found for the current endpoint.");
	}
}

public class EndpointConfiguration {
	public string Host { get; set; } = string.Empty;
	public int? Port { get; set; }
	public string Scheme { get; set; } = string.Empty;
	public string StoreName { get; set; } = string.Empty;
	public string StoreLocation { get; set; } = string.Empty;
	public string FilePath { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
}