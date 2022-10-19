namespace SourceLinkGitLabProxy;

public interface IGitLabClient {
	public Task<byte[]> GetSourceAsync(GitLabSourceFileRequest request, string? authToken = null);
}