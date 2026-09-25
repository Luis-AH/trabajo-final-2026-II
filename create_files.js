const fs = require('fs');
const path = require('path');

fs.mkdirSync('src/controllers', { recursive: true });
fs.mkdirSync('src/routes', { recursive: true });

const controllerCode = "const { PrismaClient } = require('@prisma/client');\nconst prisma = new PrismaClient();\n\nconst obtenerArticulos = async (req, res) => {\n    try {\n        const articulos = await prisma.articulo.findMany({\n            include: { propietario: true }\n        });\n        res.json(articulos);\n    } catch (error) {\n        res.status(500).json({ error: 'Error al obtener los artículos' });\n    }\n};\n\nconst crearArticulo = async (req, res) => {\n    try {\n        const { titulo, descripcion, precio, tipo, propietarioId } = req.body;\n        const nuevoArticulo = await prisma.articulo.create({\n            data: { titulo, descripcion, precio, tipo, propietarioId }\n        });\n        res.status(201).json(nuevoArticulo);\n    } catch (error) {\n        res.status(500).json({ error: 'Error al crear el artículo' });\n    }\n};\n\nmodule.exports = { obtenerArticulos, crearArticulo };\n";
fs.writeFileSync('src/controllers/ArticulosController.js', controllerCode);

const routeCode = "const express = require('express');\nconst router = express.Router();\nconst { obtenerArticulos, crearArticulo } = require('../controllers/ArticulosController');\n\nrouter.get('/', obtenerArticulos);\nrouter.post('/', crearArticulo);\n\nmodule.exports = router;\n";
fs.writeFileSync('src/routes/articulosRoutes.js', routeCode);

let serverCode = fs.readFileSync('server.js', 'utf8');
serverCode = serverCode.replace(
  "app.use(express.static(path.join(__dirname, 'public')));",
  "app.use(express.static(path.join(__dirname, 'public')));\napp.use(express.json());\n\nconst articulosRoutes = require('./src/routes/articulosRoutes');\napp.use('/api/articulos', articulosRoutes);\n"
);
fs.writeFileSync('server.js', serverCode);