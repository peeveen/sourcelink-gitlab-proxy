using System.Net.Mime;
using System.Text;
using System.Text.Json;

namespace SourceLinkGitLabProxy;

public class GitLabClient : IGitLabClient {
	private HttpClient _httpClient;
	private ILogger _logger;
	private IProxyConfig _config;

	public GitLabClient(IProxyConfig config, ILoggerFactory loggerFactory, HttpClient httpClient) {
		_httpClient = httpClient;
		_logger = loggerFactory.CreateLogger<GitLabClient>();
		_config = config;
	}

	// Calls GitLab to generate OAuth access tokens.
	private async Task<GitLabOAuthTokens> GenerateOAuthTokens(string username, string password) {
		using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_httpClient.BaseAddress!, "/oauth/token"));
		var tokenRequest = new GitLabTokenRequest { Username = username, Password = password, Scope = _config.OAuthTokenRequestScope };
		_logger.LogInformation($"Sending request for OAuth access token for user '{username}' ...");
		var tokenRequestJson = JsonSerializer.Serialize(tokenRequest);
		request.Content = new StringContent(tokenRequestJson, Encoding.UTF8, MediaTypeNames.Application.Json);
		using var response = await _httpClient.SendAsync(request);
		if (response.IsSuccessStatusCode) {
			using var responseContent = response.Content;
			var jsonResponse = await responseContent.ReadAsStringAsync();
			var tokens = JsonSerializer.Deserialize<GitLabOAuthTokens>(jsonResponse);
			_logger.LogInformation($"Access token successfully retrieved.");
			return tokens;
		}
		_logger.LogError($"Failed to generate OAuth tokens from the given username & password. Status code from server was {response.StatusCode}.");
		// Return empty tokens. This will generate a 401 down the line.
		return new GitLabOAuthTokens();
	}

	public async Task<HttpResponseMessage> GetSourceAsync(string gitLabURL, AuthorizationInfo authInfo) {
		_logger.LogInformation($"Translated to GitLab request: {gitLabURL}");

		async Task<(HttpResponseMessage, bool)> getResponse() {
			using var request = new HttpRequestMessage(HttpMethod.Get, gitLabURL);
			authInfo = await authInfo.AuthorizeRequest(request, GenerateOAuthTokens);
			var response = await _httpClient.SendAsync(request);
			// If the response is Unauthorized, and the token came from the cache, then we can suggest a retry.
			var retry = response.StatusCode == System.Net.HttpStatusCode.Unauthorized && authInfo.OAuthTokensCameFromCache;
			return (response, retry);
		}
		var (response, retry) = await getResponse();
		// If advised to retry, the token must have been old, so possibly expired.
		if (retry) {
			authInfo = authInfo.InvalidateCachedOAuthTokens();
			response.Dispose();
			(response, retry) = await getResponse();
		}
		return response;
	}
}
