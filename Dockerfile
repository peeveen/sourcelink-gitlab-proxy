ARG version="1.0.0.0"
ARG dotNetVersion="8.0"

FROM mcr.microsoft.com/dotnet/sdk:${dotNetVersion} AS build
ARG version
WORKDIR /SourceLinkGitLabProxy
COPY ./SourceLinkGitLabProxy /SourceLinkGitLabProxy
RUN dotnet publish -c Release -r linux-musl-x64 --self-contained /p:Version=${version}

FROM alpine:latest
ARG dotNetVersion
WORKDIR /SourceLinkGitLabProxy
COPY --from=build /SourceLinkGitLabProxy/bin/Release/net${dotNetVersion}/linux-musl-x64/publish/* /SourceLinkGitLabProxy/appsettings.yml ./
COPY --from=build /SourceLinkGitLabProxy/certs/* ./certs/
RUN apk add gcompat libstdc++ icu-libs
ENTRYPOINT ["/SourceLinkGitLabProxy/SourceLinkGitLabProxy"]