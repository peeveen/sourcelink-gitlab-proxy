using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SourceLinkGitLabProxy.Controllers;

namespace SourceLinkGitLabProxy.Test;

[TestClass]
public class Tests {
	// Run a fake "GitLab" server locally, at this URL.
	private static readonly int FakeGitLabPort = 6626;
	private static readonly string FakeGitLabURL = $"http://localhost:{FakeGitLabPort}";

	// Simple Basic Authentication token calculator.
	private static string CreateBasicAuthenticationToken(string username, string password) => $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"))}";

	// Our fake GitLab instance.
	internal class FakeGitLab {
		// OAuth requests using the "resource owner password credentials flow" will only be accepted for this username/password combo.
		internal static readonly string TestUserName = "PresidentMargaret";
		internal static readonly string TestUserPassword = "Vesta";

		// These are the OAuth access tokens that we will issue.
		internal readonly IReadOnlyList<string> KnownOAuthTokens = new List<string> { "Essential12939Supply", "NeutronFuel", "Poweramp", "Cheese", "Cobweb", "DominionDart" };
		// Counter on which token to issue.
		internal int OAuthTokenCounter = 0;
		// And we will keep track of which ones have been issued here.
		internal readonly List<string> IssuedOAuthTokens = new List<string>();

		// Requests using a PAT will only be accepted if this is the supplied PAT.
		internal static readonly string KnownPersonalAccessToken = "PalyarCommander'sBrotherInLaw";

		// Some fake source code to return.
		internal static readonly string UnixFakeSourceFile = "10 PRINT \"STEVEN IS COOL\"\n20 GOTO 10";
		internal static readonly string WindowsFakeSourceFile = UnixFakeSourceFile.ReplaceLineEndings("\r\n");

		// A cancellation token to stop the fake server.
		private CancellationTokenSource CancelToken { get; } = new CancellationTokenSource();

		// Simple true/false authorization check. If the given request contains an appropriate header with an
		// appropriate access token, then it'll return true.
		bool IsAuthorized(HttpRequest request) {
			var personalAccessToken = request.Headers[AuthorizationInfo.GitLabPrivateTokenHeaderName].FirstOrDefault();
			if (personalAccessToken == KnownPersonalAccessToken)
				return true;
			var oAuthToken = request.Headers[HeaderNames.Authorization].FirstOrDefault()?.Replace("Bearer ", string.Empty);
			return oAuthToken != null && IssuedOAuthTokens.Contains(oAuthToken);
		}

