docker build   -t sqlwebapi:debug .
docker run   -p 8082:80 \
-e DOTNET_ENVIRONMENT=Development \
-e DOTNET_EnableDiagnostics=1 \
-e AllowedCorsOrigins="http://localhost:8082" \
-e JWT_SECRET="ThisIsTheSecretKeyForSqlWebApiLonger" \
-e JWT_ISSUER="SqlWebApi" \
-e JWT_AUDIENCE="SWA_User" \
-e JWT_HOURS="10" \
-e  SqlConnectionString="Server=host.docker.internal,1433;Initial Catalog=NorthWind;Persist Security Info=False;User ID=UpTextService;Password=UpService123;MultipleActiveResultSets=False;Encrypt=False;TrustServerCertificate=True;Connection Timeout=30;" \
-e AzureFunctionsJobHost__Logging__LogLevel__Default=Debug \
-e AzureFunctionsJobHost__Logging__LogLevel__Microsoft=Debug \
-e AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
--name SqlWebApi --rm sqlwebapi:debug 
