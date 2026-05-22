import sqlite3, os, sys
p = r'Projects\A72.TOUTWP\data\project.db'
print('DB size:', os.path.getsize(p), 'bytes')
c = sqlite3.connect(p)
cur = c.cursor()
tables = ['OperationLogs', 'LoginAttempts', 'UserSessions', 'Users', 'UserRoles', 'SystemConfigurations']
rows = cur.execute(
    "SELECT name, sql FROM sqlite_master WHERE type='table' ORDER BY name"
).fetchall()
all_names = [r[0] for r in rows]
print('\n=== ALL TABLES ({}): ==='.format(len(all_names)))
print(', '.join(all_names))
for name, sql in rows:
    if name in tables:
        print('\n=== {} ==='.format(name))
        print(sql)
        cols = cur.execute(f"PRAGMA table_info({name})").fetchall()
        print('Columns:', [(c[1], c[2]) for c in cols])
c.close()
