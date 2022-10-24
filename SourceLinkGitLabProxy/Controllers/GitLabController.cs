using System.Reflection;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace SourceLinkGitLabProxy.Controllers;

[Controller]
[Route("/")]
public class GitLabController : Controller {
	private static readonly string ProjectPathGroupName = "projectPath";
	private static readonly string CommitHashGroupName = "commitHash";
	private static readonly string FilePathGroupName = "filePath";
	private static readonly string SourceLinkURLRegexPattern = @$"^\/(?<{ProjectPathGroupName}>.*)\/raw\/(?<{CommitHashGroupName}>[0-9A-Fa-f]*)\/(?<{FilePathGroupName}>.*)$";
	private static readonly Regex SourceLinkURLRegex = new Regex(SourceLinkURLRegexPattern);

	IGitLabClient _gitLabClient;
	ILogger _logger;
	IProxyConfig _configuration;

	public GitLabController(IProxyConfig configuration, ILoggerFactory loggerFactory, IGitLabClient gitLabClient) {
		_gitLabClient = gitLabClient;
		_configuration = configuration;
		_logger = loggerFactory.CreateLogger<GitLabController>();
	}

	// The URL that Source Link uses is a "raw" access URL.
	// We need to translate that to a URL that accesses the content via the
	// GitLab API. To do this, we need to extract the pertinent bits of data
	// from the original request path.
	public static GitLabSourceFileRequest ParseURL(string url) {
		var match = SourceLinkURLRegex.Match(url);
		if (!match.Success || match.Groups.Count < 4)
			throw new ArgumentException($"'{url}' could not be parsed as a Source Link URL.");
		var projectPath = match.Groups[ProjectPathGroupName].Value;
		var commitHash = match.Groups[CommitHashGroupName].Value;
		var filePath = match.Groups[FilePathGroupName].Value;
		return new GitLabSourceFileRequest(projectPath, filePath, commitHash);
	}

	[HttpGet]
	[Route("/version")]
	public string GetVersion() => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

	[HttpGet]
	[Route("{*queryvalues}")]
	public async Task<string> GetSource() {
		static string decodeAuthHeader(string? authHeader) => Encoding.UTF8.GetString(Convert.FromBase64String(authHeader?.Replace("Basic ", string.Empty) ?? string.Empty));
		Request.Headers.TryGetValue(HeaderNames.Authorization, out var authHeaders);
		var authInfo = !string.IsNullOrEmpty(_configuration.PersonalAccessToken) ?
			AuthorizationInfo.FromPersonalAccessToken(_configuration.PersonalAccessToken) :
			AuthorizationInfo.FromBasicAuthenticationHeader(decodeAuthHeader(authHeaders.FirstOrDefault()));

		// Annoyingly, Visual Studio will only send an Authorization header if we first complain
		// about it missing from the initial request.
		if (!authInfo.CanAttemptAuthorization) {
			_logger.LogInformation("There is insufficient authorization information to perform a source code fetch. Returning 401 status.");
			Response.StatusCode = (int)HttpStatusCode.Unauthorized;
			return string.Empty;
		}

		var sourceLinkRecord = ParseURL(Request.Path);
		_logger.LogInformation($"Received Source Link request: {sourceLinkRecord.ToString()}");
		using var response = await _gitLabClient.GetSourceAsync(sourceLinkRecord, authInfo);
		if (!response.IsSuccessStatusCode) {
			Response.StatusCode = (int)response.StatusCode;
			return await response.Content.ReadAsStringAsync();
		}
		using var content = response.Content;
		var bytes = await content.ReadAsByteArrayAsync();

		var hasBOM = bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
		var stringContent = new UTF8Encoding(hasBOM).GetString(bytes);
		if (_configuration.LineEndingChange != LineEndingChange.None)
			stringContent = stringContent.ReplaceLineEndings(_configuration.LineEndingChange == LineEndingChange.Windows ? "\r\n" : "\n");
		return stringContent;
	}
}
