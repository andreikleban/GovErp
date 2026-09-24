# Build context: repository root. Run publish first (see docker/build-image.ps1).
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY artifacts/publish/GovErp.Web .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GovErp.Web.dll"]
