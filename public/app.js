document.addEventListener('DOMContentLoaded', () => {
    console.log('Aplicación del Marketplace Circular inicializada.');
    
    // Variables globales
    let articulos = [];
    
    // Elementos del DOM para la transición de Login
    const loginForm = document.getElementById('login-form');
    const loginView = document.getElementById('login-view');
    const mainLayout = document.getElementById('main-layout');

    if (loginForm) {
        loginForm.addEventListener('submit', (e) => {
            e.preventDefault();
            loginView.classList.add('hidden');
            mainLayout.classList.remove('hidden');
        });
    }

    const btnLogout = document.getElementById('btn-logout');
    if (btnLogout) {
        btnLogout.addEventListener('click', () => {
            mainLayout.classList.add('hidden');
            loginView.classList.remove('hidden');
        });
    }

    // --- Elementos de Vista de Detalle ---
    const catalogSection = document.querySelector('.catalog-section');
    const detailSection = document.getElementById('article-detail');
    const btnBack = document.getElementById('btn-back');
    
    const detailImage = document.getElementById('detail-image');
    const detailBadge = document.getElementById('detail-badge');
    const detailTitle = document.getElementById('detail-title');
    const detailLocation = document.getElementById('detail-location');
    const actionTitle = document.getElementById('action-title');
    const exchangeGroup = document.getElementById('exchange-offer-group');
    const btnActionSubmit = document.getElementById('btn-action-submit');
    const actionForm = document.getElementById('action-form');

    if (btnBack) {
        btnBack.addEventListener('click', () => {
            detailSection.classList.add('hidden');
            catalogSection.classList.remove('hidden');
        });
    }

    if (actionForm) {
        actionForm.addEventListener('submit', (e) => {
            e.preventDefault();
            alert('¡Acción procesada exitosamente en el MVP!');
            detailSection.classList.add('hidden');
            catalogSection.classList.remove('hidden');
        });
    }

    const mostrarDetalle = (idArticulo) => {
        const art = articulos.find(a => a.id === idArticulo);
        if(!art) return;

        detailImage.src = 'https://images.unsplash.com/photo-1593085260707-5377ba37f868?q=80&w=600&auto=format&fit=crop'; 
        detailTitle.textContent = art.titulo;
        detailLocation.textContent = art.propietario ? 'Vendedor: ' + art.propietario.nombre : 'Ubicación no especificada';

        const tipoLimpio = art.tipo ? art.tipo.toLowerCase() : 'donacion';
        let badgeClass = tipoLimpio === 'intercambio' ? 'trueque' : tipoLimpio;
        detailBadge.className = 'badge ' + badgeClass;
        detailBadge.textContent = tipoLimpio === 'venta' ? 'Venta - S/ ' + (art.precio || 0) : 
                                  tipoLimpio === 'intercambio' ? 'Trueque' : 'Donación';

        if (tipoLimpio === 'intercambio' || tipoLimpio === 'trueque') {
            actionTitle.textContent = 'Proponer Intercambio';
            exchangeGroup.classList.remove('hidden');
            btnActionSubmit.textContent = 'Enviar Propuesta';
        } else if (tipoLimpio === 'venta') {
            actionTitle.textContent = 'Comprar por S/ ' + (art.precio || 0);
            exchangeGroup.classList.add('hidden');
            btnActionSubmit.textContent = 'Confirmar Compra';
        } else {
            actionTitle.textContent = 'Solicitar Donación';
            exchangeGroup.classList.add('hidden');
            btnActionSubmit.textContent = 'Solicitar Artículo';
        }

        catalogSection.classList.add('hidden');
        detailSection.classList.remove('hidden');
        window.scrollTo({ top: 0, behavior: 'smooth' });
    };

    const renderArticulos = () => {
        const grid = document.getElementById('marketplace-items');
        if (!grid) return;
        grid.innerHTML = '';
        
        articulos.forEach(art => {
            const card = document.createElement('article');
            card.className = 'card';
            
            const tipoLimpio = art.tipo ? art.tipo.toLowerCase() : 'donacion';
            let badgeClass = tipoLimpio === 'intercambio' ? 'trueque' : tipoLimpio;
            let badgeText = tipoLimpio === 'venta' ? 'Venta - S/ ' + (art.precio || 0) : 
                            tipoLimpio === 'intercambio' ? 'Trueque' : 'Donación';

            const imgSrc = 'https://images.unsplash.com/photo-1593085260707-5377ba37f868?q=80&w=600&auto=format&fit=crop';
            const locationText = art.propietario ? 'Vendedor: ' + art.propietario.nombre : 'Lima';

            card.innerHTML = 
                <div class="card-image" style="background-image: url('');"></div>
                <div class="card-content">
                    <span class="badge "></span>
                    <h3></h3>
                    <p class="location"></p>
                    <button class="btn btn-outline btn-full btn-ver-detalles">Ver detalles</button>
                </div>
            ;
            
            const btnDetail = card.querySelector('.btn-ver-detalles');
            btnDetail.addEventListener('click', () => mostrarDetalle(art.id));
            
            grid.appendChild(card);
        });
    };

    // --- FASE 5: Cargar Catálogo Dinámicamente desde API ---
    const cargarCatalogo = async () => {
        try {
            const res = await fetch('/api/articulos');
            const data = await res.json();
            articulos = data;
            renderArticulos();
        } catch (error) {
            console.error('Error al cargar catálogo:', error);
            const grid = document.getElementById('marketplace-items');
            if (grid) grid.innerHTML = '<p>No se pudo cargar el catálogo.</p>';
        }
    };

    cargarCatalogo();
});
