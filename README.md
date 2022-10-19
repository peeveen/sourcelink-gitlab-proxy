# Source Link to GitLab proxy

[Source Link](https://github.com/dotnet/sourcelink) is a technology promoted by Microsoft to allow dynamic retrieval of code from a repository when debugging. The repository URL is encoded into the PDB, and, during debugging, when the time comes to step into the code from that repository, the IDE will fetch the appropriate version of the source code from that repository, and seamlessly step into it.

The Source Link technology relies on accessing raw source code from a repository using [Basic Authentication](https://en.wikipedia.org/wiki/Basic_access_authentication) over HTTPS. This is
supported by [GitHub](https://github.com/), [BitBucket](https://bitbucket.org/), and several other online repository providers ...
[but not GitLab](https://gitlab.com/gitlab-org/gitlab/-/issues/19189), which instead expects any such access to be done via its
[API](https://docs.gitlab.com/ee/api/repository_files.html), a process that is unfortunately not directly compatible with Source Link.

There is a workaround. Using the `SourceLinkGitLabHost` element in a project file, we can override the URL that Source Link makes requests to, pointing it to a proxy webservice instead:

![SourceLinkGitLabHost](media/sourcelinkgitlabhost.png?raw=true)

The proxy webservice can then access GitLab using the API, and return the retrieved source content to the Source Link client.

This project is one such proxy webservice.

## Usage

Two parameters can be supplied:

- `GitLabHostOrigin`: (required) the origin of the GitLab host (e.g. https://gitlab.yourdomain.com)
- `AccessToken`: (optional) an access token that can be used to access source code via the API. This token must have at least `read_repository` scope, and would ideally be generated by a user who has access to all projects across the GitLab instance.

## Security

If using the `AccessToken` parameter, all source code access is performed as the user who owns the access token. This is not great from a security perspective, but is fast and efficient. If you trust your users, and your GitLab instance is safe from external access, this might be your simplest solution.

Otherwise, the proxy will perform a series of steps to obtain an access token.

1. If a request is received without a Basic Authentication header (i.e. `Authorization: Basic base64encodedUsernameAndPassword`) then the proxy will return a `401 Unauthorized` result.
2. This seems to prompt Visual Studio to retry the request, this time _with_ an `Authorization` header containing credentials that it obtains from the _Git Credential Manager_.
   > ⚠️ Visual Studio will **only** send an `Authorization` header to an **HTTPS** URL.

> Beforehand, from a command line, you should tell _Git Credential Manager_ to store your GitLab credentials against the URL of **this proxy**, e.g.
>
> ```
> /> git credential-manager-core store
> protocol=https
> host=your-PROXY-host-here_no-port
> username=your-GITLAB-username-here
> password=your-GITLAB-password-here
> ^Z
> ```

3. When the proxy receives this new request, it will call the `/oauth/token` endpoint on your GitLab server to obtain an access token, passing the username and password that were provided in the `Authorization` header.
4. If an access token is returned, this token is used to access the GitLab API to fetch the source code.
   - The token is cached, and any future requests from that user will try to use the cached access token. If a request with a cached access token fails, the proxy will generate a new access token (as described in step 3) then retry the request.

## TODO

- Make use of the refresh token that is returned with the access token, and/or try to determine expiry times of access tokens (though [this page](https://forum.gitlab.com/t/missing-expires-in-in-the-token-response/1232) suggests that they _never expire!_ 😮)
- Better text translation. Currently it expects all source files to be returned as UTF8 or UTF8-BOM, and forces Windows-style line endings (i.e. CRLF).

## Running the proxy

You can provide arguments to the webservice in a few different ways, depending on how it is being run.

1. Modify the `appSettings.json` file:

```
{
	"GitLabHostOrigin": "https://gitlab.yourdomain.com",
	"AccessToken": "1234567890abcdef",
	"Logging": {
		"LogLevel": {
			"Default": "Information",
			"Microsoft": "Warning",
			"Microsoft.Hosting.Lifetime": "Information"
		}
	},
	"AllowedHosts": "\*"
}

```

2. If running from a command line:

```

SourceLinkGitLabProxy --GitLabHostOrigin=https://gitlab.yourdomain.com --AccessToken=1234567890abcdef

```

> On Windows, the webservice will listen on HTTP port 5000, but you can change that by [editing the relevant fields](https://nodogmablog.bryanhogan.net/2022/01/a-few-ways-of-setting-the-kestrel-ports-in-net-6/) in `appsettings.json`.

3. If using a Docker image:

```

docker run -dit -p 8080:80 registry.yourdomain.com/sourcelink-gitlab-proxy --GitLabHostOrigin=https://gitlab.yourdomain.com --AccessToken=1234567890abcdef

```
