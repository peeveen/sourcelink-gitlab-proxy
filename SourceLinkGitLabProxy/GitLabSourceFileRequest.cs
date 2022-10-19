namespace SourceLinkGitLabProxy;

public record GitLabSourceFileRequest(string projectPath, string filePath, string commitHash);