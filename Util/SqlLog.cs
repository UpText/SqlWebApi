using Microsoft.Data.SqlClient;

namespace SqlWebApi;

public class SqlLog
{
    static public string sqlLogConnectionString;
	static public string sqlLogTableName;
	static public string sqlLogSchema;
    static public bool CheckOrCreateTable(string sqlSchema, string tableName, string sqlConnectionString)
    {
        sqlLogConnectionString = sqlConnectionString;
        sqlLogTableName = tableName;
        sqlLogSchema = sqlSchema;
        var sqlCreate = $@"
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = '{tableName}' AND TABLE_SCHEMA = '{sqlSchema}'
)
BEGIN
	CREATE TABLE [{sqlSchema}].[{tableName}](
		[Id] [int] IDENTITY(1,1) NOT NULL,
		[ApiName] [nvarchar](max) NULL,
		[SwaServer] [nvarchar](max) NULL,
		[MsUsed] [INT] NULL,
		[TimeStamp] [datetime]  DEFAULT GETDATE(),
		[ReturnValue] int NULL,
		[RequestBody] [nvarchar](max) NULL,
		[ReturnBody] [nvarchar](max) NULL,
	    ExecString NVARCHAR(max),
	    jwt NVARCHAR(max),
		UnexpectedError nvarchar(max) NULL
	) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.key_constraints 
    WHERE parent_object_id = OBJECT_ID('[{sqlSchema}].[{tableName}]') 
    AND type = 'PK'
)
BEGIN
	ALTER TABLE [{sqlSchema}].[{tableName}] ADD  CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED 
	(
		[Id] ASC
	)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
END;
";

        using (SqlConnection conn = new SqlConnection(sqlConnectionString))
        {
	        conn.Open();
	        using (SqlCommand cmd = new SqlCommand(sqlCreate, conn))
	        {
		        int affectedRows = cmd.ExecuteNonQuery();
	        }
        }
        
        return true;
        
    }


    public static void Log(string apiName,  string msUsed, int returnValue, string requestBody, string returnBody, string execString, string jwt, string unexpectedError)
    {
	    if (string.IsNullOrWhiteSpace(sqlLogConnectionString))
		    return;
	    string swaServer = Environment.MachineName;
	    var sqlInsert = $@"
INSERT INTO {sqlLogSchema}.{sqlLogTableName} ( ApiName, SwaServer, MsUsed, ReturnValue, RequestBody, ReturnBody, ExecString, jwt, unexpectedError )
VALUES ( @ApiName, @SwaServer, @MsUsed, @ReturnValue, @RequestBody, @ReturnBody, @ExecString, @jwt, @UnexpectedError )
";
	    using (SqlConnection conn = new SqlConnection(sqlLogConnectionString))
	    {
		    conn.Open();
		    using (SqlCommand cmd = new SqlCommand(sqlInsert, conn))
		    {
			    cmd.Parameters.AddWithValue("@ApiName", apiName);
			    cmd.Parameters.AddWithValue("@SwaServer", swaServer);
			    cmd.Parameters.AddWithValue("@MsUsed", msUsed);
			    cmd.Parameters.AddWithValue("@ReturnValue", returnValue);
			    cmd.Parameters.AddWithValue("@RequestBody", requestBody);
			    cmd.Parameters.AddWithValue("@ReturnBody", returnBody);
			    cmd.Parameters.AddWithValue("@ExecString", execString);
			    cmd.Parameters.AddWithValue("@jwt", jwt);
			    cmd.Parameters.AddWithValue("@UnexpectedError", unexpectedError);
			    cmd.ExecuteNonQuery();
		    }
	    }

    }
}