namespace SourceLinkGitLabProxy;

// Configuration object, holds the values of arguments supplied on the command line.
public class ProxyConfig : IProxyConfig {
	public const string PersonalAccessTokenArgumentName = "PersonalAccessToken";
	public const string GitLabHostOriginArgumentName = "GitLabHostOrigin";
	public const string LineEndingChangeArgumentName = "LineEndingChange";
	public const string OAuthTokenRequestScopeArgumentName = "OAuthTokenRequestScope";

	internal ProxyConfig(IConfiguration configuration) {
		static void validateArgument(string name, string value) {
			if (string.IsNullOrEmpty(value))
				throw new Exception($"No value was provided for the configuration property '{name}'.");
		}
		string getArgument(string argName, string defaultValue = "") => (configuration[argName] ?? defaultValue).Trim();

		GitLabHostOrigin = getArgument(GitLabHostOriginArgumentName);
		PersonalAccessToken = getArgument(PersonalAccessTokenArgumentName);
		OAuthTokenRequestScope = getArgument(OAuthTokenRequestScopeArgumentName, "api");
		LineEndingChange = Enum.Parse<LineEndingChange>(getArgument(LineEndingChangeArgumentName, nameof(LineEndingChange.None)));
		validateArgument(nameof(GitLabHostOrigin), GitLabHostOrigin);
	}

	public string GitLabHostOrigin { get; }

	public string PersonalAccessToken { get; }

	public LineEndingChange LineEndingChange { get; }

	public string OAuthTokenRequestScope { get; }
}