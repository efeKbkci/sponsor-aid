# 1. Aşama: SDK ile Derleme
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Protobuf sözleşmesini ve bağımlılık dosyalarını kopyala
COPY contracts/ ./contracts/
COPY src/SponsorAid.Api/*.csproj ./src/SponsorAid.Api/
COPY src/SponsorAid.Application/*.csproj ./src/SponsorAid.Application/
COPY src/SponsorAid.Domain/*.csproj ./src/SponsorAid.Domain/
COPY src/SponsorAid.Infrastructure/*.csproj ./src/SponsorAid.Infrastructure/

# Paketleri restore et (Bağımlılıklar değişmedikçe bu katman önbellekten gelir)
RUN dotnet restore src/SponsorAid.Api/SponsorAid.Api.csproj

# Tüm kaynak kodları içeri al ve derle
COPY src/ ./src/
RUN dotnet publish src/SponsorAid.Api/SponsorAid.Api.csproj -c Release -o /app/publish --no-restore

# 2. Aşama: Üretime Hazır, Hafif ve Temiz Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Yalnızca derlenmiş DLL çıktılarını al
COPY --from=build /app/publish .

# Paylaşılan dosya klasörünü hazırla
RUN mkdir -p /app/wwwroot/uploads

ENTRYPOINT ["dotnet", "SponsorAid.Api.dll"]