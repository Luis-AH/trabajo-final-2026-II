-- CreateTable
CREATE TABLE "Cliente" (
    "id" TEXT NOT NULL PRIMARY KEY,
    "nombre" TEXT NOT NULL,
    "email" TEXT NOT NULL,
    "reputacion" INTEGER NOT NULL DEFAULT 0
);

-- CreateTable
CREATE TABLE "Articulo" (
    "id" TEXT NOT NULL PRIMARY KEY,
    "titulo" TEXT NOT NULL,
    "descripcion" TEXT,
    "precio" REAL,
    "tipo" TEXT NOT NULL,
    "propietarioId" TEXT NOT NULL,
    CONSTRAINT "Articulo_propietarioId_fkey" FOREIGN KEY ("propietarioId") REFERENCES "Cliente" ("id") ON DELETE RESTRICT ON UPDATE CASCADE
);

-- CreateTable
CREATE TABLE "PropuestaIntercambio" (
    "id" TEXT NOT NULL PRIMARY KEY,
    "estado" TEXT NOT NULL DEFAULT 'Pendiente',
    "ofertanteId" TEXT NOT NULL,
    "receptorId" TEXT NOT NULL,
    "articuloOfertadoId" TEXT NOT NULL,
    "articuloDeseadoId" TEXT NOT NULL,
    CONSTRAINT "PropuestaIntercambio_ofertanteId_fkey" FOREIGN KEY ("ofertanteId") REFERENCES "Cliente" ("id") ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT "PropuestaIntercambio_receptorId_fkey" FOREIGN KEY ("receptorId") REFERENCES "Cliente" ("id") ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT "PropuestaIntercambio_articuloOfertadoId_fkey" FOREIGN KEY ("articuloOfertadoId") REFERENCES "Articulo" ("id") ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT "PropuestaIntercambio_articuloDeseadoId_fkey" FOREIGN KEY ("articuloDeseadoId") REFERENCES "Articulo" ("id") ON DELETE RESTRICT ON UPDATE CASCADE
);

-- CreateIndex
CREATE UNIQUE INDEX "Cliente_email_key" ON "Cliente"("email");
