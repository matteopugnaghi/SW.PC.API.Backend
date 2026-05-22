import sqlite3, os
p = r'Projects\A72.TOUTWP\data\project.db'
c = sqlite3.connect(p)
cur = c.cursor()
print('=== USERS + ROLES (active project A72.TOUTWP) ===')
rows = cur.execute("""
    SELECT u.Id, u.Username, u.FullName, u.Status, u.MustChangePassword,
           u.LockedUntil, u.FailedLoginAttempts,
           GROUP_CONCAT(r.Name, ', ') as Roles
    FROM Users u
    LEFT JOIN UserRoles ur ON ur.UserId = u.Id
    LEFT JOIN Roles r ON r.Id = ur.RoleId
    GROUP BY u.Id
    ORDER BY u.Id
""").fetchall()
for r in rows:
    print(f'  Id={r[0]:3} | {r[1]:20} | {r[2] or "":25} | Status={r[3]} | MustChg={r[4]} | Locked={r[5] or "no"} | Failed={r[6]} | Roles=[{r[7] or ""}]')

print('\n=== ROLES disponibles ===')
for r in cur.execute("SELECT Id, Name, Description FROM Roles ORDER BY Id").fetchall():
    print(f'  Id={r[0]} | {r[1]:20} | {r[2] or ""}')

print('\n=== PROJECTS disponibles ===')
import json
for d in os.listdir('Projects'):
    full = os.path.join('Projects', d)
    if os.path.isdir(full) and not d.startswith('_'):
        db = os.path.join(full, 'data', 'project.db')
        has_db = 'DB ok' if os.path.exists(db) else 'NO DB'
        print(f'  - {d}  [{has_db}]')

with open('active-project.json','r',encoding='utf-8') as f:
    print('\nactive-project.json:', f.read().strip())
c.close()
