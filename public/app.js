document.addEventListener('DOMContentLoaded', () => {
    console.log('Aplicación del Marketplace Circular inicializada.');
    
    // Elementos del DOM para la transición de Login
    const loginForm = document.getElementById('login-form');
    const loginView = document.getElementById('login-view');
    const mainLayout = document.getElementById('main-layout');

    // Lógica del Login (Fase 2)
    if (loginForm) {
        loginForm.addEventListener('submit', (e) => {
            e.preventDefault(); // Evitamos recargar la página

            // Transición suave ocultando login y mostrando el main
            loginView.classList.add('hidden');
            mainLayout.classList.remove('hidden');
            
            console.log('Usuario ha ingresado. Mostrando layout principal.');
        });
    }

    const btnLogout = document.getElementById('btn-logout');
    if (btnLogout) {
        btnLogout.addEventListener('click', () => {
            mainLayout.classList.add('hidden');
            loginView.classList.remove('hidden');
        });
    }

    // --- Elementos de Vista de Detalle (Fase 4) ---
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

        // Llenar datos básicos
        detailImage.src = art.imagen;
        detailTitle.textContent = art.titulo;
        detailLocation.textContent = art.ubicacion;

        // Configurar etiqueta (badge)
        detailBadge.className = `badge ${art.tipo}`;
        detailBadge.textContent = art.tipo === 'venta' ? `Venta - ${art.precio}` : 
                                  art.tipo === 'trueque' ? 'Trueque' : 'Donación';

        // Configurar Action Box según el tipo de transacción
        if (art.tipo === 'trueque') {
            actionTitle.textContent = 'Proponer Intercambio';
            exchangeGroup.classList.remove('hidden');
            btnActionSubmit.textContent = 'Enviar Propuesta';
        } else if (art.tipo === 'venta') {
            actionTitle.textContent = `Comprar por ${art.precio}`;
            exchangeGroup.classList.add('hidden');
            btnActionSubmit.textContent = 'Confirmar Compra';
        } else { // donacion
            actionTitle.textContent = 'Solicitar Donación';
            exchangeGroup.classList.add('hidden');
            btnActionSubmit.textContent = 'Solicitar Artículo';
        }

        // Transición de vistas
        catalogSection.classList.add('hidden');
        detailSection.classList.remove('hidden');
        window.scrollTo({ top: 0, behavior: 'smooth' });
    };

    // --- FASE 3: Catálogo Dinámico ---
    const articulos = [
        {
            id: 1,
            titulo: "Bicicleta Urbana Retro",
            tipo: "venta",
            precio: "S/ 250.00",
            ubicacion: "San Isidro, Lima",
            imagen: "https://images.unsplash.com/photo-1485965120184-e220f721d03e?q=80&w=600&auto=format&fit=crop"
        },
        {
            id: 2,
            titulo: "Macetas de cerámica",
            tipo: "trueque",
            precio: "Intercambio",
            ubicacion: "Barranco, Lima",
            imagen: "https://images.unsplash.com/photo-1485955900006-10f4d324d411?q=80&w=600&auto=format&fit=crop"
        },
        {
            id: 3,
            titulo: "Abrigo de invierno",
            tipo: "donacion",
            precio: "Gratis",
            ubicacion: "Surco, Lima",
            imagen: "https://images.unsplash.com/photo-1551028719-00167b16eac5?q=80&w=600&auto=format&fit=crop"
        },
        {
            id: 4,
            titulo: "Libros universitarios",
            tipo: "donacion",
            precio: "Gratis",
            ubicacion: "Cercado de Lima",
            imagen: "https://images.unsplash.com/photo-1512820790803-83ca734da794?q=80&w=600&auto=format&fit=crop"
        },
        {
            id: 5,
            titulo: "Mesa de centro vintage",
            tipo: "venta",
            precio: "S/ 120.00",
            ubicacion: "Miraflores, Lima",
            imagen: "https://images.unsplash.com/photo-1533090481720-856c6e3c1fdc?q=80&w=600&auto=format&fit=crop"
        },
        {
            id: 6,
            titulo: "Cámara analógica",
            tipo: "trueque",
            precio: "Intercambio",
            ubicacion: "Lince, Lima",
            imagen: "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?q=80&w=600&auto=format&fit=crop"
        }
    ];

    const renderArticulos = () => {
        const grid = document.getElementById('marketplace-items');
        if (!grid) return;
        
        grid.innerHTML = ''; // Limpiamos el contenedor
        
        articulos.forEach(art => {
            const card = document.createElement('article');
            card.className = 'card';
            
            // Determinar estilo de la etiqueta (badge) según el tipo
            let badgeClass = art.tipo;
            let badgeText = art.tipo === 'venta' ? `Venta - ${art.precio}` : 
                            art.tipo === 'trueque' ? 'Trueque' : 'Donación';

            card.innerHTML = `
                <div class="card-image" style="background-image: url('${art.imagen}');"></div>
                <div class="card-content">
                    <span class="badge ${badgeClass}">${badgeText}</span>
                    <h3>${art.titulo}</h3>
                    <p class="location">${art.ubicacion}</p>
                    <button class="btn btn-outline btn-full btn-ver-detalles">Ver detalles</button>
                </div>
            `;
            
            // Asignar evento click al botón de detalles
            const btnDetail = card.querySelector('.btn-ver-detalles');
            btnDetail.addEventListener('click', () => mostrarDetalle(art.id));
            
            grid.appendChild(card);
        });
    };

    // Renderizar los artículos dinámicamente al iniciar
    renderArticulos();
});
