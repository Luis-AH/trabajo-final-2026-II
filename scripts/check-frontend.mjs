// Node >=18. Pruebas de DOM simulado: no sustituyen revisión visual ni Maps real.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';
const root = new URL('../', import.meta.url);
const read = path => fs.readFile(new URL(path, root), 'utf8');
const recommendations = await read('SegundaOportunidad/wwwroot/js/recommendations.js');
class Element {
    children = []; attributes = {}; textContent = ''; dataset = {};
    appendChild(node) { this.children.push(node); return node; }
    setAttribute(key, value) { this.attributes[key] = value; }
    set innerHTML(_) { throw new Error('No debe insertar HTML del servidor o IA.'); }
}
const malicious = '<img src=x onerror=alert(1)>';
const product = { id: 'real-01', titulo: malicious, categoria: 'Libros', modalidad: 'Venta', precio: 100, motivo: malicious, imagen: 'javascript:alert(1)' };
const settle = async () => { for (let i = 0; i < 12; i++) await Promise.resolve(); };
async function runCase(scenario, payload) {
    const section = new Element(), status = new Element(), grid = new Element();
    section.dataset.query = 'libros & estudio';
    section.querySelector = selector => selector === '.ml-status' ? status : grid;
    let timer, cleared = false, request;
    const context = {
        document: { querySelectorAll: () => [section], createElement: tag => Object.assign(new Element(), { tag }) },
        URLSearchParams, Intl, AbortController, encodeURIComponent,
        setTimeout: callback => { timer = callback; return 1; }, clearTimeout: () => { cleared = true; },
        fetch: (url, options) => {
            request = { url, options };
            if (scenario === 'timeout') return new Promise((_, reject) => options.signal.addEventListener('abort', () => reject(Object.assign(new Error(), { name: 'AbortError' }))));
            if (scenario === 'offline') return Promise.reject(new Error('Conexión no disponible.'));
            return Promise.resolve({ ok: scenario !== '503', headers: { get: () => scenario === 'html' ? 'text/html' : 'application/json; charset=utf-8' }, json: async () => payload });
        }
    };
    vm.runInNewContext(recommendations, context, { filename: 'recommendations.js' });
    assert.equal(section.attributes['aria-busy'], 'true');
    if (scenario === 'timeout') timer();
    await settle();
    assert.equal(section.attributes['aria-busy'], 'false'); assert.ok(cleared);
    assert.equal(request.options.credentials, 'same-origin');
    assert.equal(new URL(request.url, 'https://example.invalid').searchParams.get('consulta'), 'libros & estudio');
    return { status, grid };
}
let result = await runCase('ok', { criterio: 'Contenido real', publicaciones: [product] });
assert.equal(result.grid.children.length, 1);
const card = result.grid.children[0], image = card.children[0], body = card.children[1];
assert.equal(image.src, '/images/catalog-placeholder.svg');
assert.equal(body.children.find(c => c.tag === 'h3').textContent, malicious);
assert.equal(body.children.find(c => c.tag === 'a').href, '/Articulos/Details/real-01');
assert.equal((await runCase('ok', { publicaciones: [] })).grid.children.length, 0);
assert.match((await runCase('ok', { publicaciones: [] })).status.textContent, /no hay suficientes/);
assert.equal((await runCase('503', { criterio: 'Modelo no disponible.' })).status.textContent, 'Modelo no disponible.');
assert.match((await runCase('html', {})).status.textContent, /servicio de recomendaciones/);
assert.match((await runCase('offline', {})).status.textContent, /Conexión no disponible/);
assert.match((await runCase('timeout', {})).status.textContent, /tardó demasiado/);
assert.equal((await runCase('ok', { publicaciones: Array.from({ length: 30 }, () => product) })).grid.children.length, 12);
console.log('OK FRONTEND: texto malicioso sin HTML, enlaces locales, imágenes seguras, límite 12, vacío, 503, HTML inesperado, red y timeout; carga accesible restaurada.');
console.log('Sin navegador ni APIs externas.');
