const { PrismaClient } = require('@prisma/client');
const prisma = new PrismaClient();

const obtenerArticulos = async (req, res) => {
    try {
        const articulos = await prisma.articulo.findMany({
            include: { propietario: true }
        });
        res.json(articulos);
    } catch (error) {
        res.status(500).json({ error: 'Error al obtener los artículos' });
    }
};

const crearArticulo = async (req, res) => {
    try {
        const { titulo, descripcion, precio, tipo, propietarioId } = req.body;
        const nuevoArticulo = await prisma.articulo.create({
            data: { titulo, descripcion, precio, tipo, propietarioId }
        });
        res.status(201).json(nuevoArticulo);
    } catch (error) {
        res.status(500).json({ error: 'Error al crear el artículo' });
    }
};

module.exports = { obtenerArticulos, crearArticulo };
