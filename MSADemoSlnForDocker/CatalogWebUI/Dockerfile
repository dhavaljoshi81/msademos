FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["CatalogWebUI/CatalogWebUI.csproj", "CatalogWebUI/"]
RUN dotnet restore "CatalogWebUI/CatalogWebUI.csproj"

COPY CatalogWebUI/ CatalogWebUI/
WORKDIR "/src/CatalogWebUI"
RUN dotnet publish "CatalogWebUI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
RUN rm -rf ./*
COPY --from=build /app/publish/wwwroot .

# Write a clean, inline Nginx configuration directly inside the Linux container
RUN printf 'server {\n\
    listen 80;\n\
    server_name localhost;\n\
    location / {\n\
        root /usr/share/nginx/html;\n\
        try_files $uri $uri/ /index.html;\n\
    }\n\
    error_page 500 502 503 504 /50x.html;\n\
    location = /50x.html {\n\
        root /usr/share/nginx/html;\n\
    }\n\
}\n' > /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]