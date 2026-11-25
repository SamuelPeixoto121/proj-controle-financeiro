# Receita do site de demonstração (usada pelo Render): monta as telas do React e a API
# numa imagem só, e a API entrega as telas. Sempre no modo demonstração (Demo__Ativo=true):
# cadastro fechado e "Entrar como visitante" com dados inventados.
# A conexão com o banco NÃO fica aqui: é configurada no painel do Render
# (variável ConnectionStrings__DefaultConnection).

# 1) Telas do React
FROM node:22-alpine AS telas
WORKDIR /frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# 2) API
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS api
WORKDIR /backend
COPY backend/ControleFinanceiro.csproj ./
RUN dotnet restore
COPY backend/ ./
RUN dotnet publish -c Release -o /publicado --no-restore

# 3) O que roda no servidor: a API publicada + as telas na pasta wwwroot
FROM mcr.microsoft.com/dotnet/aspnet:11.0
WORKDIR /app
COPY --from=api /publicado ./
COPY --from=telas /frontend/dist ./wwwroot
# FORWARDEDHEADERS: o Render recebe o HTTPS e repassa para cá; assim a API sabe que é HTTPS
# (e o cookie de login sai marcado como seguro).
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    Demo__Ativo=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "ControleFinanceiro.dll"]
