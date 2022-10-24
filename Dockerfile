ARG version="1.0.0.0"

FROM mcr.microsoft.com/dotnet/sdk AS build
ARG version
WORKDIR /SourceLinkGitLabProxy
COPY ./SourceLinkGitLabProxy /SourceLinkGitLabProxy
RUN dotnet publish -c Release -r linux-x64 --no-self-contained /p:Version=${version}

FROM mcr.microsoft.com/dotnet/aspnet
WORKDIR /SourceLinkGitLabProxy
COPY --from=build /SourceLinkGitLabProxy/bin/Release/net6.0/linux-x64/publish/* ./
COPY --from=build /SourceLinkGitLabProxy/certs/* ./certs/
COPY --from=build /SourceLinkGitLabProxy/appsettings.yml ./
ENTRYPOINT ["/SourceLinkGitLabProxy/SourceLinkGitLabProxy"]