ARG version="1.0.0.0"

FROM mcr.microsoft.com/dotnet/sdk AS build
ARG version
WORKDIR /SourceLinkGitLabProxy
COPY ./SourceLinkGitLabProxy /SourceLinkGitLabProxy
RUN dotnet publish -c Release -r linux-musl-x64 --self-contained /p:Version=${version}

FROM alpine:latest
WORKDIR /SourceLinkGitLabProxy
COPY --from=build /SourceLinkGitLabProxy/bin/Release/net6.0/linux-musl-x64/publish/* ./
COPY --from=build /SourceLinkGitLabProxy/certs/* ./certs/
COPY --from=build /SourceLinkGitLabProxy/appsettings.yml ./
RUN apk add gcompat
RUN apk add libstdc++
RUN apk add icu-libs
ENTRYPOINT ["/SourceLinkGitLabProxy/SourceLinkGitLabProxy"]