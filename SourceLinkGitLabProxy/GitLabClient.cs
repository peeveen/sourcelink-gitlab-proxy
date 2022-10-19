using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace SourceLinkGitLabProxy;

public class GitLabClient : IGitLabClient {
	private HttpClient _httpClient;
	private ILogger _logger;
	private IDictionary<string, string> _accessTokens = new ConcurrentDictionary<string, string>();

	internal struct GitLabAccessTokens {
		[JsonInclude]
		[JsonPropertyName("access_token")]
		public string AccessToken { get; init; }
		[JsonInclude]
		[JsonPropertyName("refresh_token")]
		public string RefreshToken { get; init; }
		[JsonInclude]
		[JsonPropertyName("token_type")]
		public string TokenType { get; init; }
		[JsonInclude]
		[JsonPropertyName("scope")]
		public string Scope { get; init; }
		[JsonInclude]
		[JsonPropertyName("created_at")]
		public ulong CreatedAt { get; init; }
	}

	internal record GitLabTokenRequest {
		[JsonInclude]
		[JsonPropertyName("grant_type")]
		public string GrantType { get; } = "password";
		[JsonInclude]
		[JsonPropertyName("username")]
		public string Username { get; init; } = string.Empty;
		[JsonInclude]
		[JsonPropertyName("password")]
		public string Password { get; init; } = string.Empty;
		[JsonInclude]
		[JsonPropertyName("scope")]
		public string Scope { get; } = "api";
	}

	public GitLabClient(ILoggerFactory loggerFactory, HttpClient httpClient) {
		_httpClient = httpClient;
		_logger = loggerFactory.CreateLogger<GitLabClient>();
	}

	private async Task<(string?, bool)> GetAccessToken(string authToken, bool canUseCache = true) {
		var authParts = authToken.Split(':');
		var username = authParts.First();
		string? accessToken = null;
		var cached = canUseCache && _accessTokens.TryGetValue(username, out accessToken);
		if (!cached) {
			var password = string.Join(string.Empty, authParts[1..]);
			var tokenRequest = new GitLabTokenRequest { Username = username, Password = password };
			async Task<HttpResponseMessage> GetToken() {
				using var request = new HttpRequestMessage(HttpMethod.Post, $"{_httpClient.BaseAddress}oauth/token");
				var tokenRequestJson = JsonSerializer.Serialize(tokenRequest);
				request.Content = new StringContent(tokenRequestJson, Encoding.UTF8, "application/json");
				return await _httpClient.SendAsync(request);
			}
			using var response = await GetToken();
			response.EnsureSuccessStatusCode();
			using var responseContent = response.Content;
			var jsonResponse = await responseContent.ReadAsStringAsync();
			var tokens = JsonSerializer.Deserialize<GitLabAccessTokens>(jsonResponse);
			_accessTokens["username"] = accessToken = tokens.AccessToken;
		}
		return (accessToken, cached);
	}

	public async Task<byte[]> GetSourceAsync(GitLabSourceFileRequest request, string? authToken = null) {
		// We will receive a request along these lines ...
		// /PROJECT_PATH/raw/LONG_COMMIT_HASH/FILE_PATH
		// We need to change it to:
		// GITLAB_HOST_ORIGIN/api/v4/projects/PROJECT_PATH/repository/files/FILE_PATH/raw?ref=LONG_COMMIT_HASH
		var encodedProjectPath = HttpUtility.UrlEncode(request.projectPath);
		var encodedFilePath = HttpUtility.UrlEncode(request.filePath);
		var gitLabURL = $"/api/v4/projects/{encodedProjectPath}/repository/files/{encodedFilePath}/raw?ref={request.commitHash}";
		_logger.LogDebug($"Translated to GitLab request: {gitLabURL}");

		async Task<HttpResponseMessage> getSourceWithAccessToken(string? accessToken, bool retryAllowed = true) {
			using var request = new HttpRequestMessage(HttpMethod.Get, gitLabURL);
			request.Headers.Add("Authorization", $"Bearer {accessToken}");
			var response = await _httpClient.SendAsync(request);
			if (!response.IsSuccessStatusCode && retryAllowed) {
				response.Dispose();
				return await getSourceWithAuthenticationToken(authToken, false);
			}
			return response;
		}

		async Task<HttpResponseMessage> getSourceWithAuthenticationToken(string authenticationToken, bool cachedAccessTokensAllowed) {
			var (accessToken, wasCached) = await GetAccessToken(authenticationToken, cachedAccessTokensAllowed);
			return await getSourceWithAccessToken(accessToken, wasCached);
		}

		async Task<HttpResponseMessage> getSourceResponse() {
			if (authToken == null)
				return await _httpClient.GetAsync(gitLabURL);
			return await getSourceWithAuthenticationToken(authToken, true);
		}

		using var response = await getSourceResponse();
		response.EnsureSuccessStatusCode();
		using var content = response.Content;
		return await content.ReadAsByteArrayAsync();
	}
}