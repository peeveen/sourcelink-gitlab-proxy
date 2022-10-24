namespace SourceLinkGitLabProxy;

// Deciphered source file request (components are broken out from request URL path).
public record GitLabSourceFileRequest(string projectPath, string filePath, string commitHash);