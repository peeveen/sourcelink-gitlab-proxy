using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace SourceLinkGitLabProxy.Controllers;

[Controller]
[Route("/")]
public class GitLabController : Controller {
	private static readonly string SourceLinkURLRegexPattern = @"^\/(?<projectPath>.*)\/raw\/(?<commitHash>[0-9A-Fa-f]*)\/(?<filePath>.*)$";
	private static readonly Regex SourceLinkURLRegex = new Regex(SourceLinkURLRegexPattern);

	IGitLabClient _gitLabClient;
	ILogger _logger;
	bool _authorizationRequired = true;

	public GitLabController(IConfiguration configuration, ILoggerFactory loggerFactory, IGitLabClient gitLabClient) {
		_gitLabClient = gitLabClient;
		_logger = loggerFactory.CreateLogger<GitLabController>();
		_authorizationRequired = string.IsNullOrEmpty(configuration[Startup.AccessTokenParameterName]);
	}

	public static GitLabSourceFileRequest ParseURL(string url) {
		var match = SourceLinkURLRegex.Match(url);
		if (!match.Success || match.Groups.Count < 4)
			throw new ArgumentException($"'{url}' could not be parsed as a Source Link URL.");
		var projectPath = match.Groups["projectPath"].Value;
		var commitHash = match.Groups["commitHash"].Value;
		var filePath = match.Groups["filePath"].Value;
		return new GitLabSourceFileRequest(projectPath, filePath, commitHash);
	}

	[HttpGet]
	[Route("{*queryvalues}")]
	public async Task<string> GetSource() {
		static string decodeAuthHeader(string authHeader) => Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Replace("Basic ", string.Empty)));
		var authHeaderGot = Request.Headers.TryGetValue("Authorization", out var authHeaders);
		// Annoyingly, Visual Studio will only send an Authorization header if we first complain
		// about it missing from the initial request.
		if (!authHeaderGot && _authorizationRequired) {
			Response.StatusCode = (int)HttpStatusCode.Unauthorized;
			return string.Empty;
		} else {
			var authToken = _authorizationRequired ? decodeAuthHeader(authHeaders.First()) : null;
			var sourceLinkRecord = ParseURL(Request.Path);
			_logger.LogDebug($"Received Source Link request: {sourceLinkRecord.ToString()}");
			var bytes = await _gitLabClient.GetSourceAsync(sourceLinkRecord, authToken);
			var hasBOM = bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
			var stringContent = new UTF8Encoding(hasBOM).GetString(bytes);
			var normalizedStringContent = stringContent.ReplaceLineEndings("\r\n");
			return normalizedStringContent;
		}
	}
}