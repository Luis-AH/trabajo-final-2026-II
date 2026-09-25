const express = require('express');
const router = express.Router();
const { obtenerArticulos, crearArticulo } = require('../controllers/ArticulosController');

router.get('/', obtenerArticulos);
router.post('/', crearArticulo);

module.exports = router;
