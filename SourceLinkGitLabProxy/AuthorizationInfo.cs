using System.Collections.Concurrent;
using Microsoft.Net.Http.Headers;

namespace SourceLinkGitLabProxy;

public class AuthorizationInfo {
	public static readonly string GitLabPrivateTokenHeaderName = "PRIVATE-TOKEN";

	// Cached OAuth tokens. Prevents us having to make an OAuth request for every request that this proxy receives.
	// OAuth tokens are cached per user.
	private static readonly IDictionary<string, GitLabOAuthTokens> _accessTokens = new ConcurrentDictionary<string, GitLabOAuthTokens>();

	private AuthorizationInfo() { }

	private AuthorizationInfo(AuthorizationInfo authInfo) {
		OAuthTokens = authInfo.OAuthTokens;
		OAuthTokensCameFromCache = authInfo.OAuthTokensCameFromCache;
		BasicAuthenticationHeaderUser = authInfo.BasicAuthenticationHeaderUser;
		BasicAuthenticationHeaderPassword = authInfo.BasicAuthenticationHeaderPassword;
		PersonalAccessToken = authInfo.PersonalAccessToken;
	}

	public static AuthorizationInfo FromPersonalAccessToken(string personalAccessToken) {
		return new AuthorizationInfo {
			PersonalAccessToken = personalAccessToken
		};
	}

	public static AuthorizationInfo FromBasicAuthenticationHeader(string basicAuthenticationHeader) {
		return new AuthorizationInfo {
			BasicAuthenticationHeader = basicAuthenticationHeader
		};
	}

	// Resets the current OAuth tokens if they came from the cache, and also
	// removes them from the cache.
	public static AuthorizationInfo InvalidateCachedOAuthTokens(AuthorizationInfo authInfo) {
		if (!authInfo.OAuthTokensCameFromCache)
			return authInfo;
		_accessTokens.Remove(authInfo.BasicAuthenticationHeaderUser);
		return new AuthorizationInfo(authInfo) {
			OAuthTokens = new GitLabOAuthTokens(),
			OAuthTokensCameFromCache = false
		};
	}

	// A fixed PAT, manually generated by someone and supplied to this proxy.
	public string PersonalAccessToken { get; init; } = string.Empty;

	// OAuth tokens. May be empty.
	public GitLabOAuthTokens OAuthTokens { get; init; }
	public bool OAuthTokensCameFromCache { get; init; } = false;

	// Deciphered contains of the Basic Authentication header, if one was received.
	public string BasicAuthenticationHeaderUser { get; init; } = string.Empty;
	public string BasicAuthenticationHeaderPassword { get; init; } = string.Empty;
	private string BasicAuthenticationHeader {
		init {
			var authParts = value.Split(':');
			BasicAuthenticationHeaderUser = authParts.First();
			BasicAuthenticationHeaderPassword = string.Join(string.Empty, authParts[1..]);
			if (OAuthTokensCameFromCache = _accessTokens.TryGetValue(BasicAuthenticationHeaderUser, out var accessTokens))
				this.OAuthTokens = accessTokens;
		}
	}

	// Returns true if this object contains enough information to at least ATTEMPT authorization.
	public bool CanAttemptAuthorization => !string.IsNullOrEmpty(PersonalAccessToken) || !string.IsNullOrEmpty(OAuthTokens.AccessToken) || (!string.IsNullOrEmpty(BasicAuthenticationHeaderUser) && !string.IsNullOrEmpty(BasicAuthenticationHeaderPassword));

	// Applies authorization headers to a request.
	// If necessary, will call the supplied tokenGenerator function to get
	// OAuth access tokens.
	public async Task<AuthorizationInfo> AuthorizeRequest(HttpRequestMessage request, Func<string, string, Task<GitLabOAuthTokens>> tokenGenerator) {
		if (!string.IsNullOrEmpty(PersonalAccessToken)) {
			request.Headers.Add(GitLabPrivateTokenHeaderName, PersonalAccessToken);
			return this;
		} else if (string.IsNullOrEmpty(OAuthTokens.AccessToken)) {
			var generatedTokens = await tokenGenerator(BasicAuthenticationHeaderUser, BasicAuthenticationHeaderPassword);
			_accessTokens[BasicAuthenticationHeaderUser] = generatedTokens;
			request.Headers.Add(HeaderNames.Authorization, $"Bearer {generatedTokens.AccessToken}");
			return new AuthorizationInfo(this) {
				OAuthTokens = generatedTokens,
				OAuthTokensCameFromCache = false
			};
		}
		return this;
	}
}