(() => {
    const reducedMotion = typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
    const reveal = card => { card.classList.remove('recommendation-pending'); card.classList.add('recommendation-reveal'); };
    const observer = !reducedMotion && typeof IntersectionObserver === 'function' ? new IntersectionObserver(entries => {
        for (const entry of entries) if (entry.isIntersecting) { reveal(entry.target); observer.unobserve(entry.target); }
    }, { threshold: 0.12 }) : null;
    const money = value => value == null ? 'Sin precio de venta' : new Intl.NumberFormat('es-PE', { style: 'currency', currency: 'PEN' }).format(value);
    for (const section of document.querySelectorAll('.ml-recommendations')) {
        const status = section.querySelector('.ml-status'), grid = section.querySelector('.recommendation-grid');
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 15000);
        section.setAttribute('aria-busy', 'true');
        const parameters = new URLSearchParams({ cantidad: '4' });
        if (section.dataset.article) parameters.set('articuloId', section.dataset.article);
        if (section.dataset.query) parameters.set('consulta', section.dataset.query);
        fetch('/api/ml/recomendaciones?' + parameters.toString(), { signal: controller.signal, credentials: 'same-origin', headers: { Accept: 'application/json' } }).then(async response => {
            if (!response.headers.get('content-type')?.includes('application/json')) throw new Error('El servicio de recomendaciones no está disponible ahora.');
            const data = await response.json(); if (!response.ok) throw new Error(data.criterio || 'Las recomendaciones no están disponibles ahora.'); return data;
        }).then(data => {
            status.textContent = data.criterio;
            for (const [index, item] of (Array.isArray(data.publicaciones) ? data.publicaciones : []).slice(0, 12).entries()) {
                const card = document.createElement('article'); card.className = 'recommendation-card';
                card.style.animationDelay = `${Math.min(index, 5) * 130}ms`;
                const image = document.createElement('img'); image.src = '/images/catalog-placeholder.svg'; image.alt = ''; image.loading = 'lazy';
                if (typeof item.imagen === 'string' && (item.imagen.startsWith('/Articulos/Imagen/') || /^\/images\/catalog\/[a-z]+\.jpg$/.test(item.imagen) || item.imagen === '/images/demo-placeholder.svg')) image.src = item.imagen;
                image.alt = `Imagen ilustrativa: ${item.titulo}`;
                card.appendChild(image);
                const body = document.createElement('div'); body.className = 'recommendation-body'; card.appendChild(body);
                const add = (tag, text, style) => { const node = document.createElement(tag); node.textContent = text; if (style) node.className = style; body.appendChild(node); return node; };
                add('span', item.categoria, 'eyebrow'); add('h3', item.titulo, 'h6'); add('p', item.modalidad === 'Donacion' ? 'Donación' : item.modalidad === 'Intercambio' ? 'Intercambio' : money(item.precio), 'product-price');
                add('small', 'Recomendado por afinidad de contenido', 'ml-affinity');
                if (Number.isFinite(item.similitud)) {
                    const score = Math.max(0, Math.min(1, item.similitud));
                    const label = add('small', `Afinidad textual: ${Math.round(score * 100)}%`, 'ml-affinity-score');
                    label.title = 'Similitud del texto calculada por el modelo. No mide calidad, seguridad ni probabilidad de compra.';
                    const meter = add('meter', '', 'ml-affinity-meter'); meter.min = 0; meter.max = 1; meter.value = score;
                    meter.setAttribute('aria-label', `Afinidad textual ${Math.round(score * 100)} por ciento`);
                }
                add('p', item.motivo, 'small text-muted');
                const link = add('a', 'Ver publicación', 'btn btn-outline-brand btn-sm'); link.href = '/Articulos/Details/' + encodeURIComponent(item.id);
                grid.appendChild(card);
                if (!reducedMotion) {
                    if (observer) { card.classList.add('recommendation-pending'); observer.observe(card); card.addEventListener('focusin', () => reveal(card)); }
                    else reveal(card);
                }
            }
            if (!data.publicaciones?.length) status.textContent = 'Aún no hay suficientes coincidencias. Explora el catálogo o publica tus intereses.';
        }).catch(error => { status.textContent = error.name === 'AbortError' ? 'La consulta tardó demasiado. Vuelve a intentarlo más tarde.' : error.message || 'No se pudieron cargar las recomendaciones.'; })
            .finally(() => { clearTimeout(timeout); section.setAttribute('aria-busy', 'false'); });
    }
})();
