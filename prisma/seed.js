const { PrismaClient } = require('@prisma/client');
const prisma = new PrismaClient();

async function main() {
    const cliente1 = await prisma.cliente.create({
        data: {
            nombre: 'Ana Gómez',
            email: 'ana.gomez@ejemplo.com',
            reputacion: 5,
        }
    });

    const cliente2 = await prisma.cliente.create({
        data: {
            nombre: 'Carlos Ruiz',
            email: 'carlos.ruiz@ejemplo.com',
            reputacion: 10,
        }
    });

    await prisma.articulo.create({
        data: {
            titulo: 'Bicicleta Urbana Retro',
            descripcion: 'Bicicleta en buen estado, color azul.',
            precio: 250.00,
            tipo: 'Venta',
            propietarioId: cliente1.id
        }
    });

    await prisma.articulo.create({
        data: {
            titulo: 'Macetas de cerámica',
            descripcion: 'Set de 3 macetas pintadas a mano.',
            precio: null,
            tipo: 'Intercambio',
            propietarioId: cliente1.id
        }
    });

    await prisma.articulo.create({
        data: {
            titulo: 'Abrigo de invierno',
            descripcion: 'Abrigo casi nuevo, ideal para el frío limeño.',
            precio: 120.00,
            tipo: 'Venta',
            propietarioId: cliente2.id
        }
    });

    await prisma.articulo.create({
        data: {
            titulo: 'Cámara analógica',
            descripcion: 'Cámara antigua de rollo, ideal para coleccionistas.',
            precio: null,
            tipo: 'Intercambio',
            propietarioId: cliente2.id
        }
    });

    console.log('¡Siembra de datos (Seed) ejecutada exitosamente!');
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
