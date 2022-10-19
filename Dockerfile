FROM mcr.microsoft.com/dotnet/aspnet
# Build first using: dotnet build -c Release
COPY ./SourceLinkGitLabProxy/bin/Release/net6.0/* /sourcelinkgitlabproxy/
ENTRYPOINT [ "/sourcelinkgitlabproxy/SourceLinkGitLabProxy" ]
