# Source Link GitLab proxy

[Source Link](https://github.com/dotnet/sourcelink) is a technology promoted by Microsoft to allow dynamic retrieval of code from a repository when debugging. The repository URL is encoded into the PDB, and, during debugging, when the time comes to step into the code from that repository, the IDE will fetch the appropriate version of the source code from that repository, and seamlessly step into it.

The Source Link technology relies on accessing raw source code from a repository using [Basic Authentication](https://en.wikipedia.org/wiki/Basic_access_authentication) over HTTPS. This is supported by [GitHub](https://github.com/), [BitBucket](https://bitbucket.org/), and several other online repository providers ... [but not private GitLab servers](https://gitlab.com/gitlab-org/gitlab/-/issues/19189), which instead expect any such access to be done via the GitLab [API](https://docs.gitlab.com/ee/api/repository_files.html), a process that is unfortunately not directly compatible with Source Link.

There is a workaround. Using the `SourceLinkGitLabHost` element in the project file of the code that is being published to a NuGet feed, we can override the URL that Source Link makes requests to, pointing it to a proxy webservice instead:

![SourceLinkGitLabHost](media/sourcelinkgitlabhost.png?raw=true)

The proxy webservice can then access GitLab using the API, and return the retrieved source content to the Source Link client.

This project is one such proxy webservice.

## Build

1. _(Optional)_ Modify `appsettings.yml` with your preferred settings (see 'Usage' section below). If you don't do this, you will have to supply your configuration arguments via command line when you run the proxy.
2. _(Optional)_ Add an HTTPS certificate somewhere (see 'HTTPS' section below). If you don't do this, you will only be able to use this proxy via HTTP, or with a static personal access token.
3. Run the build command (you can specify a different image tag if you wish):

```
docker build -t sourcelinkgitlabproxy .
```

> You can add `--build-arg version=n.n.n.n` to set the version numbers in the built files, otherwise they will have a default version of 1.0.0.0.

## Running the unit tests

If you have the .NET SDK installed:

```
dotnet test
```

... or if you want to use Docker:

```
docker run -v ${PWD}/:/SourceLinkGitLabProxy mcr.microsoft.com/dotnet/sdk /bin/sh -c "cd SourceLinkGitLabProxy && dotnet test"
```

## Run

Assuming you have used the suggested tag, you can run your built image with this command (mapped port numbers can obviously be changed if you wish):

```
docker run -dit -p 5041:80 -p 5042:443 sourcelinkgitlabproxy
```

> See the upcoming 'Usage' section for available arguments.

If you want to quickly check that the app is running, there is `/version` endpoint that will return the app version:

```
# Via HTTP
curl http://localhost:5041/version
# Via HTTPS ... add -k if you are using a self-signed certificate
curl https://localhost:5042/version
```

## Usage

Arguments can be supplied by appending them to the `docker run` command (using `--ArgumentName=Value` syntax), or by setting their values in the appropriate `appsettings.*.yml` file:

Arguments specific to this app are:

- `GitLabHostOrigin`: _(required)_ The origin of the GitLab host (e.g. https://gitlab.yourdomain.com)
- `PersonalAccessToken`: _(optional)_ A personal access token that will be used to access source code via the API. This token must have at least `read_repository` scope, and would ideally be generated by a user who has access to all projects across the GitLab instance (see 'Security' section below). If this is _not_ used, source code access will be via OAuth, on a per-user basis.
- `LineEndingChange`: _(optional)_ After the source code is obtained, line-endings can be replaced with the line-ending from a particular platform. The acceptable values for this property are `Windows` (CRLF), `Unix` (LF), or `None` to leave the content unaltered. These values are case-sensitive. The default is `None`. For more info, see the 'Line Endings' section below.
- `OAuthTokenRequestScope`: _(optional)_ The scope that is requested during an OAuth access token request. Defaults to `api`.

Any other property from the `appsettings.yml` file can also be supplied via command line. Nested properties should be separated by colon characters, e.g. `--TopLevelProperty:NestedProperty:FurtherNestedProperty=Value`.

## HTTPS

You **will** need to run this proxy as an HTTPS server, unless:

- you are using the `PersonalAccessToken` argument.
- you are hosting this behind a reverse proxy that deals with HTTPS (e.g. _nginx_, _Apache_, etc).

Otherwise you will need to set some properties in the `HttpServer:Endpoints:Https` section to tell the app about your certificate. There are two methods of describing the certificate to the app:

- **File**: Set the `HttpServer:Endpoints:Https:FilePath` property to the path to the certificate file, and the `HttpServer:Endpoints:Https:Password` property to the certificate password (The .NET Core libraries seem to like PKCS#12
  certificates best).
- **Certificate Store**: The certificate should be imported to a store on the server machine, and the `HttpServer:Endpoints:Https:StoreLocation` and `HttpServer:Endpoints:Https:StoreName` properties should be set (e.g. "LocalMachine" and "My", respectively). The certificate will be found inside the specified store by matching against `HttpServer:Endpoints:Https:Host`.

> ⚠️ The default `appsettings.yml` points to a self-signed certificate which _will not work_, but will suffice if you only using this proxy via HTTP.

> Read more about certificates in the [separate README file in the `certs` folder](./SourceLinkGitLabProxy/certs/README.md).

## Security

If using the `PersonalAccessToken` argument, all source code access is performed as the user who owns the access token. This is not great from a security perspective, but is fast and efficient. If you trust your users, and your GitLab instance is safe from external access, this might be your simplest solution.

Otherwise, the proxy will perform a series of steps to obtain an access token.

1. If a request is received without a Basic Authentication header (i.e. `Authorization: Basic base64encodedUsernameAndPassword`) then the proxy will return a `401 Unauthorized` result.
2. This will prompt Visual Studio to retry the request, this time _with_ an `Authorization` header containing credentials that it obtains from the _Git Credential Manager_.

   > ⚠️ Visual Studio will **only** send an `Authorization` header to an **HTTPS** URL.

3. When the proxy receives this new request, it will call the [`/oauth/token` endpoint](https://docs.gitlab.com/ee/api/oauth2.html#resource-owner-password-credentials-flow) on your GitLab server to request an access token with `api` scope, passing the username and password that were provided in the `Authorization` header.
   - For some reason, a `read_repository`-scoped access token generated in this manner will not work.
4. If an access token is returned, this token is used to access the GitLab API to fetch the source code.
   - The token is cached, and any future requests from that user will try to use the cached access token. If a request with a cached access token fails, the proxy will generate a new access token (as described in step 3) then retry the request.

## Git Credential Manager

If Visual Studio gets a `401 Unauthorized` response from the proxy (which it will if your are using OAuth-style access), it will
attempt to get access credentials from _Git Credential Manager_. If it doesn't find any, it should then automatically prompt you for the credentials to access GitLab with.

If you want, you can authorize beforehand, from a command line, like this:

```
> git credential-manager-core store
protocol=https
host=your-PROXY-host-and-port-here
username=your-GITLAB-username-here
password=your-GITLAB-password-here
^Z
```

> Use Ctrl+Z to end the input, and press Return.

## Line Endings

_Git for Windows_ has a '_Checkout Windows-style, commit Unix-style line endings_' feature, controlled by the `autocrlf` variable. This feature is **bad news** for Source Link.

Visual Studio will **reject** any retrieved source file if the exact checksum of that source file does not match the checksum stored in the package PDB, so it is imperative that the source code fetched by this proxy is a byte-for-byte copy of the code as it was seen _**during the creation of the NuGet package**_.

This means that, if you published a NuGet package from a Windows environment (where the code was checked-out with CRLF line-endings), but Source Link provides the code for the package _directly from the Git repository_ (where the code will have Unix-style LF line-endings), then there will be a mismatch.

You can use the `LineEndingType` argument to make the proxy attempt a line-ending replacement on any fetched source content. For the proxy to perform such a string modification, it has to try to figure out the file encoding so that it can decode the content, perform the substitutions, then re-encode that modified content using the original encoding type. For now, only UTF-8/16/32 encodings are supported, and if a source file has no byte-order-mark, it
is assumed to be UTF-8.

## TODO

- Make use of the refresh token that is returned with the access token, and/or try to determine expiry times of access tokens (though [this page](https://forum.gitlab.com/t/missing-expires-in-in-the-token-response/1232) suggests that they _never expire!_ 😮)
- Support more encodings for the `LineEndingChange` functionality.
- Slightly better error handling during access token generation.
- Change build to `--self-contained`, running on a minimal Linux image.
