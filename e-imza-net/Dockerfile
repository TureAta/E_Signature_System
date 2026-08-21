# Adým 1: .NET SDK'sýný içeren build ortamýný kullan
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Önce .csproj dosyasýný kopyala ve baðýmlýlýklarý yükle (bu katman cache'lenir)
COPY *.csproj .
RUN dotnet restore

# Projenin geri kalan tüm dosyalarýný kopyala
COPY . .

# Projeyi build et ve publish et (yayýmla)
RUN dotnet publish "EimzaSignerService.csproj" -c Release -o /app/publish

# Adým 2: Sadece runtime içeren daha küçük bir final imajý oluþtur
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Uygulamanýn baþlangýç noktasýný belirt
ENTRYPOINT ["dotnet", "EimzaSignerService.dll"]