namespace SourceLinkGitLabProxy;

// Configuration object, holds the values of arguments supplied on the command line.
public class ProxyConfig : IProxyConfig {
	public static readonly string PersonalAccessTokenArgumentName = "PersonalAccessToken";
	public static readonly string GitLabHostOriginArgumentName = "GitLabHostOrigin";
	public static readonly string LineEndingChangeArgumentName = "LineEndingChange";
	public static readonly string OAuthTokenRequestScopeArgumentName = "OAuthTokenRequestScope";

	internal ProxyConfig(IConfiguration configuration) {
		static void validateArgument(string name, string value) {
			if (string.IsNullOrEmpty(value))
				throw new Exception($"No value was provided for the configuration property '{name}'.");
		}
		string getArgument(string argName, string defaultValue = "") => (configuration[argName] ?? defaultValue).Trim();

		GitLabHostOrigin = getArgument(GitLabHostOriginArgumentName);
		PersonalAccessToken = getArgument(PersonalAccessTokenArgumentName);
		OAuthTokenRequestScope = getArgument(OAuthTokenRequestScopeArgumentName, "api");
		LineEndingChange = Enum.Parse<LineEndingChange>(getArgument(LineEndingChangeArgumentName, nameof(LineEndingChange.Windows)));
		validateArgument(nameof(GitLabHostOrigin), GitLabHostOrigin);
	}

	public string GitLabHostOrigin { get; }

	public string PersonalAccessToken { get; }

	public LineEndingChange LineEndingChange { get; }

	public string OAuthTokenRequestScope { get; }
}