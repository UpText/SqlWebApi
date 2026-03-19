using System.Text;
using SqlWebApi;

namespace sqlwebapi;

public class SqlCodeBuilder
{
          static public string BuildTableProc(string service, string schema,  TableModel m, string verb, bool search, bool page, bool sort)
        {
            switch (verb.ToLower()) {
                case "get":
                    return BuildGetTableProc(service, schema,m, search, page, sort);
                case "put":
                    return BuildPutTableProc(service, schema, m);
                case "post":
                    return BuildPostTableProc(service, schema,m);
                case "delete":
                    return BuildDeleteTableProc(service, schema, m);
            }
            return "";
        }


        static public string BuildPostTableProc(string service, string schema, TableModel m)
        {
            var cols = (!m.isIdentity) ? m.columns :  m.nonKeyColumns;
            var sb = new StringBuilder(6000)
            .AppendFormat("CREATE OR ALTER PROCEDURE {0}.{1}_post( \r\n",service, m.resource);
            int j = 1;
            foreach (var col in cols)
            {
                char sep = (j < cols.Count) ? ',' : ' ';
                if (col.sqlType.ToLower().IndexOf("char") >= 0)
                    sb.AppendFormat("@{0} {1}({2}) = NULL{3}", col.name, col.sqlType,
                        (col.maxLen == -1) ? "max" : col.maxLen.ToString(), sep);
                else {

                    sb.AppendFormat("@{0} {1} = NULL{2}", col.name, col.sqlType, sep);
                    sb.AppendLine("");
                }
                ++j;
            }
            sb.AppendLine(") AS");
            sb.AppendFormat("INSERT INTO {0}.{1} ( ", schema, m.tableName);
            int i = 1;
            foreach (var col in cols)
            {
                char sep = (i <cols.Count) ? ',' : ' ';
                sb.AppendFormat(" {0} {1} ", col.name, sep);
                i += 1;
            }
            sb.AppendLine(") VALUES (");
            i = 1;
            foreach (var col in cols)
            {
                sb.AppendFormat(" @{0} {1} ", col.name, (i < cols.Count) ? ',' : ' ');
                i += 1;
            }

            sb.AppendLine(")"); 
            sb.AppendFormat("DECLARE @NEWID AS VARCHAR(max) = {0}\r\n", (m.isIdentity) ? "SCOPE_IDENTITY()" : "@" + m.KeyColum);
  
            sb.AppendFormat("EXEC {0}.{1}_Get  @ID=@NEWID \r\n",service, m.tableName);
            sb.AppendLine(" RETURN 200 -- OK");
            return sb.ToString();
        }
        static public string BuildDeleteTableProc(string service, string schema, TableModel m)
        {
            var sb = new StringBuilder(6000)
 
            .AppendFormat("CREATE OR ALTER PROCEDURE {0}.{1}_delete (@ID varchar(max)) \r\n",service, m.resource);
            sb.AppendLine("AS");
            sb.AppendFormat("IF NOT EXISTS(SELECT {2} FROM {0}.{1} WHERE @ID = {2})  \r\n",schema, m.tableName, m.KeyColum);
            sb.AppendLine("BEGIN");
            sb.AppendFormat("   RAISERROR('Unknown {0}',1,1) \r\n", m.resource);
            sb.AppendLine("   RETURN 404");
            sb.AppendLine("END");
            sb.AppendFormat("DELETE FROM {0}.{1}  \r\n", schema, m.tableName);
            sb.AppendFormat("    WHERE @ID = {0} \r\n", m.KeyColum);
            
            sb.AppendLine("RETURN 200 -- OK");
            return sb.ToString();
        }


