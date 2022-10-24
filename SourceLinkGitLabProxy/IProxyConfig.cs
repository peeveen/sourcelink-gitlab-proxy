namespace SourceLinkGitLabProxy;

// Interface for our configuration object.
public interface IProxyConfig {
	string GitLabHostOrigin { get; }
	string PersonalAccessToken { get; }
	LineEndingChange LineEndingChange { get; }
	string OAuthTokenRequestScope { get; }
}