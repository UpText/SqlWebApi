# SqlWebApi

Build your REST API from SQL Strored Procedures


## Features
- Complete REST API using only SQL
- Server side sorting, filtering and pagination
- Login with JWT tokens
- Run as a container
- Run from a hosted service
- Run from sources
- OpenApi and SwaggerUI built in
- Custom logging to SQL Table
- Secure the service with a minimum trust user

## Getting started

SqlWebApi may be run as:
1. Docker container. This is the easiest way if you have Docker
2. From our hosted service. Just bring your connection string.
3. From sources. Clone the repository and run as a local Azure Functions App.

You will often use multiple methods. For your development machine 
you may want to run multiple containers locally with your app, SqlWebApi and SQL Server.

We will start with securing your SQL server by creating a
service user with a single access right. It should be able to 
execute stored procedure on a single sql schema.

Separation of data schema and api schema is not mandatory but highly recommended for security and production. 

You may have a table like dbo.products and a stored procedure like api.products_get()

The api user have no access to the table and only execute access to the procedure.
In this example we also use api as the name of our service. It offers flexibility when hosting.
We need to give the service a connectionString like:
```
Server=localhost,1433;Database=NorthWind;User ID=upservice;Initial Catalog=NorthWind;Persist Security Info=False;User ID=UpService;Password=UpService123;MultipleActiveResultSets=False;Encrypt=False;TrustServerCertificate=True;Connection Timeout=30;",
```

### Create a minimal trust service user
The service user may be created by:
```sql

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'upservice')
BEGIN
    -- CHANGE THIS PASSWORD:
    CREATE LOGIN upservice
        WITH PASSWORD = N'API_USER_PASSWORD';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'api')
BEGIN
    EXEC ('CREATE SCHEMA api AUTHORIZATION dbo;');
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'upservice')
BEGIN
    CREATE USER upservice FOR LOGIN upservice;
END
GO
GRANT CONNECT TO upservice;
GO
GRANT EXECUTE ON SCHEMA::api TO upservice;
GO

```

### Install locally with Docker
You need docker installed. We will download the latest
build and then run it with a connectionString to the SQL server
You need to update the connection to the correct server and database.
We will use the upservice user created above.

```sh
docker image pull uptext/sqlwebapi:latest
docker run -p 8086:80 -e  SqlConnectionString="Server=192.168.86.20,1433;Initial Catalog=NorthWind;Persist Security Info=False;User ID=upervice;Password=API_USER_PASSWORD_321+;MultipleActiveResultSets=False;Encrypt=False;TrustServerCertificate=True;Connection Timeout=30;" -e AzureFunctionsJobHost__Logging__Console__IsEnabled=true  -d uptext/sqlwebapi:latest
```
Try 
```http request
http://localhost:8086/swa/version
```
We map port 80 on the docker container to port 8086 on the host.

### Use hosted service

At www.uptext.com we will host SqlWebApi as a SAAS offering. 
You will bring your database connection string and start using the API.
It runs on Azure. The service must be able to connect to your database.

- Easy service setup with UI
- Built in logging with UI for the logs
- Security built in
- Code generation for SQL Stored Procedures
- OpenAPI and Swagger

### Use sources

SqlWebApi is built on Azure Functions. If you have DOTNET installed
you may run the API locally or on Azure.

```sh
git clone uptext/sqlwebapi
cd sqlwebapi
```

Add the following local.settings.json file.
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "SqlConnectionString":    "Server=localhost,1433;Database=NorthWind;Initial Catalog=NorthWind;Persist Security Info=False;User ID=UpTextService;Password=UpService123;MultipleActiveResultSets=False;Encrypt=False;TrustServerCertificate=True;Connection Timeout=30;",
    "CONFIG_API_BASEURL" : "http://localhost:8080/swa/api",
    "SqlSchema" : "api"
    
  },

  "Host": {
    "CORS": "*"
  }
}
```
Install Azure Functions on your machine

```shell
func start
```

## Usage

### GET / SELECT 
### POST / INSERT
### PUT / UPDATE
### DELETE / DELETE
### Authentication
### Sorting
### Paging
