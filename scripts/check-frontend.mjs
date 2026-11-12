// Node >=18. Pruebas de DOM simulado: no sustituyen revisión visual ni Maps real.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';
const root = new URL('../', import.meta.url);
const read = path => fs.readFile(new URL(path, root), 'utf8');
const recommendations = await read('SegundaOportunidad/wwwroot/js/recommendations.js');
class Element {
    children = []; attributes = {}; textContent = ''; dataset = {}; style = {};
    classes = new Set();
    classList = { add: name => this.classes.add(name), remove: name => this.classes.delete(name) };
    addEventListener(name, handler) { this[name] = handler; }
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
    let timer, cleared = false, request, observer;
    const context = {
        document: { querySelectorAll: () => [section], createElement: tag => Object.assign(new Element(), { tag }) },
        URLSearchParams, Intl, AbortController, encodeURIComponent,
        matchMedia: () => ({ matches: scenario === 'reduced' }),
        IntersectionObserver: scenario === 'observed' ? class { constructor(callback) { observer = this; this.callback = callback; } observe(card) { this.card = card; } unobserve(card) { this.unobserved = card; } } : undefined,
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
    return { status, grid, observer };
}
let result = await runCase('ok', { criterio: 'Contenido real', publicaciones: [product] });
assert.equal(result.status.textContent, 'Contenido real');
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
const animated = await runCase('observed', { publicaciones: [product, product] });
assert.equal(animated.grid.children[1].style.animationDelay, '130ms');
assert.ok(animated.grid.children[0].classes.has('recommendation-pending'));
animated.observer.callback([{ target: animated.grid.children[0], isIntersecting: true }]);
assert.ok(animated.grid.children[0].classes.has('recommendation-reveal'));
assert.ok(!animated.grid.children[0].classes.has('recommendation-pending'));
const reduced = await runCase('reduced', { publicaciones: [product] });
assert.ok(!reduced.grid.children[0].classes.has('recommendation-pending'));
const photo = await runCase('ok', { publicaciones: [{ ...product, imagen: '/images/catalog/libros.jpg', similitud: .42 }] });
assert.equal(photo.grid.children[0].children[0].src, '/images/catalog/libros.jpg');
assert.equal(photo.grid.children[0].children[1].children.find(c => c.tag === 'meter').value, .42);
console.log('OK ANIMACION: revelado al entrar en pantalla, demora escalonada, movimiento reducido, foto local y afinidad real.');
console.log('OK FRONTEND: texto malicioso sin HTML, enlaces locales, imágenes seguras, límite 12, vacío, 503, HTML inesperado, red y timeout; carga accesible restaurada.');
console.log('Sin navegador ni APIs externas.');
