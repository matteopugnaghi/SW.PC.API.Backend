using Microsoft.Data.Sqlite;

var db = @"c:\Users\mpugnaghi\Documents\Work_In_Process\_Web\AI test\SW.PC.API.Backend_\Projects\A72.TOUTWP\data\project.db";
using var cn = new SqliteConnection($"Data Source={db};Mode=ReadOnly");
cn.Open();

Console.WriteLine($"UtcNow = {DateTime.UtcNow:O}");
Console.WriteLine($"Local  = {DateTime.Now:O}\n");

Console.WriteLine("=== SMM_Groups ===");
using (var cmd = cn.CreateCommand())
{
    cmd.CommandText = "SELECT Id, GroupName, ContinuousReadIntervalSec, ContinuousRetentionDays, RunningBitVar FROM SMM_Groups WHERE ReadFrequency='Continuous'";
    using var r = cmd.ExecuteReader();
    while (r.Read())
        Console.WriteLine($"  id={r["Id"]} | {r["GroupName"]} | M={r["ContinuousReadIntervalSec"]}s | RetDays={r["ContinuousRetentionDays"]} | RunBit={r["RunningBitVar"]}");
}

Console.WriteLine("\n=== Continuous readings: total + min/max ===");
using (var cmd = cn.CreateCommand())
{
    cmd.CommandText = "SELECT GroupId, COUNT(*) N, MIN(Timestamp) MinTs, MAX(Timestamp) MaxTs FROM SMM_Readings WHERE CycleId IS NULL GROUP BY GroupId ORDER BY GroupId";
    using var r = cmd.ExecuteReader();
    while (r.Read())
        Console.WriteLine($"  group={r["GroupId"]} | N={r["N"]} | min={r["MinTs"]} | max={r["MaxTs"]}");
}

Console.WriteLine("\n=== Primeros 5 timestamps por grupo (Continuous) ===");
using (var cmd = cn.CreateCommand())
{
    cmd.CommandText = "SELECT GroupId, Timestamp FROM SMM_Readings WHERE CycleId IS NULL ORDER BY GroupId, Timestamp LIMIT 30";
    using var r = cmd.ExecuteReader();
    int last = -1; int n = 0;
    while (r.Read())
    {
        var gid = Convert.ToInt32(r["GroupId"]);
        if (gid != last) { Console.WriteLine($"--- group {gid} ---"); last = gid; n = 0; }
        if (n++ < 5) Console.WriteLine($"  {r["Timestamp"]}");
    }
}

Console.WriteLine("\n=== Buckets por hora (group=6 BarChart) ===");
using (var cmd = cn.CreateCommand())
{
    cmd.CommandText = @"SELECT substr(Timestamp,1,13) AS hourBucket, COUNT(*) N
                         FROM SMM_Readings WHERE GroupId=6 AND CycleId IS NULL
                         GROUP BY hourBucket ORDER BY hourBucket";
    using var r = cmd.ExecuteReader();
    while (r.Read())
        Console.WriteLine($"  {r["hourBucket"]}:00 → {r["N"]}");
}
