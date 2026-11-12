import argparse, json, sqlite3
from pathlib import Path

parser=argparse.ArgumentParser();parser.add_argument('--db',required=True);parser.add_argument('--output',default='ml/artifacts/catalog.json');args=parser.parse_args()
connection=sqlite3.connect(Path(args.db).resolve().as_uri()+'?mode=ro',uri=True)
connection.row_factory=sqlite3.Row
rows=connection.execute('''SELECT a.Id id,a.Title title,a.Description description,c.Name category,a.Modalidad mode,a.EstadoFisico condition
FROM Articulos a JOIN Categorias c ON c.Id=a.CategoriaId JOIN AspNetUsers u ON u.Id=a.UserId
WHERE a.IsActive=1 AND a.Moderacion=0 AND (u.AccountSuspendedUntilUtc IS NULL OR u.AccountSuspendedUntilUtc<=datetime('now'))
AND (u.PublishingSuspendedUntilUtc IS NULL OR u.PublishingSuspendedUntilUtc<=datetime('now'))
AND ((u.IsVerified=1 AND u.VerifiedAtUtc IS NOT NULL AND u.VerifiedByAdminId IS NOT NULL) OR EXISTS
(SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId=u.Id AND r.Name='Admin'))
AND NOT EXISTS(SELECT 1 FROM Intercambios i WHERE i.Estado=3 AND (i.OfferedArticleId=a.Id OR i.RequestedArticleId=a.Id)) ORDER BY a.Id''').fetchall()
result=[]
for row in rows:
    item=dict(row);item['mode']=['Venta','Intercambio','Donacion'][item['mode']];item['condition']=['Nuevo','ComoNuevo','BuenEstado','Aceptable'][item['condition']];item['available']=True;result.append(item)
p=Path(args.output);p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print('Snapshot solo lectura:',len(result),'publicaciones; sin datos de Identity exportados.')
