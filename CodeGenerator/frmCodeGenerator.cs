using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CodeGenerator
{
    public partial class frmCodeGenerator : Form
    {
        public frmCodeGenerator()
        {
            InitializeComponent();
        }

        private void btnLoadDatabases_Click(object sender, EventArgs e)
        {
            cbDatabases.Items.Clear();

            using (SqlConnection conn = new SqlConnection(txtConnectionString.Text))
            {
                string query = "SELECT name FROM sys.databases WHERE database_id > 4";

                SqlCommand cmd = new SqlCommand(query, conn);

                conn.Open();

                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    cbDatabases.Items.Add(reader["name"].ToString());
                }

                reader.Close();

                if (cbDatabases.Items.Count > 0)
                {
                    cbDatabases.Enabled = true;
                    cbDatabases.SelectedIndex = -1;
                    cbDatabases.Focus();
                    btnLoadDatabases.Enabled = false;
                }
            }
        }

        private void cbDatabases_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbDatabases.SelectedIndex == -1)
                return;

            clbTables.Items.Clear();

            string cs = txtConnectionString.Text;

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(cs);

            builder.InitialCatalog = cbDatabases.SelectedItem.ToString();

            using (SqlConnection conn = new SqlConnection(builder.ConnectionString))
            {
                string query = @"SELECT TABLE_NAME
                                 FROM INFORMATION_SCHEMA.TABLES
                                 WHERE TABLE_TYPE = 'BASE TABLE'
                                 AND TABLE_NAME <> 'sysdiagrams'";

                SqlCommand cmd = new SqlCommand(query, conn);

                conn.Open();

                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    clbTables.Items.Add(reader["TABLE_NAME"].ToString());
                }

                reader.Close();
            }

            clbTables.Enabled = true;
            chkSelectAll.Enabled = true;

            CheckGenerateButtonState();
        }

        private void chkSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < clbTables.Items.Count; i++)
            {
                clbTables.SetItemChecked(i, chkSelectAll.Checked);
            }

            txtOutputPath.Enabled = chkSelectAll.Checked;
            btnBrowse.Enabled = chkSelectAll.Checked;

            CheckGenerateButtonState();
        }

        private void CheckGenerateButtonState()
        {
            bool hasOutputFolder = !string.IsNullOrWhiteSpace(txtOutputPath.Text);
            bool hasDatabase = cbDatabases.SelectedIndex != -1;
            bool hasTables = clbTables.CheckedItems.Count > 0;

            btnGenerate.Enabled = (hasOutputFolder && hasDatabase && hasTables);
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    txtOutputPath.Text = dlg.SelectedPath;

                    CheckGenerateButtonState();
                }
            }
        }

        private void btnGenerate_Click(object sender, EventArgs e)
        {
            string databaseName = cbDatabases.SelectedItem.ToString();
            string basePath = txtOutputPath.Text;

            string dalPath = Path.Combine(basePath, databaseName + ".DataAccess");
            Directory.CreateDirectory(dalPath);

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(txtConnectionString.Text);
            builder.InitialCatalog = databaseName;

            foreach (var item in clbTables.CheckedItems)
            {
                string tableName = item.ToString();

                string entityName = tableName.EndsWith("s") ? tableName.Substring(0, tableName.Length - 1) : tableName;

                DataTable columns = GetTableColumns(builder.ConnectionString, tableName);

                bool hasPrimaryKey = columns.AsEnumerable().Any(r => Convert.ToInt32(r["IS_PRIMARY_KEY"]) == 1);

                string dalCode = GenerateDALClass(databaseName, tableName, columns, hasPrimaryKey);

                string filePath = Path.Combine(dalPath, $"cls{entityName}Data.cs");

                File.WriteAllText(filePath, dalCode);
            }

            MessageBox.Show("DAL Generated Successfully ✔");

            string bllPath = Path.Combine(basePath, databaseName + ".BusinessLogic");
            Directory.CreateDirectory(bllPath);

            foreach (var item in clbTables.CheckedItems)
            {
                string tableName = item.ToString();

                string entityName = tableName.EndsWith("s") ? tableName.Substring(0, tableName.Length - 1) : tableName;

                DataTable columns = GetTableColumns(builder.ConnectionString, tableName);

                bool hasPrimaryKey = columns.AsEnumerable().Any(r => Convert.ToInt32(r["IS_PRIMARY_KEY"]) == 1);

                string bllCode = GenerateBLLClass(databaseName, tableName, columns, hasPrimaryKey);

                string filePath = Path.Combine(bllPath, $"cls{entityName}.cs");

                File.WriteAllText(filePath, bllCode);
            }

            MessageBox.Show("BLL Generated Successfully ✔");

            _ResetAll();
        }

        private void _ResetAll()
        {
            cbDatabases.Items.Clear();
            clbTables.Items.Clear();
            txtOutputPath.Clear();
            chkSelectAll.Checked = false;

            cbDatabases.Enabled = false;
            chkSelectAll.Enabled = false;
            clbTables.Enabled = false;

            txtOutputPath.Enabled = false;
            btnBrowse.Enabled = false;
            btnGenerate.Enabled = false;

            btnLoadDatabases.Enabled = true;
            btnLoadDatabases.Focus();
        }

        private DataTable GetTableColumns(string connectionString, string tableName)
        {
            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"SELECT c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE,
                                 CASE 
                                     WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 
                                     ELSE 0 
                                 END AS IS_PRIMARY_KEY
                                 FROM INFORMATION_SCHEMA.COLUMNS c
                                 LEFT JOIN
                                 (
                                 	SELECT 
                                         kcu.TABLE_NAME,
                                         kcu.COLUMN_NAME
                                     FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                                     INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                                 WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                                 ) pk
                                 ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME
                                 WHERE c.TABLE_NAME = @TableName
                                 ORDER BY c.ORDINAL_POSITION;";

                SqlCommand cmd = new SqlCommand(query, conn);

                cmd.Parameters.AddWithValue("@TableName", tableName);

                conn.Open();

                SqlDataReader reader = cmd.ExecuteReader();

                dt.Load(reader);

                reader.Close();
            }

            return dt;
        }

        private string ConvertSqlTypeToCSharp(string sqlType)
        {
            switch (sqlType)
            {
                case "int":
                    return "int";
                case "bigint":
                    return "long";
                case "smallint":
                    return "short";
                case "bit":
                    return "bool";
                case "decimal":
                case "numeric":
                case "money":
                case "smallmoney":
                    return "decimal";
                case "datetime":
                case "smalldatetime":
                    return "DateTime";
                case "date":
                    return "DateTime";
                case "float":
                    return "float";
                default:
                    return "string";
            }
        }

        private string GenerateDALClass(string databaseName, string tableName, DataTable columns, bool hasPrimaryKey)
        {
            StringBuilder sb = new StringBuilder();

            string entityName = tableName.EndsWith("s") ? tableName.Substring(0, tableName.Length - 1) : tableName;

            string className = "cls" + entityName + "Data";

            // ================= Primary Key Detection (FIX) =================
            DataRow pkRow = null;

            if (hasPrimaryKey)
            {
                DataRow[] pkRows = columns.Select("IS_PRIMARY_KEY = 1");

                // If there is only one Primary Key
                if (pkRows.Length == 1)
                    pkRow = pkRows[0];
                else
                    hasPrimaryKey = false; // Composite PK or error (We Cancel it)
            }

            string pkName = hasPrimaryKey ? pkRow["COLUMN_NAME"].ToString() : "";
            string pkType = hasPrimaryKey ? ConvertSqlTypeToCSharp(pkRow["DATA_TYPE"].ToString()) : "";

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Data;");
            sb.AppendLine("using System.Data.SqlClient;");
            sb.AppendLine();
            sb.AppendLine($"namespace {databaseName}.DataAccess");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");

            // ================= Get All =================
            sb.AppendLine($"        public static DataTable GetAll{tableName}()");
            sb.AppendLine("        {");
            sb.AppendLine("            DataTable dt = new DataTable();");
            sb.AppendLine();
            sb.AppendLine($"            string query = \"SELECT * FROM {tableName}\";");
            sb.AppendLine();
            sb.AppendLine("            using (SqlConnection connection = new SqlConnection(clsDataAccessSettings.ConnectionString))");
            sb.AppendLine("            {");
            sb.AppendLine("                using (SqlCommand cmd = new SqlCommand(query, connection))");
            sb.AppendLine("                {");
            sb.AppendLine("                    connection.Open();");
            sb.AppendLine();
            sb.AppendLine("                    SqlDataReader reader = cmd.ExecuteReader();");
            sb.AppendLine();
            sb.AppendLine("                    if (reader.HasRows)");
            sb.AppendLine("                        dt.Load(reader);");
            sb.AppendLine();
            sb.AppendLine("                    reader.Close();");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return dt;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // If there is no PK → only GetAll

            if (!hasPrimaryKey)
            {
                sb.AppendLine("    }");
                sb.AppendLine("}");

                return sb.ToString();
            }

            // ================= Add New =================
            sb.Append($"        public static int AddNew{entityName}(");

            bool first = true;

            foreach (DataRow row in columns.Rows)
            {
                if (row["COLUMN_NAME"].ToString() == pkName)
                    continue;

                string colType = ConvertSqlTypeToCSharp(row["DATA_TYPE"].ToString());
                bool isNullable = row["IS_NULLABLE"].ToString() == "YES";

                if (isNullable && colType != "string")
                    colType += "?";

                if (!first) 
                    sb.Append(", ");

                sb.Append($"{colType} {row["COLUMN_NAME"]}");

                first = false;
            }

            sb.AppendLine(")");
            sb.AppendLine("        {");
            sb.AppendLine("            int insertedID = -1;");
            sb.AppendLine();

            sb.Append($"            string query = \"INSERT INTO {tableName} (");

            first = true;

            foreach (DataRow row in columns.Rows)
            {
                if (row["COLUMN_NAME"].ToString() == pkName)
                    continue;

                if (!first)
                    sb.Append(", ");

                sb.Append(row["COLUMN_NAME"]);

                first = false;
            }

            sb.Append(") VALUES (");

            first = true;

            foreach (DataRow row in columns.Rows)
            {
                if (row["COLUMN_NAME"].ToString() == pkName)
                    continue;

                if (!first)
                    sb.Append(", ");

                sb.Append("@" + row["COLUMN_NAME"]);

                first = false;
            }

            sb.AppendLine("); SELECT SCOPE_IDENTITY();\";");
            sb.AppendLine();

            sb.AppendLine("            using (SqlConnection connection = new SqlConnection(clsDataAccessSettings.ConnectionString))");
            sb.AppendLine("            {");
            sb.AppendLine("                using (SqlCommand cmd = new SqlCommand(query, connection))");
            sb.AppendLine("                {");

            foreach (DataRow row in columns.Rows)
            {
                if (row["COLUMN_NAME"].ToString() == pkName)
                    continue;

                string colName = row["COLUMN_NAME"].ToString();
                bool isNullable = row["IS_NULLABLE"].ToString() == "YES";

                if (isNullable)
                    sb.AppendLine($"                    cmd.Parameters.AddWithValue(\"@{colName}\", {colName} ?? (object)DBNull.Value);");
                else
                    sb.AppendLine($"                    cmd.Parameters.AddWithValue(\"@{colName}\", {colName});");
            }

            sb.AppendLine();
            sb.AppendLine("                    connection.Open();");
            sb.AppendLine();
            sb.AppendLine("                    object result = cmd.ExecuteScalar();");
            sb.AppendLine();
            sb.AppendLine("                    if (result != null)");
            sb.AppendLine("                        int.TryParse(result.ToString(), out insertedID);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return insertedID;");
            sb.AppendLine("        }");
            sb.AppendLine();


            // ================= Get By ID =================
            sb.Append($"        public static bool Get{entityName}ByID({pkType} {pkName}");

            foreach (DataRow row in columns.Rows)
            {
                if (row["COLUMN_NAME"].ToString() == pkName) 
                    continue;

                string colType = ConvertSqlTypeToCSharp(row["DATA_TYPE"].ToString());
                bool isNullable = row["IS_NULLABLE"].ToString() == "YES";

                if (isNullable && colType != "string")
                    colType += "?";

                sb.Append($", ref {colType} {row["COLUMN_NAME"]}");
            }

            sb.AppendLine(")");
            sb.AppendLine("        {");
            sb.AppendLine("            bool isFound = false;");
            sb.AppendLine();
            sb.AppendLine($"            string query = \"SELECT * FROM {tableName} WHERE {pkName} = @{pkName}\";");
            sb.AppendLine();
            sb.AppendLine("            using (SqlConnection connection = new SqlConnection(clsDataAccessSettings.ConnectionString))");
            sb.AppendLine("            {");
            sb.AppendLine("                using (SqlCommand cmd = new SqlCommand(query, connection))");
            sb.AppendLine("                {");
            sb.AppendLine($"                    cmd.Parameters.AddWithValue(\"@{pkName}\", {pkName});");
            sb.AppendLine();
            sb.AppendLine("                    connection.Open();");
            sb.AppendLine();
            sb.AppendLine("                    SqlDataReader reader = cmd.ExecuteReader();");
            sb.AppendLine();
            sb.AppendLine("                    if (reader.Read())");
            sb.AppendLine("                    {");
            sb.AppendLine("                        isFound = true;");
            sb.AppendLine();

            foreach (DataRow row in columns.Rows)
            {
                if (row["COLUMN_NAME"].ToString() == pkName) 
                    continue;

                string colName = row["COLUMN_NAME"].ToString();
                string colType = ConvertSqlTypeToCSharp(row["DATA_TYPE"].ToString());
                bool isNullable = row["IS_NULLABLE"].ToString() == "YES";

                if (isNullable && colType != "string")
                {
                    sb.AppendLine($"                        {colName} = reader[\"{colName}\"] == DBNull.Value ? null : ({colType})reader[\"{colName}\"];");
                }
                else
                {
                    sb.AppendLine($"                        {colName} = ({colType})reader[\"{colName}\"];");
                }
            }

            sb.AppendLine("                    }");
            sb.AppendLine();
            sb.AppendLine("                    reader.Close();");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return isFound;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // ================= Update =================
            sb.Append($"        public static bool Update{entityName}({pkType} {pkName}");

            foreach (DataRow row in columns.Rows)
            {
                if (row["COLUMN_NAME"].ToString() == pkName) 
                    continue;

                string colType = ConvertSqlTypeToCSharp(row["DATA_TYPE"].ToString());
                bool isNullable = row["IS_NULLABLE"].ToString() == "YES";

                if (isNullable && colType != "string")
                    colType += "?";

                sb.Append($", {colType} {row["COLUMN_NAME"]}");
            }

            sb.AppendLine(")");
            sb.AppendLine("        {");
            sb.AppendLine("            int rowsAffected = 0;");
            sb.AppendLine();

            sb.Append($"            string query = \"UPDATE {tableName} SET ");

            first = true;

            foreach (DataRow row in columns.Rows)
            {
                if (row["COLUMN_NAME"].ToString() == pkName) 
                    continue;

                if (!first)
                    sb.Append(", ");

                sb.Append($"{row["COLUMN_NAME"]} = @{row["COLUMN_NAME"]}");

                first = false;
            }

            sb.AppendLine($" WHERE {pkName} = @{pkName}\";");
            sb.AppendLine();

            sb.AppendLine("            using (SqlConnection connection = new SqlConnection(clsDataAccessSettings.ConnectionString))");
            sb.AppendLine("            {");
            sb.AppendLine("                using (SqlCommand cmd = new SqlCommand(query, connection))");
            sb.AppendLine("                {");
            sb.AppendLine($"                    cmd.Parameters.AddWithValue(\"@{pkName}\", {pkName});");

            foreach (DataRow row in columns.Rows)
            {
                if (row["COLUMN_NAME"].ToString() == pkName) 
                    continue;

                bool isNullable = row["IS_NULLABLE"].ToString() == "YES";
                string colName = row["COLUMN_NAME"].ToString();

                if (isNullable)
                    sb.AppendLine($"                    cmd.Parameters.AddWithValue(\"@{colName}\", {colName} ?? (object)DBNull.Value);");
                else
                    sb.AppendLine($"                    cmd.Parameters.AddWithValue(\"@{colName}\", {colName});");
            }

            sb.AppendLine();
            sb.AppendLine("                    connection.Open();");
            sb.AppendLine();
            sb.AppendLine("                    rowsAffected = cmd.ExecuteNonQuery();");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return (rowsAffected > 0);");
            sb.AppendLine("        }");
            sb.AppendLine();


            // ================= Delete =================
            sb.AppendLine($"        public static bool Delete{entityName}({pkType} {pkName})");
            sb.AppendLine("        {");
            sb.AppendLine("            int rowsAffected = 0;");
            sb.AppendLine();
            sb.AppendLine($"            string query = \"DELETE FROM {tableName} WHERE {pkName} = @{pkName}\";");
            sb.AppendLine();

            sb.AppendLine("            using (SqlConnection connection = new SqlConnection(clsDataAccessSettings.ConnectionString))");
            sb.AppendLine("            {");
            sb.AppendLine("                using (SqlCommand cmd = new SqlCommand(query, connection))");
            sb.AppendLine("                {");
            sb.AppendLine($"                    cmd.Parameters.AddWithValue(\"@{pkName}\", {pkName});");
            sb.AppendLine();
            sb.AppendLine("                    connection.Open();");
            sb.AppendLine();
            sb.AppendLine("                    rowsAffected = cmd.ExecuteNonQuery();");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return (rowsAffected > 0);");
            sb.AppendLine("        }");
            sb.AppendLine();


            // ================= Is Exists =================
            sb.AppendLine($"        public static bool Is{entityName}Exists({pkType} {pkName})");
            sb.AppendLine("        {");
            sb.AppendLine("            bool isFound = false;");
            sb.AppendLine();
            sb.AppendLine($"            string query = \"SELECT 1 FROM {tableName} WHERE {pkName} = @{pkName}\";");
            sb.AppendLine();

            sb.AppendLine("            using (SqlConnection connection = new SqlConnection(clsDataAccessSettings.ConnectionString))");
            sb.AppendLine("            {");
            sb.AppendLine("                using (SqlCommand cmd = new SqlCommand(query, connection))");
            sb.AppendLine("                {");
            sb.AppendLine($"                    cmd.Parameters.AddWithValue(\"@{pkName}\", {pkName});");
            sb.AppendLine();
            sb.AppendLine("                    connection.Open();");
            sb.AppendLine();
            sb.AppendLine("                    SqlDataReader reader = cmd.ExecuteReader();");
            sb.AppendLine();
            sb.AppendLine("                    isFound = reader.HasRows;");
            sb.AppendLine();
            sb.AppendLine("                    reader.Close();");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return isFound;");
            sb.AppendLine("        }");


            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateBLLClass(string databaseName, string tableName, DataTable columns, bool hasPrimaryKey)
        {
            StringBuilder sb = new StringBuilder();

            string entityName = tableName.EndsWith("s") ? tableName.Substring(0, tableName.Length - 1) : tableName;

            string className = "cls" + entityName;

            // ================= Primary Key Detection (FIX) =================
            DataRow pkRow = null;

            if (hasPrimaryKey)
            {
                DataRow[] pkRows = columns.Select("IS_PRIMARY_KEY = 1");

                // If there is only one Primary Key
                if (pkRows.Length == 1)
                    pkRow = pkRows[0];
                else
                    hasPrimaryKey = false; // Composite PK or error (We Cancel it)
            }

            string pkName = hasPrimaryKey ? pkRow["COLUMN_NAME"].ToString() : "";
            string pkType = hasPrimaryKey ? ConvertSqlTypeToCSharp(pkRow["DATA_TYPE"].ToString()) : "";

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Data;");
            sb.AppendLine($"using {databaseName}.DataAccess;");
            sb.AppendLine();
            sb.AppendLine($"namespace {databaseName}.BusinessLogic");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");

            // ============================================================
            //          If there is no Primary Key → only GetAll
            // ============================================================

            if (!hasPrimaryKey)
            {
                sb.AppendLine($"        public static DataTable GetAll{tableName}()");
                sb.AppendLine("        {");
                sb.AppendLine($"            return cls{entityName}Data.GetAll{tableName}();");
                sb.AppendLine("        }");

                sb.AppendLine("    }");
                sb.AppendLine("}");

                return sb.ToString();
            }

            // ================= Enum =================
            sb.AppendLine("        public enum enMode { AddNew = 0, Update = 1 };");
            sb.AppendLine("        public enMode Mode { get; set; }");
            sb.AppendLine();

            // ================= Properties =================
            foreach (DataRow row in columns.Rows)
            {
                string colName = row["COLUMN_NAME"].ToString();
                string colType = ConvertSqlTypeToCSharp(row["DATA_TYPE"].ToString());
                bool isNullable = row["IS_NULLABLE"].ToString() == "YES";

                if (isNullable && colType != "string")
                    colType += "?";

                sb.AppendLine($"        public {colType} {colName} {{ get; set; }}");
            }

            sb.AppendLine();

            // ================= Constructor AddNew =================
            sb.AppendLine($"        public {className}()");
            sb.AppendLine("        {");

            foreach (DataRow row in columns.Rows)
            {
                string colName = row["COLUMN_NAME"].ToString();
                string colType = ConvertSqlTypeToCSharp(row["DATA_TYPE"].ToString());
                bool isNullable = row["IS_NULLABLE"].ToString() == "YES";

                if (colName == pkName)
                    sb.AppendLine($"            this.{colName} = -1;");
                else if (isNullable)
                    sb.AppendLine($"            this.{colName} = null;");
                else if (colType == "string")
                    sb.AppendLine($"            this.{colName} = string.Empty;");
                else if (colType == "DateTime")
                    sb.AppendLine($"            this.{colName} = DateTime.Now;");
                else if (colType == "bool")
                    sb.AppendLine($"            this.{colName} = false;");
                else
                    sb.AppendLine($"            this.{colName} = default;");
            }

            sb.AppendLine();
            sb.AppendLine("            Mode = enMode.AddNew;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // ================= Constructor Update =================
            sb.Append($"        private {className}(");

            for (int i = 0; i < columns.Rows.Count; i++)
            {
                string colType = ConvertSqlTypeToCSharp(columns.Rows[i]["DATA_TYPE"].ToString());
                bool isNullable = columns.Rows[i]["IS_NULLABLE"].ToString() == "YES";

                if (isNullable && colType != "string")
                    colType += "?";

                sb.Append(colType + " " + columns.Rows[i]["COLUMN_NAME"]);

                if (i < columns.Rows.Count - 1)
                    sb.Append(", ");
            }

            sb.AppendLine(")");
            sb.AppendLine("        {");

            foreach (DataRow row in columns.Rows)
            {
                string colName = row["COLUMN_NAME"].ToString();
                sb.AppendLine($"            this.{colName} = {colName};");
            }

            sb.AppendLine();
            sb.AppendLine("            Mode = enMode.Update;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // ================= Find =================
            sb.AppendLine($"        public static {className} Find{entityName}({pkType} {pkName})");
            sb.AppendLine("        {");

            foreach (DataRow row in columns.Rows)
            {
                string colName = row["COLUMN_NAME"].ToString();

                if (colName == pkName)
                    continue;

                string colType = ConvertSqlTypeToCSharp(row["DATA_TYPE"].ToString());
                bool isNullable = row["IS_NULLABLE"].ToString() == "YES";

                if (isNullable && colType != "string")
                    colType += "?";

                sb.AppendLine($"            {colType} {colName} = default;");
            }

            sb.AppendLine();
            sb.Append($"            if (cls{entityName}Data.Get{entityName}ByID({pkName}");

            for (int i = 1; i < columns.Rows.Count; i++)
                sb.Append($", ref {columns.Rows[i]["COLUMN_NAME"]}");

            sb.AppendLine("))");

            sb.Append($"                return new {className}(");

            for (int i = 0; i < columns.Rows.Count; i++)
            {
                sb.Append(columns.Rows[i]["COLUMN_NAME"]);

                if (i < columns.Rows.Count - 1)
                    sb.Append(", ");
            }

            sb.AppendLine(");");
            sb.AppendLine();
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // ================= Save =================
            sb.AppendLine("        public bool Save()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (Mode == enMode.AddNew)");
            sb.AppendLine("            {");
            sb.AppendLine($"                if (_AddNew{entityName}())");
            sb.AppendLine("                {");
            sb.AppendLine("                    Mode = enMode.Update;");
            sb.AppendLine();
            sb.AppendLine("                    return true;");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine($"            return _Update{entityName}();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // ================= Add New =================
            sb.AppendLine($"        private bool _AddNew{entityName}()");
            sb.AppendLine("        {");
            sb.Append($"            this.{pkName} = cls{entityName}Data.AddNew{entityName}(");

            for (int i = 1; i < columns.Rows.Count; i++)
            {
                sb.Append(columns.Rows[i]["COLUMN_NAME"]);

                if (i < columns.Rows.Count - 1)
                    sb.Append(", ");
            }

            sb.AppendLine(");");
            sb.AppendLine();
            sb.AppendLine($"            return (this.{pkName} != -1);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // ================= Update =================
            sb.AppendLine($"        private bool _Update{entityName}()");
            sb.AppendLine("        {");
            sb.Append($"            return cls{entityName}Data.Update{entityName}({pkName}");

            for (int i = 1; i < columns.Rows.Count; i++)
                sb.Append(", " + columns.Rows[i]["COLUMN_NAME"]);

            sb.AppendLine(");");
            sb.AppendLine("        }");
            sb.AppendLine();

            // ================= Delete =================
            sb.AppendLine($"        public static bool Delete{entityName}({pkType} {pkName})");
            sb.AppendLine("        {");
            sb.AppendLine($"            return cls{entityName}Data.Delete{entityName}({pkName});");
            sb.AppendLine("        }");
            sb.AppendLine();

            // ================= Exists =================
            sb.AppendLine($"        public static bool Is{entityName}Exists({pkType} {pkName})");
            sb.AppendLine("        {");
            sb.AppendLine($"            return cls{entityName}Data.Is{entityName}Exists({pkName});");
            sb.AppendLine("        }");
            sb.AppendLine();

            // ================= Get All =================
            sb.AppendLine($"        public static DataTable GetAll{tableName}()");
            sb.AppendLine("        {");
            sb.AppendLine($"            return cls{entityName}Data.GetAll{tableName}();");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private void _UpdateClock()
        {
            lblDateTime.Text = DateTime.Now.ToString("dd/MM/yyyy  HH:mm:ss");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _UpdateClock();
            timerDateTime.Start();

            this.ActiveControl = btnLoadDatabases;
            btnLoadDatabases.Focus();
        }

        private void clbTables_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate
            {
                bool hasCheckedTables = (clbTables.CheckedItems.Count > 0);

                btnBrowse.Enabled = hasCheckedTables;
                txtOutputPath.Enabled = hasCheckedTables;

                CheckGenerateButtonState();
            });
        }

        private void timerClock_Tick(object sender, EventArgs e)
        {
            _UpdateClock();
        }
    }
}