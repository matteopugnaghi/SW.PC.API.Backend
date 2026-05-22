using Microsoft.Data.Sqlite;

var db = args.Length > 0 ? args[0] : @"..\..\Projects\A72.TOUTWP\data\project.db";
using var cn = new SqliteConnection($"Data Source={db};Mode=ReadOnly");
cn.Open();

Console.WriteLine("=== Tables ===");
using (var cmd = cn.CreateCommand())
{
    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
    using var r = cmd.ExecuteReader();
    while (r.Read()) Console.WriteLine($"  {r[0]}");
}

string tbl = "AuditEntries";
using (var cmd = cn.CreateCommand())
{
    cmd.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND (name LIKE '%udit%' OR name LIKE '%Audit%')";
    using var r = cmd.ExecuteReader();
    if (r.Read()) tbl = r.GetString(0);
}
Console.WriteLine($"\n=== Using audit table: {tbl} ===\n");

Console.WriteLine($"=== Columns of {tbl} ===");
using (var cmd = cn.CreateCommand())
{
    cmd.CommandText = $"PRAGMA table_info({tbl})";
    using var r = cmd.ExecuteReader();
    while (r.Read()) Console.WriteLine($"  {r[1]} ({r[2]})");
}

Console.WriteLine("\n=== Last 30 Audit Entries (any category) ===");
using (var cmd = cn.CreateCommand())
{
    cmd.CommandText = $"SELECT datetime(Timestamp,'localtime'), Category, Action, Result, substr(Details,1,150) FROM {tbl} ORDER BY Timestamp DESC LIMIT 30";
    using var r = cmd.ExecuteReader();
    while (r.Read())
        Console.WriteLine($"{r[0]} | {r[1],-12}/{r[2],-25} {r[3],-8} | {r[4]}");
}

Console.WriteLine();
Console.WriteLine("=== Backup-related ONLY (Category=Backup OR Action LIKE 'Backup%') ===");
using (var cmd = cn.CreateCommand())
{
    cmd.CommandText = $"SELECT datetime(Timestamp,'localtime'), Category, Action, Result, UserId, substr(Details,1,200) FROM {tbl} WHERE Category LIKE '%Backup%' OR Action LIKE '%Backup%' ORDER BY Timestamp DESC LIMIT 30";
    using var r = cmd.ExecuteReader();
    int n = 0;
    while (r.Read())
    {
        n++;
        Console.WriteLine($"{r[0]} | {r[1],-10}/{r[2],-25} {r[3],-8} user={r[4]} | {r[5]}");
    }
    Console.WriteLine($"--- Total backup-related rows: {n}");
}
