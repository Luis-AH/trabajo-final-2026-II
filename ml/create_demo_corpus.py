import json
from pathlib import Path

catalog = [
('Computación y accesorios', ['Laptop para clases','Teclado mecánico','Mouse inalámbrico','Monitor para escritorio','Tablet para apuntes','Memoria USB'], 'Computación para estudiar, clases virtuales y tareas universitarias. Equipo portátil, accesorios y escritorio de trabajo.'),
('Ropa', ['Casaca de abrigo','Polo de algodón','Chompa para invierno','Pantalón cómodo','Camisa de vestir','Buzo deportivo'], 'Ropa cómoda, abrigo para invierno y prendas para uso diario. Tallas variadas y material de algodón.'),
('Muebles', ['Silla de escritorio','Mesa de estudio','Estante para libros','Escritorio compacto','Sillón de sala','Repisa de madera'], 'Mueble de madera para organizar el hogar. Escritorio, silla y espacio para estudiar o trabajar.'),
('Deportes', ['Bicicleta urbana','Pelota de fútbol','Casco para bicicleta','Mancuernas ligeras','Raqueta de tenis','Bicicleta de montaña'], 'Artículos deportivos para entrenar y pasear al aire libre. Bicicleta, deporte, ejercicio y protección.'),
('Libros', ['Libro de programación','Novela de aventuras','Libro de matemática','Manual de física','Colección de cuentos','Libro de historia'], 'Libros para lectura y aprendizaje. Material de estudio, programación y lectura universitaria, páginas completas.'),
('Oficina y útiles', ['Cuadernos para apuntes','Lámpara de estudio','Archivador de documentos','Set de lápices','Calculadora científica','Organizador de escritorio'], 'Útiles de oficina para estudiar, escribir apuntes y organizar documentos. Material escolar y universitario.'),
('Celulares y accesorios', ['Celular básico','Celular para fotografía','Cargador USB','Audífonos con cable','Funda protectora','Soporte para celular'], 'Teléfono móvil y accesorios para comunicación, llamadas y clases virtuales. Celular, batería y conexiones.'),
('Electrodomésticos', ['Licuadora de cocina','Hervidor eléctrico','Plancha de ropa','Ventilador pequeño','Tostadora','Batidora manual'], 'Electrodoméstico de cocina y hogar. Aparato para preparar alimentos o facilitar tareas domésticas.')]
rows=[]
for category, titles, description in catalog:
    for title in titles:
        i=len(rows)+1
        rows.append({'id':f'demo-ml-art-{i:03d}','title':'[DEMO] '+title,
                     'description':description+' Publicación ficticia para demostración académica; no realizar pagos.',
                     'category':category,'mode':['Venta','Intercambio','Donacion'][(i-1)%3],
                     'condition':['Nuevo','ComoNuevo','BuenEstado','Aceptable'][(i-1)%4],
                     'price':float(30+i*12) if (i-1)%3==0 else None,
                     'available':i not in (6,12,18,24,30,36,42,48)})
Path('ml/demo-corpus.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
print('Corpus DEMO reproducible:',len(rows),'publicaciones')