		// Responds to the /oauth/token endpoint. POSTed data should be a username and password in JSON.
		// Returns some access tokens if they match our expected username/password combo.
		async Task getOAuthTokens(HttpRequest request, HttpResponse response, string requestJson) {
			var tokenRequest = JsonSerializer.Deserialize<GitLabTokenRequest>(requestJson);
			if (tokenRequest != null)
				if (tokenRequest.Username == TestUserName && tokenRequest.Password == TestUserPassword) {
					var accessTokenToIssue = KnownOAuthTokens[OAuthTokenCounter++];
					OAuthTokenCounter %= KnownOAuthTokens.Count();
					IssuedOAuthTokens.Add(accessTokenToIssue);
					var tokens = new GitLabOAuthTokens {
						AccessToken = accessTokenToIssue,
						Scope = tokenRequest.Scope,
						TokenType = "bearer",
						RefreshToken = "this_aint_gonna_get_used",
						CreatedAt = (ulong)DateTime.Now.Ticks
					};
					await response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tokens)));
				} else
					response.StatusCode = (int)HttpStatusCode.Unauthorized;
			else
				response.StatusCode = (int)HttpStatusCode.BadRequest;
		}

		// Responds to the /api/v4/projects/... endpoint, by returning some source code.
		async Task getSourceCode(HttpRequest request, HttpResponse response, string requestJson) {
			if (IsAuthorized(request))
				await response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(UnixFakeSourceFile));
			else
				response.StatusCode = (int)HttpStatusCode.Unauthorized;
		}

		// Simple request handler.
		static void HandleRequest(IApplicationBuilder app, string permittedMethod, Func<HttpRequest, HttpResponse, string, Task> fn) {
			app.Run(async context => {
				if (context.Request.Method == permittedMethod) {
					string requestJson = string.Empty;
					using (var reader = new StreamReader(context.Request.Body)) {
						requestJson = await reader.ReadToEndAsync();
					}
					await fn(context.Request, context.Response, requestJson);
				} else
					context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
			});
		}

		// Removes the given access token from the list of OAuth tokens, effectively "revoking" it.
		internal void RevokeOAuthToken(string token) {
			IssuedOAuthTokens.Remove(token);
		}

		internal FakeGitLab() {
			var cancellationToken = CancelToken.Token;
			var providerTask = Task.Run(async () => {
				var builder = WebApplication.CreateBuilder();
				var gitLabApp = builder.Build();
				gitLabApp.Map("/oauth/token", app => HandleRequest(app, "POST", getOAuthTokens));
				gitLabApp.Map("/api/v4/projects", app => HandleRequest(app, "GET", getSourceCode));
				cancellationToken.Register(() => {
					gitLabApp.StopAsync();
				});
				gitLabApp.Run(FakeGitLabURL);
				await gitLabApp.StopAsync();
			});
		}

		internal void Stop() {
			CancelToken.Cancel();
		}
	}

	private HttpClient GetTestClient(params string[] args) {
		var hostBuilder = Host
			.CreateDefaultBuilder(args)
			.ConfigureWebHostDefaults(webBuilder => webBuilder.UseTestServer().UseStartup<SourceLinkGitLabProxy.Startup>());
		var testServer = hostBuilder.Start();
		return testServer.GetTestClient();
	}

	private void WithClient(Action<HttpClient, FakeGitLab> testFunc, int refreshDelaySeconds = 0, params string[] additionalArgs) {
		Task.WaitAll(WithClientAsync(async (client, rulesProvider) => await Task.Run(() => testFunc(client, rulesProvider)), additionalArgs));
	}

	private async Task WithClientAsync(Func<HttpClient, FakeGitLab, Task> testFunc, params string[] additionalArgs) {
		var rulesProvider = new FakeGitLab();
		try {
			var client = GetTestClient(additionalArgs);
			await testFunc(client, rulesProvider);
		} finally {
			rulesProvider.Stop();
		}
	}

	[TestMethod]
	public void TestURLParse() {
		var projectPath = "steven.frew/someproject";
		var commitHash = "09ef7F892345";
		var filePath = "blah/yap/folder/file.ext";
		var parseResult = GitLabController.ParseURL($"/{projectPath}/raw/{commitHash}/{filePath}");
		Assert.AreEqual(projectPath, parseResult.projectPath);
		Assert.AreEqual(commitHash, parseResult.commitHash);
		Assert.AreEqual(filePath, parseResult.filePath);
	}

	private async Task ValidateSourceCodeResponse(HttpResponseMessage response, string expectedSourceCodeResult) {
		response.EnsureSuccessStatusCode();
		using var content = response.Content;
		var responseBody = await content.ReadAsStringAsync();
		Assert.AreEqual(expectedSourceCodeResult, responseBody);
	}

	private async Task TestGetSource(HttpClient client, Func<HttpResponseMessage, Task> responseValidator, Action<HttpRequestMessage>? requestDecorator = null) {
		// This exact path doesn't matter, so long as it's in the correct "form".
		using var request = new HttpRequestMessage(HttpMethod.Get, "/path/to/my/project/raw/342923974a8678d8787e87f78b8c/path/to/my/file");
		requestDecorator?.Invoke(request);
		using var response = await client.SendAsync(request);
		await responseValidator(response);
	}

	private async Task TestGetSource(HttpClient client, Action<HttpResponseMessage> responseValidator, Action<HttpRequestMessage>? requestDecorator = null) {
		await TestGetSource(client, async (resp) => await Task.Run(() => responseValidator(resp)), requestDecorator);
	}

	private Func<HttpResponseMessage, Task> GetSourceCodeValidator(string expectedCode) => async (resp) => await ValidateSourceCodeResponse(resp, expectedCode);

	private void AddBasicAuthenticationHeader(HttpRequestMessage req) => req.Headers.Add(HeaderNames.Authorization, CreateBasicAuthenticationToken(FakeGitLab.TestUserName, FakeGitLab.TestUserPassword));

	[TestMethod]
	public async Task TestGetSourceWindows() {
		await WithClientAsync(async (client, fakeGitLab) => {
			await TestGetSource(client, GetSourceCodeValidator(FakeGitLab.WindowsFakeSourceFile));
		}, $"--{ProxyConfig.GitLabHostOriginArgumentName}={FakeGitLabURL}", $"--{ProxyConfig.PersonalAccessTokenArgumentName}={FakeGitLab.KnownPersonalAccessToken}");
	}

	[TestMethod]
	public async Task TestGetSourceUnix() {
		await WithClientAsync(async (client, fakeGitLab) => {
			await TestGetSource(client, GetSourceCodeValidator(FakeGitLab.UnixFakeSourceFile));
		}, $"--{ProxyConfig.GitLabHostOriginArgumentName}={FakeGitLabURL}", $"--{ProxyConfig.PersonalAccessTokenArgumentName}={FakeGitLab.KnownPersonalAccessToken}", $"--{ProxyConfig.LineEndingChangeArgumentName}={LineEndingChange.Unix}");
	}

	[TestMethod]
	public async Task TestGetSourceWithBadPersonalAccessToken() {
		await WithClientAsync(async (client, fakeGitLab) => {
			await TestGetSource(client, (resp) => Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode));
		}, $"--{ProxyConfig.GitLabHostOriginArgumentName}={FakeGitLabURL}", $"--{ProxyConfig.PersonalAccessTokenArgumentName}=Ghettoblaster", $"--{ProxyConfig.LineEndingChangeArgumentName}={LineEndingChange.Unix}");
	}

	[TestMethod]
	public async Task TestGetSourceWithOAuth() {
		await WithClientAsync(async (client, fakeGitLab) => {
			await TestGetSource(client, GetSourceCodeValidator(FakeGitLab.WindowsFakeSourceFile), AddBasicAuthenticationHeader);
			Assert.AreEqual(1, fakeGitLab.IssuedOAuthTokens.Count());
		}, $"--{ProxyConfig.GitLabHostOriginArgumentName}={FakeGitLabURL}");
	}

	[TestMethod]
	public async Task TestGetSourceWithOAuthAfterRevokingAccessToken() {
		await WithClientAsync(async (client, fakeGitLab) => {
			await TestGetSource(client, GetSourceCodeValidator(FakeGitLab.WindowsFakeSourceFile), AddBasicAuthenticationHeader);
			// Revoke the issued OAuth token.
			Assert.AreEqual(1, fakeGitLab.IssuedOAuthTokens.Count());
			fakeGitLab.RevokeOAuthToken(fakeGitLab.IssuedOAuthTokens.First());
			await TestGetSource(client, GetSourceCodeValidator(FakeGitLab.WindowsFakeSourceFile), AddBasicAuthenticationHeader);
			// We should be onto the second OAuth token now.
			Assert.AreEqual(2, fakeGitLab.OAuthTokenCounter);
			Assert.AreEqual(fakeGitLab.KnownOAuthTokens[1], fakeGitLab.IssuedOAuthTokens[0]);
		}, $"--{ProxyConfig.GitLabHostOriginArgumentName}={FakeGitLabURL}");
	}

	[TestMethod]
	public async Task TestGetSourceWithBadOAuthCredentials() {
		await WithClientAsync(async (client, fakeGitLab) => {
			await TestGetSource(client, (resp) => Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode), (req) => req.Headers.Add(HeaderNames.Authorization, CreateBasicAuthenticationToken("PaulWoakes", "Electromagnet")));
		}, $"--{ProxyConfig.GitLabHostOriginArgumentName}={FakeGitLabURL}");
	}
}