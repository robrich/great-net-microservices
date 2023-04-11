# buid "server" image
FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine as build

WORKDIR /src

COPY Microservices.ShoppingCart.csproj .
RUN dotnet restore

COPY . .
RUN dotnet build -c Release
RUN dotnet test -c Release
RUN dotnet publish -c Release -o /dist


# production runtime "server" image
FROM mcr.microsoft.com/dotnet/aspnet:7.0-alpine

ENV ASPNETCORE_ENVIRONMENT Production
ENV ASPNETCORE_URLS http://+:8000
EXPOSE 8000

ENV ConnectionStrings__ShoppingCart ""

RUN adduser -D aspnet
USER aspnet

WORKDIR /app
COPY --chown=aspnet --from=build /dist .

CMD ["dotnet", "Microservices.ShoppingCart.dll"]
