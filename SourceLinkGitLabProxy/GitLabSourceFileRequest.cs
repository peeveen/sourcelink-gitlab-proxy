using System.Text.RegularExpressions;
using System.Web;

namespace SourceLinkGitLabProxy;

// Deciphered source file request (components are broken out from request URL path).
public class GitLabSourceFileRequest {
	private record URLComponents(string projectPath, string filePath, string commitHash);

	private const string ProjectPathGroupName = "projectPath";
	private const string CommitHashGroupName = "commitHash";
	private const string FilePathGroupName = "filePath";
	private const string SourceLinkURLRegexPattern = @$"^\/(?<{ProjectPathGroupName}>.*)\/raw\/(?<{CommitHashGroupName}>[0-9A-Fa-f]*)\/(?<{FilePathGroupName}>.*)$";
	private static readonly Regex SourceLinkURLRegex = new Regex(SourceLinkURLRegexPattern);

	public GitLabSourceFileRequest(string url) : this(ParseURL(url)) { }

	private GitLabSourceFileRequest(URLComponents components) {
		ProjectPath = components.projectPath;
		FilePath = components.filePath;
		CommitHash = components.commitHash;
		// We have received a request along these lines ...
		// /PROJECT_PATH/raw/LONG_COMMIT_HASH/FILE_PATH
		// We need to change it to:
		// GITLAB_HOST_ORIGIN/api/v4/projects/PROJECT_PATH/repository/files/FILE_PATH/raw?ref=LONG_COMMIT_HASH
		var encodedProjectPath = HttpUtility.UrlEncode(ProjectPath);
		var encodedFilePath = HttpUtility.UrlEncode(FilePath);
		GitLabURL = $"/api/v4/projects/{encodedProjectPath}/repository/files/{encodedFilePath}/raw?ref={CommitHash}"; ;
	}

	private static URLComponents ParseURL(string url) {
		var match = SourceLinkURLRegex.Match(url);
		if (!match.Success || match.Groups.Count < 4)
			throw new ArgumentException($"'{url}' could not be parsed as a Source Link URL.");
		var projectPath = match.Groups[ProjectPathGroupName].Value;
		var filePath = match.Groups[FilePathGroupName].Value;
		var commitHash = match.Groups[CommitHashGroupName].Value;
		return new URLComponents(projectPath, filePath, commitHash);
	}

	public string ProjectPath { get; }
	public string FilePath { get; }
	public string CommitHash { get; }
	public string GitLabURL { get; }
}