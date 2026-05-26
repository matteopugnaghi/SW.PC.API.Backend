using System;
using Microsoft.Data.Sqlite;
var c = new SqliteConnection("Data Source=Projects\A72.TOUTWP\data\project.db");
c.Open();
var cmd = c.CreateCommand();
cmd.CommandText = "PRAGMA table_info(ExportTasks)";
using var r = cmd.ExecuteReader();
while(r.Read()) Console.WriteLine(r.GetString(1) + " | " + r.GetString(2) + " | notnull=" + r.GetInt32(3));
