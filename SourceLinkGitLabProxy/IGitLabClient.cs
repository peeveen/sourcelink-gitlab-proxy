namespace SourceLinkGitLabProxy;

// Interface for our GitLab client. Only need to do one thing: get code!
public interface IGitLabClient {
	public Task<HttpResponseMessage> GetSourceAsync(GitLabSourceFileRequest request, AuthorizationInfo authInfo);
}