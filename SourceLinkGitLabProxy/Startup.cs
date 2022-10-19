using NLog.Extensions.Logging;
using Opw.HttpExceptions.AspNetCore;

namespace SourceLinkGitLabProxy;

public class Startup {
	internal static readonly string AccessTokenParameterName = "AccessToken";
	internal static readonly string GitLabHostOriginParameterName = "GitLabHostOrigin";

	public IConfiguration Configuration { get; }
	public bool IsDevelopment { get; }

	public Startup(IConfiguration configuration, IWebHostEnvironment env) {
		Configuration = configuration;
		IsDevelopment = env.IsDevelopment();
	}

	// This method gets called by the runtime. Use this method to add services to the container.
	public void ConfigureServices(IServiceCollection services) {
		static void validateArgument(string name, string value) {
			if (string.IsNullOrEmpty(value))
				throw new Exception($"No value was provided for the configuration property '{name}'.");
		}
		string getArgument(string argName, string defaultValue = "") => (Configuration[argName] ?? defaultValue).Trim();

		var gitLabHostOrigin = getArgument(GitLabHostOriginParameterName);
		var accessToken = getArgument(AccessTokenParameterName);
		validateArgument(nameof(gitLabHostOrigin), gitLabHostOrigin);

		services.AddLogging(builder => builder.AddNLog());
		services.AddHttpClient<IGitLabClient, GitLabClient>(client => {
			client.BaseAddress = new Uri(gitLabHostOrigin);
			if (!string.IsNullOrEmpty(accessToken))
				client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", accessToken);
		});
		services.AddControllersWithViews();
		services.AddMvc().AddHttpExceptions(options => {
			// Always include exception details in dev mode.
			// Otherwise simple message will do.
			options.IncludeExceptionDetails = context => IsDevelopment;
			options.IsExceptionResponse = context => {
				if (context.Response.StatusCode < 400 || context.Response.StatusCode >= 600)
					return false;
				if (context.Response.ContentLength.HasValue)
					return false;
				if (string.IsNullOrEmpty(context.Response.ContentType))
					return true;
				return false;
			};
		});
		services.AddRouting();
	}

	// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
	public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory) {
		app.UseHttpExceptions();
		app.UseRouting();
		if (IsDevelopment)
			// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
			app.UseHsts();
		app.UseEndpoints(endpoints => {
			endpoints.MapDefaultControllerRoute();
		});
		var logger = loggerFactory.CreateLogger<Startup>();
	}
}
