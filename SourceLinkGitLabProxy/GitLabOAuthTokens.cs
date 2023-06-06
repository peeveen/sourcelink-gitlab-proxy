using System.Text.Json.Serialization;

namespace SourceLinkGitLabProxy;

// OAuth token info that is returned from GitLab.
public readonly struct GitLabOAuthTokens {
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
