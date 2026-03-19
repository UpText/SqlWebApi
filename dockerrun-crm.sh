#docker build  --platform linux/amd64 -t sqlwebapi:debug .
docker buildx build --platform linux/amd64 -t swa:debug --load .
docker run  --platform linux/amd64 -p 8083:80 \
-e DOTNET_ENVIRONMENT=Production \
-e DOTNET_EnableDiagnostics=0 \
-e AllowedCorsOrigins="http://localhost:8082" \
-e JWT_SECRET="ThisIsTheSecretKeyForSqlWebApiLonger" \
-e JWT_ISSUER="SqlWebApi" \
-e JWT_AUDIENCE="SWA_User" \
-e JWT_HOURS="10" \
-e  SqlConnectionString="Server=host.docker.internal,1433;Initial Catalog=crm2;Persist Security Info=False;User ID=up-service-user;Password=UpServiceUserStrongPassword!23;MultipleActiveResultSets=False;Encrypt=False;TrustServerCertificate=True;Connection Timeout=30;" \
-e AzureFunctionsJobHost__Logging__LogLevel__Default=Warning \
-e AzureFunctionsJobHost__Logging__LogLevel__Microsoft=Warning \
-e AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
-e SQLWEBAPI__SQLSCHEMA=crmapi \
-e SQLWEBAPI__SQLCONNECTIONSTRING='Server=host.docker.internal,1433;Initial Catalog=crm2;Persist Security Info=False;User ID=up-service-user;Password=UpServiceUserStrongPassword!23;MultipleActiveResultSets=False;Encrypt=False;TrustServerCertificate=True;Connection Timeout=30;' \
--name SqlWebApi-Crm --rm swa:debug 


  
