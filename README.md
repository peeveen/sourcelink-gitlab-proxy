# Source Link GitLab proxy

[Source Link](https://github.com/dotnet/sourcelink) is a technology promoted by Microsoft to allow dynamic retrieval of code from a repository when debugging. The repository URL is encoded into the PDB, and, during debugging, when the time comes to step into the code from that repository, the IDE will fetch the appropriate version of the source code from that repository, and seamlessly step into it.

The Source Link technology relies on accessing raw source code from a repository using [Basic Authentication](https://en.wikipedia.org/wiki/Basic_access_authentication) over HTTPS. This is
supported by [GitHub](https://github.com/), [BitBucket](https://bitbucket.org/), and several other online repository providers ...
[but not private GitLab servers](https://gitlab.com/gitlab-org/gitlab/-/issues/19189), which instead expect any such access to be done via the GitLab
[API](https://docs.gitlab.com/ee/api/repository_files.html), a process that is unfortunately not directly compatible with Source Link.

There is a workaround. Using the `SourceLinkGitLabHost` element in the project file of the code that is being published to a NuGet feed, we can override the URL that Source Link makes requests to, pointing it to a proxy webservice instead:

![SourceLinkGitLabHost](media/sourcelinkgitlabhost.png?raw=true)

The proxy webservice can then access GitLab using the API, and return the retrieved source content to the Source Link client.

This project is one such proxy webservice.

## Build

1. Optionally modify `appsettings.yml` with your preferred settings (see 'Usage' section below).
2. Optionally add an HTTPS certificate somewhere (see 'HTTPS' section below)
3. Run the build command (you can specify a different tag if you wish):

```
docker build -t sourcelinkgitlabproxy .
```

> You can optionally add `--build-arg version=n.n.n.n` to version stamp the built files, otherwise they will have a default version of 1.0.0.0.

## Testing

If you have the .NET SDK installed:

```
dotnet test
```

... or if you want to use Docker:

```
docker run -v ${PWD}/:/SourceLinkGitLabProxy mcr.microsoft.com/dotnet/sdk /bin/sh -c "cd SourceLinkGitLabProxy && dotnet test"
```

## Run

Assuming you have kept the port numbers from the default config, and used the suggested tag, you can run your built image with this command (mapped port number can obviously be changed if you wish):

```
docker run -dit -p 5041:5041 -p 5042:5042 sourcelinkgitlabproxy
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

> Beforehand, from a command line, you should tell _Git Credential Manager_ to store your GitLab credentials against the URL of **this proxy**, e.g.
>
> ```
> > git credential-manager-core store
> protocol=https
> host=your-PROXY-host-here_no-port
> username=your-GITLAB-username-here
> password=your-GITLAB-password-here
> ^Z
> ```

3. When the proxy receives this new request, it will call the [`/oauth/token` endpoint](https://docs.gitlab.com/ee/api/oauth2.html#resource-owner-password-credentials-flow) on your GitLab server to request an access token with `api` scope, passing the username and password that were provided in the `Authorization` header.
   - For some reason, a `read_repository`-scoped access token generated in this manner will not work.
4. If an access token is returned, this token is used to access the GitLab API to fetch the source code.
   - The token is cached, and any future requests from that user will try to use the cached access token. If a request with a cached access token fails, the proxy will generate a new access token (as described in step 3) then retry the request.

## Line Endings

Much like how _Git for Windows_ has a _Checkout Windows-style, commit Unix-style line endings_ feature, this proxy can do a similar thing, and this is controlled by the `LineEndingChange` argument. If the exact checksum of the source file provided via Source Link does not match the checksum stored in the package PDB, Visual Studio will reject it for not matching the original source code, so it is imperative that the source code fetched by this proxy is a byte-for-byte copy of the code as it was _**during the creation of the NuGet package**_.

> If your NuGet packages are created on a Linux build server, you probably don't need to worry about this.

## TODO

- Make use of the refresh token that is returned with the access token, and/or try to determine expiry times of access tokens (though [this page](https://forum.gitlab.com/t/missing-expires-in-in-the-token-response/1232) suggests that they _never expire!_ 😮)
- Better text translation. Currently it expects all source files to be returned as UTF8 or UTF8-BOM. Not sure if other encodings will ever be received.
- Slightly better error handling during access token generation.
- Change build to self contained, running on a minimal Linux image.
