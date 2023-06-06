using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace SourceLinkGitLabProxy.Controllers;

[Controller]
[Route("/")]
public class GitLabController : Controller {
	private const string FaviconFilename = "favicon.png";

	private static readonly byte[]? _favicon = GetEmbeddedResourceBytes(typeof(Program).GetTypeInfo().Assembly, FaviconFilename);

	private readonly IGitLabClient _gitLabClient;
	private readonly ILogger _logger;
	private readonly IProxyConfig _configuration;

	/// <summary>
	/// Obtains an embedded resource stream from the given assembly.
	/// </summary>
	/// <param name="assembly">Assembly to find the resource in.</param>
	/// <param name="resourceName">Name of resource.</param>
	/// <returns>Stream, or null if not found.</returns>
	public static Stream? GetEmbeddedResourceStream(Assembly assembly, string resourceName) =>
		assembly.GetManifestResourceStream($"{assembly.GetName().Name}.{resourceName}");

	/// <summary>
	/// Obtains an embedded resource from the given assembly, as bytes.
	/// </summary>
	/// <param name="assembly">Assembly to find the resource in.</param>
	/// <param name="resourceName">Name of resource.</param>
	/// <returns>Bytes, or null if not found.</returns>
	[SuppressMessage("csharp", "S1168")]
	public static byte[]? GetEmbeddedResourceBytes(Assembly assembly, string resourceName) {
		using var resourceStream = GetEmbeddedResourceStream(assembly, resourceName);
		if (resourceStream == null) return null;
		var resourceBytes = new byte[resourceStream.Length];
		using var memStream = new MemoryStream(resourceBytes, true);
		resourceStream.CopyTo(memStream);
		return resourceBytes;
	}

	public GitLabController(IProxyConfig configuration, ILoggerFactory loggerFactory, IGitLabClient gitLabClient) {
		_gitLabClient = gitLabClient;
		_configuration = configuration;
		_logger = loggerFactory.CreateLogger<GitLabController>();
	}

	private async Task<HttpContent> PostProcessContent(HttpContent content) {
		if (_configuration.LineEndingChange != LineEndingChange.None) {
			_logger.LogInformation("Replacing line endings with {LineEndingType}-style line endings.", _configuration.LineEndingChange);
			var bytes = await content.ReadAsByteArrayAsync();
			var (encoding, stringContent) = EncodingUtils.GetFileContentAsString(bytes);
			_logger.LogInformation("File content is believed to be {EncodingName}.", encoding.WebName);
			stringContent = stringContent.ReplaceLineEndings(_configuration.LineEndingChange == LineEndingChange.Windows ? "\r\n" : "\n");
			bytes = EncodingUtils.GetFileContentForString(encoding, stringContent);
			content.Dispose();
			content = new ByteArrayContent(bytes);
		}
		return content;
	}

	[HttpGet]
	[Route("/favicon.ico")]
	[SuppressMessage("csharp", "CA1822")]
	public IActionResult GetIcon() => _favicon == null ? new NotFoundResult() : new FileContentResult(_favicon, MediaTypeNames.Application.Octet);

	[HttpGet]
	[Route("/version")]
	[SuppressMessage("csharp", "CA1822")]
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
			var sourceLinkRecord = new GitLabSourceFileRequest(Request.Path);
			_logger.LogInformation("Received Source Link request: {SourceLinkRecord}", sourceLinkRecord);
			using var response = await _gitLabClient.GetSourceAsync(sourceLinkRecord.GitLabURL, authInfo);
			Response.StatusCode = (int)response.StatusCode;
			using var content = await PostProcessContent(response.Content);
			using var inStream = await content.ReadAsStreamAsync();
			using var outStream = Response.BodyWriter.AsStream();
			_logger.LogInformation("Returning source file content.");
			await inStream.CopyToAsync(outStream);
		}
	}
}
