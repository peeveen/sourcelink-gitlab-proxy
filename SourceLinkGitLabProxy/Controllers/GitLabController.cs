using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace SourceLinkGitLabProxy.Controllers;

[Controller]
[Route("/")]
public class GitLabController : Controller {
	const string ProjectPathGroupName = "projectPath";
	const string CommitHashGroupName = "commitHash";
	const string FilePathGroupName = "filePath";
	const string SourceLinkURLRegexPattern = @$"^\/(?<{ProjectPathGroupName}>.*)\/raw\/(?<{CommitHashGroupName}>[0-9A-Fa-f]*)\/(?<{FilePathGroupName}>.*)$";
	static readonly Regex SourceLinkURLRegex = new Regex(SourceLinkURLRegexPattern);

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

	private async Task<HttpContent> PostProcessContent(HttpContent content) {
		if (_configuration.LineEndingChange != LineEndingChange.None) {
			_logger.LogInformation($"Replacing line endings with {_configuration.LineEndingChange}-style line endings.");
			var bytes = await content.ReadAsByteArrayAsync();
			var (encoding, stringContent) = EncodingUtils.GetFileContentAsString(bytes);
			_logger.LogInformation($"File content is believed to be {encoding.WebName}.");
			stringContent = stringContent.ReplaceLineEndings(_configuration.LineEndingChange == LineEndingChange.Windows ? "\r\n" : "\n");
			bytes = EncodingUtils.GetFileContentForString(encoding, stringContent);
			content.Dispose();
			content = new ByteArrayContent(bytes);
		}
		return content;
	}

	[HttpGet]
	[Route("/version")]
	public string GetVersion() => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

	[HttpGet]
	[Route("{*queryvalues}")]
	public async Task GetSource() {
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
		} else {
			var sourceLinkRecord = ParseURL(Request.Path);
			_logger.LogInformation($"Received Source Link request: {sourceLinkRecord.ToString()}");
			using var response = await _gitLabClient.GetSourceAsync(sourceLinkRecord, authInfo);
			Response.StatusCode = (int)response.StatusCode;
			using var content = await PostProcessContent(response.Content);
			using var inStream = await content.ReadAsStreamAsync();
			using var outStream = Response.BodyWriter.AsStream();
			_logger.LogInformation("Returning source file content.");
			await inStream.CopyToAsync(outStream);
		}
	}
}
