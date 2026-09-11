FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ .
RUN dotnet publish -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080 DATA_DIR=/data
RUN mkdir /data && chown app:app /data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "App.dll"]