        static public string BuildPutTableProc(string service, string schema, TableModel m)
        {
            var sb = new StringBuilder(6000)
                .AppendFormat("CREATE OR ALTER PROCEDURE {0}.{1}_put(@ID varchar(max)  \r\n",service, m.resource);
            foreach (var col in m.nonKeyColumns)
            {
                if (col.sqlType.ToLower().IndexOf("char") >=0 )
                    sb.AppendFormat(", @{0} {1}({2})   = NULL ", col.name, col.sqlType,(col.maxLen == -1) ? "max" : col.maxLen.ToString());
                else
                    sb.AppendFormat(", @{0} {1} = NULL ", col.name, col.sqlType);
                sb.AppendLine("");
            }
            sb.AppendLine(") AS");
            sb.AppendFormat("IF NOT EXISTS(SELECT {0} FROM {1}.{2} WHERE @ID = {0})  \r\n", m.KeyColum, schema, m.tableName);
            sb.AppendLine("BEGIN");
            sb.AppendFormat("   RAISERROR('Unknown {0}',1,1) \r\n", m.resource);
            sb.AppendLine  ("   RETURN 404");
            sb.AppendLine("END");
            sb.AppendFormat("UPDATE {0}.{1}  SET \r\n",schema, m.tableName);
            int i = 1;
            foreach (var col in m.nonKeyColumns)
            {
                sb.AppendFormat("    {0} = COALESCE(@{0},{0}){1} \r\n ", col.name, (i<m.nonKeyColumns.Count) ? ',' : ' ');
                i += 1;
            }
            sb.AppendFormat("    WHERE @ID = {0} \r\n", m.KeyColum);
            sb.AppendFormat("EXEC {0}.{1}_Get  @ID=@ID \r\n",service, m.tableName);
            
            sb.AppendLine("RETURN 200 -- OK");
            return sb.ToString();
        }

        static public string BuildGetTableProc(string service, string schema, TableModel m, bool search, bool paging, bool sort)
        {
            var sb = new StringBuilder(6000)
            .AppendFormat("--- Retrieve {0} \r\n", m.resource)
            .AppendFormat("     CREATE OR ALTER PROCEDURE {0}.{1}_get(@ID varchar(max) = NULL  \r\n",service,  m.resource);
            if (search)
                sb.Append("         , @filter varchar(max)=NULL \r\n");
            if (paging) 
                sb.Append("         , @first_row INT = 0, @last_row INT = 1000 \r\n");
            if (sort)
                sb.Append("         , @sort_field NVARCHAR(100) = NULL, @sort_order NVARCHAR(4) = NULL \r\n ");
            sb.Append(    "    ) AS \r\n      SELECT  ");
            sb.AppendFormat("{0} AS id, ", m.KeyColum);
            int i = 0;
            foreach (var col in m.columns)
            {
                i += 1;
                if (col.name.ToLower() != "id")
                {
                    sb.Append(col.name);
                    if (i < m.columns.Count)
                        sb.Append(", ");
                }
            }
            if (paging) 
                sb.Append(      ", COUNT(*) OVER() AS total_rows ");
            sb.AppendFormat(    "\r\n           FROM {0}.{1}  \r\n", schema, m.tableName);
            sb.AppendFormat(    "           WHERE (@ID IS NULL OR @ID = {0}) \r\n", m.KeyColum);
            if (search && m.columns.Count > 1)
                sb.AppendFormat("           AND (@filter IS NULL OR @filter = {0} OR CHARINDEX(@filter,CAST({1} AS varchar)) > 0)\r\n ", m.KeyColum, m.columns[1].name);
            sb.AppendFormat(    "           ORDER BY\r\n");
            if (sort)
            {
                foreach (var col in m.columns)
                {
                    if (col.sqlType.ToLower().IndexOf("char") >= 0  || col.sqlType.ToLower().IndexOf("int") >= 0)
                    {
                        var name = col.name;
                        sb.AppendFormat("           CASE WHEN @sort_field = '{0}' AND @sort_order = 'ASC' THEN {0} END ASC, \r\n ", name);
                        sb.AppendFormat("           CASE WHEN @sort_field = '{0}' AND @sort_order = 'DESC' THEN {0} END DESC, \r\n ", name);
                    }
                }
                sb.AppendFormat(    "           CASE WHEN @sort_field IS NULL THEN {0} END ASC \r\n", m.columns.First().name);
            } else 
                sb.AppendFormat("           {0} \r\n", m.columns.First().name);


            if (paging)
            {
                sb.Append(      "           OFFSET @first_row ROWS\r\n");
                sb.Append(      "           FETCH NEXT (@last_row - @first_row + 1) ROWS ONLY  \r\n");
            }
            return sb.ToString();
        }
 
}