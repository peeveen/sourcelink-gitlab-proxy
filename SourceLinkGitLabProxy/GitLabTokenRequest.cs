using System.Text.Json.Serialization;

namespace SourceLinkGitLabProxy;

// Info that GitLab requires when making a request for OAuth tokens
// using the "resource owner password credentials flow".
public record GitLabTokenRequest {
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
	public string Scope { get; init; } = "api";
}
